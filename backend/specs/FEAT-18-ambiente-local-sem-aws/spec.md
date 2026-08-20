# FEAT-18: Ambiente local sem dependência de AWS real

## Objetivo

Permitir que o backend rode **inteiramente na máquina do desenvolvedor**,
sem se conectar a nenhum recurso AWS real (nem produção, nem
homologação), usando containers Docker que emulam DynamoDB, Parameter
Store e Cognito. O objetivo é eliminar a dependência de credenciais AWS
e de conectividade para o dia a dia de desenvolvimento, mantendo
produção e homologação exatamente como estão hoje.

## Contexto

Desde a criação do projeto, `backend/docs/constitution.md` e
`backend/infra/CLAUDE.md` estabelecem como regra imutável que **não há
ambiente simulado**: mesmo em desenvolvimento local, o backend se
conecta diretamente a DynamoDB, Cognito e Parameter Store reais na AWS
(ver `backend/infra/CLAUDE.md`, seção "Princípios"). Essa regra tornou
o desenvolvimento local dependente de credenciais AWS válidas e de
conectividade constante com a conta real — inclusive de homologação, o
que traz risco de um erro local afetar dados de hom sem querer.

Esta feature **reverte esse princípio, mas só para o ambiente local**:
produção (`environments/prod/`) e homologação (`environments/hom/`)
continuam inalteradas, 100% AWS real, providas por Terraform, sem
nenhuma mudança nesta feature. O que muda é exclusivamente como o
backend roda na máquina do desenvolvedor.

Decisões já confirmadas com o usuário para esta feature:
- **DynamoDB e Parameter Store**: emulados via **LocalStack** (edição
  Community, gratuita — cobre os dois serviços).
