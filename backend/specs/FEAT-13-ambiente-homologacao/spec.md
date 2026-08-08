# FEAT-13: Ambiente de homologação do backend

## Objetivo

Criar um ambiente de **homologação** completo para o backend, isolado do
ambiente de produção existente, duplicando as peças de infraestrutura já
provisionadas (banco de dados, autenticação, configuração, compute e
API pública), de forma que seja possível validar mudanças de backend
antes de irem para produção, sem risco de afetar dados ou usuários reais.
A API de homologação deve responder em `https://api-hom.jrnexpenses.com`,
mantendo o custo o mais próximo possível de zero.

## Contexto

Até aqui (FEAT-09, FEAT-10, FEAT-11, FEAT-12), toda a infraestrutura
provisionada em `backend/infra/terraform/` — tabela DynamoDB, Cognito
User Pool + App Client, parâmetros no Parameter Store, Lambda .NET
Native AOT, API Gateway HTTP API e o domínio customizado
`api.jrnexpenses.com` — corresponde exclusivamente ao ambiente de
**produção**. Não existe hoje nenhum ambiente separado para testar
mudanças de backend antes de expô-las publicamente.

Esta feature duplica essas peças para um novo ambiente de homologação,
mantendo produção intacta e sem alteração de comportamento. A hosted
zone `jrnexpenses.com.` (gerenciada pelo Terraform do frontend, FEAT-07)
já foi criada prevendo esse uso futuro — o novo record
`api-hom.jrnexpenses.com` é criado dentro dela, seguindo o mesmo padrão
de referência (sem duplicar ou gerenciar a zona a partir do backend) já
usado por `api.jrnexpenses.com` na FEAT-12.

Precedentes diretos de estilo/abordagem: `backend/specs/FEAT-09-terraform-cognito-parameter-store/`
(Cognito e Parameter Store sob Terraform), `backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/`
(Lambda + API Gateway) e `backend/specs/FEAT-12-terraform-dominio-customizado-api/`
(domínio customizado + ACM + DNS). A diferença central é que ali os
recursos já existiam e foram *importados*; aqui os recursos do ambiente
de homologação são **novos** (criados do zero via Terraform, não
importados).

## Requisitos de negócio / restrições

- **Isolamento total de produção**: o ambiente de homologação deve ter
  seus próprios dados (tabela DynamoDB própria), seus próprios usuários
  e credenciais (Cognito User Pool + App Client próprios), sua própria
  configuração (Parameter Store com prefixo distinto do de produção) e
  seu próprio compute (Lambda + API Gateway próprios). Nenhuma operação
  em homologação pode ler, escrever ou afetar de qualquer forma os
  recursos de produção, e vice-versa.
- **Mesmo contrato de API**: a API de homologação expõe exatamente o
  mesmo contrato já documentado em `backend/docs/openapi.json` — mesmos
  endpoints, request/response e status codes de produção. Esta feature
  não adiciona, remove nem altera nenhum endpoint ou comportamento de
  negócio; duplica apenas a infraestrutura que hospeda o mesmo código.
- **Rota pública**: a API de homologação deve responder em
  `https://api-hom.jrnexpenses.com`, seguindo o mesmo padrão de domínio
  customizado + certificado ACM + record DNS já usado em produção
  (FEAT-12), dentro da hosted zone `jrnexpenses.com.` já existente
  (gerenciada pelo Terraform do frontend) — sem duplicar ou gerenciar
  essa zona a partir do backend.
- **Custo baixo (idealmente free tier)**: mesmas restrições de custo já
  vigentes em `backend/docs/architecture.md` e no `/CLAUDE.md` raiz
  aplicam-se a homologação:
  - DynamoDB em modo `PAY_PER_REQUEST` (sem custo fixo, free tier
    permanente cobre volume de uso de teste).
  - Lambda dentro do free tier permanente (mesmo tamanho/timeout de
    produção: 256MB/10s).
  - Cognito User Pool dentro do free tier (até 50 MAU).
  - Certificado ACM público: gratuito.
  - Record DNS adicional na zona já existente: sem custo incremental
    (zona já é cobrada pelo frontend).
  - API Gateway HTTP API: mesmo custo por requisição de produção
    (~US$1/milhão, fora do free tier de 12 meses desta conta) —
    desprezível dado o volume esperado de uso em homologação.
  - CloudWatch Logs com a mesma retenção de produção (14 dias), para
    não acumular custo de armazenamento indefinido.
- **Nenhuma ação na conta AWS sem autorização prévia explícita do
  usuário** — vale tanto para o desenho da estratégia (`plan.md`) quanto
  para qualquer execução futura (`terraform plan`/`terraform apply`).
  Nenhum comando que possa criar, alterar ou destruir recursos reais
  roda de forma autônoma.
- **Nenhuma mudança de comportamento observável em produção**: a API em
  `https://api.jrnexpenses.com` e a URL padrão do API Gateway de
  produção continuam respondendo exatamente como hoje, sem nenhuma
  alteração de configuração, dados ou disponibilidade.
- **IaC exclusivamente em Terraform**, seguindo a mesma convenção já
  usada em `backend/infra/terraform/` (ver `backend/infra/CLAUDE.md`) —
  a forma de organizar essa duplicação (novos arquivos na mesma
  configuração, Terraform workspaces, ou uma configuração separada) é
  uma decisão técnica do `plan.md`, não desta spec.
- **Pequena mudança de código é necessária para isolamento real**: o
  caminho lido no Parameter Store hoje é fixo no código
  (`AwsParameterStoreExtensions.cs`, `/GastosApp/`), não vem de
  configuração. Sem torná-lo configurável via variável de ambiente, a
  Lambda de homologação acabaria lendo os parâmetros de Cognito de
  **produção**, quebrando o isolamento exigido pela US2. Esta feature
  inclui essa pequena mudança em `GastosApp.Infrastructure` (o caminho
  passa a ser parametrizável, com default igual ao valor atual) — sem
  nenhuma mudança de contrato de API ou de comportamento em produção.
  Detalhes técnicos em `plan.md`.

## User Stories

**US1 — Dados isolados em homologação**
- Given o ambiente de homologação provisionado
- When uma despesa é criada através da API de homologação
- Then ela é persistida numa tabela DynamoDB própria de homologação, e
  não aparece em nenhuma consulta feita contra a API de produção

**US2 — Autenticação isolada em homologação**
- Given o ambiente de homologação provisionado
- When um usuário se registra e faz login através da API de
  homologação
- Then a conta é criada num Cognito User Pool próprio de homologação, e
  as credenciais não funcionam contra a API de produção (e vice-versa)

**US3 — API de homologação acessível pelo domínio customizado**
- Given o ambiente de homologação provisionado, com certificado ACM
  válido para `api-hom.jrnexpenses.com`
- When uma requisição é feita para `https://api-hom.jrnexpenses.com`
- Then ela é respondida pela Lambda/API Gateway de homologação, com o
  mesmo contrato de API de produção (mesmos endpoints, mesmos status
  codes, incluindo `401` sem token válido)

**US4 — Produção sem regressão**
- Given o ambiente de homologação provisionado
- When requisições continuam sendo feitas para
  `https://api.jrnexpenses.com` (produção)
- Then o comportamento observado é idêntico ao anterior à esta feature
  — mesmos dados, mesmos usuários, mesma disponibilidade

**US5 — Custo controlado**
- Given os recursos de homologação provisionados
- When o uso em homologação se mantém em volume de teste (não
  produção)
- Then o custo incremental gerado fica dentro do free tier permanente
  da AWS (DynamoDB, Lambda, Cognito) ou é desprezível (API Gateway,
  ACM, DNS), sem nenhum recurso cobrado por hora ligada

**US6 — Nenhuma execução sem aprovação explícita**
- Given qualquer comando que possa criar, alterar ou destruir um
  recurso AWS (`terraform plan`, `terraform apply`)
- When esse comando está prestes a ser executado
- Then o usuário é consultado e precisa aprovar explicitamente antes da
  execução — nenhum comando desse tipo roda de forma autônoma

## Contratos observáveis

Nenhum novo endpoint, campo ou status code é introduzido — o contrato
de wire permanece exatamente o documentado em
`backend/docs/openapi.json`. A única diferença observável é a **base
URL**: a mesma API passa a responder também em
`https://api-hom.jrnexpenses.com`, além de `https://api.jrnexpenses.com`
(produção, inalterado). Não há necessidade de regenerar o
`openapi.json` nesta feature. Há uma pequena mudança de código interna
(caminho do Parameter Store configurável, ver seção acima), mas ela não
altera nenhum comportamento observável da API em produção nem introduz
nenhuma mudança de contrato.

## Critérios de aceite

- [x] Existe uma tabela DynamoDB própria para homologação, com o mesmo
      modelo de dados (PK/SK, GSI1, GSI2) da tabela de produção, vazia
      no início e sem nenhum dado de produção — `GastosApp-Hom`
      provisionada e validada (isolamento confirmado manualmente)
- [x] Existe um Cognito User Pool + App Client próprios de
      homologação, com a mesma configuração de política de senha e
      fluxos de autenticação de produção — `user-pool-gastos-app-hom`
      provisionado, fluxo de registro/login validado
- [x] Existem parâmetros próprios de homologação no Parameter Store,
      em um prefixo distinto do de produção (`/GastosApp/Hom/...` vs.
      `/GastosApp/...`), sem colidir com os parâmetros de produção