- **Cognito**: LocalStack Community **não suporta Cognito** (recurso
  exclusivo da edição paga). Em vez disso, usar
  **[cognito-local](https://github.com/jagregory/cognito-local)**, um
  projeto open-source que emula a API do Cognito (sign-up,
  `USER_PASSWORD_AUTH`, JWKS) rodando em container próprio, separado do
  LocalStack. Como ele expõe a mesma API HTTP do Cognito real, o AWS
  SDK usado hoje em `GastosApp.Infrastructure` continua sendo usado sem
  reescrita — só muda o endpoint configurado.
- **Artefatos legados** (`backend/infra/docker-compose.yml`,
  `kong.yml`, `scripts/localstack-init/`, de uma tentativa anterior já
  documentada como "decisão pendente" em `backend/infra/CLAUDE.md`):
  serão **removidos e recriados do zero** nesta feature, já que Kong
  não faz parte da solução atual e as suposições antigas de LocalStack
  estão desatualizadas.

## Requisitos de negócio / restrições

- **Isolamento total de AWS real**: subir o ambiente local não pode, em
  nenhuma circunstância, ler ou escrever em recursos reais de produção
  ou homologação. A configuração local aponta exclusivamente para os
  containers (`localhost`), nunca para endpoints AWS reais.
- **Um comando para subir tudo**: `docker compose up` (a partir de
  `backend/infra/`) deve subir LocalStack (DynamoDB + SSM Parameter
  Store) e cognito-local, prontos para uso, sem passos manuais
  adicionais além de rodar um script de inicialização/seed (criação da
  tabela, dos parâmetros e do user pool local).
- **Mesmo contrato de API**: nenhuma mudança de endpoint, request,
  response ou status code. O backend rodando localmente expõe
  exatamente o mesmo contrato hoje documentado em
  `backend/docs/openapi.json`.
- **Sem custo**: todos os serviços emulados rodam localmente em
  containers Docker, sem nenhuma chamada à conta AWS real e,
  portanto, sem custo algum.
- **Configuração via variável de ambiente / `appsettings.Development.json`**,
  seguindo o padrão já usado para `ParameterStore__Path` e
  `DynamoDb__TableName` (FEAT-13) — os endpoints locais (DynamoDB,
  Parameter Store, Cognito) devem ser configuráveis e ter como default
  os valores reais da AWS, para que Lambda/produção/homologação
  continuem funcionando exatamente como hoje sem precisar declarar
  nada a mais. A troca para os endpoints locais só acontece quando
  explicitamente configurada (ambiente de desenvolvimento).
- **Sem mudança de comportamento em produção/homologação**: nenhuma
  variável de ambiente, configuração Terraform ou comportamento da
  Lambda publicada muda como consequência desta feature.
- **Documentação atualizada**: `backend/docs/constitution.md`,
  `backend/CLAUDE.md` e `backend/infra/CLAUDE.md` devem refletir o novo
  princípio — ambiente local emulado, produção/homologação continuam
  100% AWS real — substituindo o texto atual que proíbe simulação
  local.

## User Stories

**US1 — Subir o ambiente local com um comando**
- Given o repositório clonado e Docker instalado
- When o desenvolvedor roda `docker compose up` (mais o script de
  inicialização) em `backend/infra/`
- Then LocalStack (DynamoDB + Parameter Store) e cognito-local sobem
  prontos para uso, com a tabela, os parâmetros e o user pool local já
  criados

**US2 — CRUD de despesas funciona 100% local**
- Given o ambiente local no ar e o backend configurado para apontar
  para os endpoints locais
- When o desenvolvedor cria, consulta, atualiza e exclui uma despesa
  via API
- Then as operações são persistidas na tabela DynamoDB do LocalStack, e
  nenhuma chamada é feita à AWS real

**US3 — Autenticação funciona 100% local**
- Given o ambiente local no ar
- When o desenvolvedor registra um usuário e faz login via
  `USER_PASSWORD_AUTH` contra o cognito-local
- Then o backend valida o JWT emitido pelo cognito-local (JWKS local),
  igual ao fluxo hoje validado contra o Cognito real

**US4 — Produção e homologação sem regressão**
- Given esta feature implementada
- When o backend é publicado/executado em produção ou homologação
  (Lambda + AWS real, sem nenhuma variável de ambiente local definida)
- Then o comportamento observado é idêntico ao anterior a esta
  feature — mesmos endpoints AWS reais, sem nenhuma alteração

**US5 — Isolamento garantido mesmo por engano**
- Given uma configuração local incompleta ou mal feita
- When o backend sobe localmente
- Then não há como ele acidentalmente se conectar a DynamoDB, Cognito
  ou Parameter Store reais de produção/homologação — os defaults locais
  documentados apontam sempre para os containers

## Contratos observáveis

Nenhum endpoint, campo de request/response ou status code muda —
o contrato de wire permanece exatamente o documentado em
`backend/docs/openapi.json`. Não é necessário regenerar esse arquivo
nesta feature. A única mudança observável é operacional: o backend
passa a poder ser executado apontando para serviços locais em vez de
AWS real, controlado por configuração (detalhes técnicos — nomes de
variáveis, estrutura do `docker-compose.yml`, imagem do cognito-local —
ficam para `plan.md`).

## Critérios de aceite

- [x] `backend/infra/docker-compose.yml` novo sobe LocalStack
      (DynamoDB + SSM Parameter Store) e cognito-local, substituindo os
      artefatos legados removidos (`kong.yml`,
      `scripts/localstack-init/` antigos)
- [x] Script de inicialização cria a tabela DynamoDB (mesmo modelo de
      dados — PK/SK, GSI1, GSI2 — de produção), os parâmetros
      equivalentes no Parameter Store local e um user pool +
      app client no cognito-local, de forma repetível
      (`docker compose up` + script funcionam do zero em uma máquina
      limpa)
- [x] Endpoints de DynamoDB, Parameter Store e Cognito usados pelo
      backend são configuráveis via variável de ambiente /
      `appsettings.Development.json`, com default igual ao valor real
      da AWS (produção/homologação inalteradas)
- [x] Fluxo completo validado localmente: registro → login →
      criar/consultar/atualizar/excluir despesa → criar/consultar
      categoria, tudo contra os serviços locais, sem nenhuma chamada à
      AWS real (confirmável por ausência de credenciais AWS
      configuradas na máquina de teste)
- [x] `dotnet build`/`dotnet test` continuam passando sem alteração
- [x] Produção e homologação validadas sem regressão após a mudança
      (mesmo comportamento observável de antes desta feature)
- [x] `backend/docs/constitution.md`, `backend/CLAUDE.md` e
      `backend/infra/CLAUDE.md` atualizados para refletir o novo
      princípio (ambiente local emulado; produção/homologação
      continuam 100% AWS real)
- [x] `backend/infra/README.md` (ou seção equivalente) documenta como
      subir e usar o ambiente local do zero

## Status

**Implementado e validado.**

- `DynamoDbOptions` e `AddAwsParameterStore` ganharam
  `ServiceURL`/`AccessKey`/`SecretKey` opcionais (mesmo padrão já usado
  em `CognitoOptions`); `AddCognitoAuth` passou a montar
  `Authority`/`RequireHttpsMetadata` a partir de `Cognito:ServiceURL`
  quando presente. Produção/homologação não declaram essas chaves —
  comportamento inalterado, confirmado por smoke test (`401` sem token
  em `api.jrnexpenses.com` e `api-hom.jrnexpenses.com`, idêntico a
  antes da feature).
- Artefatos legados de LocalStack/Kong removidos;
  `backend/infra/docker-compose.yml` novo sobe LocalStack (DynamoDB +
  SSM, edição Community) e cognito-local (build próprio a partir do
  pacote npm `cognito-local@5.3.0`, já que Cognito não está disponível
  na edição gratuita do LocalStack).
- Scripts idempotentes (`local-init.sh` + 3 scripts) criam o User Pool,
  a tabela `GastosApp-Local` e os parâmetros em `/GastosApp/` no SSM
  local — testados rodando duas vezes seguidas sem duplicar recursos.
- **Achado durante a validação**: no Git Bash/MSYS (Windows),
  argumentos de linha de comando começando com `/` (ex.:
  `/GastosApp/...`) são reescritos como caminho de arquivo Windows
  antes de chegar no AWS CLI, corrompendo nomes de parâmetro
  silenciosamente (o `put-parameter` "funcionava" mas gravava em outro
  nome; `get-parameters-by-path` não encontrava nada). Corrigido
  exportando `MSYS_NO_PATHCONV=1` nos scripts — sem efeito em bash real
  (Linux/macOS/WSL).
- Validação manual end-to-end, do zero (`docker compose up` +
  `local-init.sh` numa máquina limpa): `POST /auth/register` →
  `admin-confirm-sign-up` (cognito-local) → `POST /auth/login` →
  `GET /auth/me` → `POST /categories` → `GET /categories` →
  `POST /expenses` → `GET /expenses` → `PUT /expenses/{id}` →
  `DELETE /expenses/{id}` → `DELETE /categories/{id}`, tudo contra os
  containers locais.
- `dotnet build`/`dotnet test` sem alteração: 195 testes unitários + 89
  de componente + 1 de integração, 100% passando.
- Documentação atualizada: `backend/docs/constitution.md`,
  `backend/CLAUDE.md`, `backend/infra/CLAUDE.md` e novo
  `backend/infra/README.md`.

## Fora do escopo

- Qualquer mudança em produção ou homologação (Terraform, Lambda,
  variáveis de ambiente reais) — este ambiente é exclusivamente local
- Pipeline de CI/CD — os workflows já existentes (`backend-feature-pr.yml`,
  `backend-deploy-hom.yml`, `backend-deploy-prod.yml`) continuam usando
  AWS real, sem interação com o ambiente local
- Testes automatizados (unitários/componente) rodarem contra os
  serviços locais — eles já usam mocks/`WebApplicationFactory` (ver
  FEAT-03) e continuam assim; o ambiente local desta feature é para uso
  manual/exploratório do desenvolvedor, não para a esteira de testes
- Dados de seed realistas — o script de inicialização cria apenas a
  estrutura (tabela, parâmetros, user pool) vazia; popular dados de
  teste é decisão operacional posterior
- Réplica de Kong ou de qualquer camada de API Gateway localmente — o
  backend local roda direto via `dotnet run`, sem simular API Gateway