- [x] Existe uma Lambda + API Gateway HTTP API próprios de
      homologação, publicando o mesmo código/contrato da API —
      `gastos-app-api-hom` provisionada e validada
- [x] `https://api-hom.jrnexpenses.com` responde com o mesmo
      comportamento de `https://api.jrnexpenses.com` (incluindo `401`
      sem token válido), usando o certificado ACM correspondente —
      validado manualmente
- [x] `https://api.jrnexpenses.com` (produção) continua respondendo
      exatamente como antes desta feature, sem nenhuma regressão —
      validado manualmente (`401` sem token, 16 itens da tabela
      inalterados durante todo o processo)
- [x] Nenhum recurso de homologação provisionado gera custo fixo por
      hora ligada; `terraform plan`/`apply` só executam com aprovação
      explícita do usuário no momento da execução
- [x] `backend/infra/CLAUDE.md` e `backend/infra/terraform/README.md`
      atualizados para refletir a existência do ambiente de
      homologação e como ele se relaciona com produção

## Status

**Implementado, provisionado e validado.**

- Mudança de código concluída e testada: caminho do Parameter Store
  configurável via `ParameterStore:Path`/`ParameterStore__Path`
  (`AwsParameterStoreExtensions.cs`, `Program.cs`).
- **Bug encontrado durante a validação e corrigido**: o nome da tabela
  DynamoDB (`DynamoDbOptions.TableName`) era lido via
  `services.Configure<DynamoDbOptions>(IConfiguration)`, que usa
  reflection e **falha silenciosamente sob Native AOT** — a Lambda de
  hom sempre lia o default hardcoded (`"GastosApp"`, a tabela de
  produção) e ignorava a variável de ambiente `DynamoDb__TableName`.
  Esse mesmo problema já tinha sido identificado e corrigido para
  `CognitoOptions` na FEAT-10, mas nunca replicado para
  `DynamoDbOptions` — nunca deu problema porque o default coincidia
  com o nome real da tabela de produção. Corrigido em
  `InfrastructureServiceCollectionExtensions.cs` (leitura manual, mesmo
  padrão já usado para Cognito). `dotnet build`/`dotnet test` (180
  testes) passando após a correção.
- `backend/infra/terraform/` reorganizado em `environments/prod/`
  (produção migrada — `terraform init -migrate-state`, sem recriar
  nenhum recurso, `terraform plan` final "No changes.") e
  `environments/hom/` (21 recursos novos provisionados via
  `terraform apply`, "No changes." no plan seguinte).
- Validação manual end-to-end em hom: `POST /auth/register` →
  `admin-confirm-sign-up` → `POST /auth/login` → `GET /auth/me` →
  `GET /expenses` (vazio) → `POST /expenses` → confirmado 1 item em
  `GastosApp-Hom` e 0 em `GastosApp` (produção) → `DELETE /expenses/{id}`
  → `admin-delete-user`, ambiente de teste limpo ao final.
- Produção validada sem regressão em todos os pontos do processo
  (`401` sem token, 16 itens na tabela `GastosApp` inalterados do
  início ao fim).
- Documentação (`backend/infra/CLAUDE.md`,
  `backend/infra/terraform/README.md`) atualizada.
- Todos os comandos que tocaram a conta AWS real (migração de state,
  `apply` em hom, `apply` em prod, remoção do state órfão) foram
  aprovados explicitamente pelo usuário no momento da execução,
  conforme US6.

## Fora do escopo

- Ambiente de homologação do **frontend** (hosting, DNS do frontend) —
  a demanda desta feature é explicitamente restrita às peças de
  backend; um eventual `frontend-hom.jrnexpenses.com` é feature futura
  separada no contexto frontend
- Pipeline de CI/CD para deploy automático em homologação — o deploy
  (build da Lambda + `terraform apply`) continua manual, a partir da
  máquina do usuário, com aprovação passo a passo, igual ao fluxo já
  usado em produção (FEAT-10). Há intenção futura de criar uma stack de
  CI/CD própria, mas isso é feature separada, não parte desta spec
- Dados de seed ou massa de teste em homologação — a tabela é
  provisionada vazia; popular dados de teste é decisão operacional
  posterior, fora desta spec
- Qualquer mudança de contrato, regra de negócio ou comportamento da
  API além de expô-la também em homologação — nenhuma mudança de
  endpoint, validação ou autenticação é escopo desta feature
- Definição fina de política de CORS / origens permitidas em
  homologação (não há frontend de homologação ainda) — tratada como
  detalhe técnico no `plan.md`, com a opção mais restritiva e de menor
  risco por padrão
- Promoção/sincronização de dados entre homologação e produção (ex.:
  copiar dados de produção para homologação) — não faz parte desta
  feature