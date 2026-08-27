# FEAT-29: Testes integrados multiambiente (local/hom/prod) + gate de CI/CD

## Objetivo

Criar uma suíte de **testes integrados** para o backend — testes que
exercitam a API real (não mockada) contra os serviços de que ela
depende de verdade (Cognito, DynamoDB) — capaz de rodar em três
contextos: **local** (contra o binário Native AOT rodando dentro da
mesma imagem de base usada pela Lambda real, para pegar erros
específicos de AOT antes de chegar na AWS), **homologação** (contra
`https://api-hom.jrnexpenses.com`, já implantada) e **produção**
(contra `https://api.jrnexpenses.com`, já implantada). A partir desta
feature, testes integrados passam a fazer parte da esteira de CI/CD:
rodam automaticamente após todo deploy de homologação, e **nenhum
deploy de produção acontece sem que o teste integrado de homologação
daquele mesmo commit tenha passado**. Além disso, deve existir uma
forma de disparar manualmente o teste integrado de produção a partir
do GitHub, sem precisar rodar o restante da pipeline.

## Contexto

Hoje o backend tem duas camadas de teste automatizado:
- `GastosApp.UnitTests` — testa classes isoladas (Handlers, `Result`,
  `CognitoAuthService`) com dublês.
- `GastosApp.ComponentTests` (FEAT-03) — sobe a API inteira em memória
  via `WebApplicationFactory`, mas com Cognito/repositórios
  substituídos por dublês NSubstitute. Válido para regra de negócio e
  contrato HTTP, mas **não** exercita a integração real com AWS nem o
  binário publicado (Native AOT).
- `GastosApp.IntegrationTests` — existe como projeto vazio desde a
  FEAT-03 (`UnitTest1.cs` sem conteúdo), com sua destinação registrada
  como "decisão futura do time". Esta feature é essa decisão: o
  projeto passa a ser usado, preenchido com os primeiros testes reais.

O deploy da API é feito hoje via binário **Native AOT** (runtime Lambda
`provided.al2023`, ver `backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/`),
compilado dentro de um container `amazonlinux:2023`
(`backend/infra/lambda/Dockerfile.build`) — mesma base do runtime da
Lambda, mas isso garante só que o *binário compila e roda* nessa base,
não que o *comportamento da aplicação sob AOT* (reflection não
suportada, trimming, `services.Configure<T>()` já quebrado
silenciosamente sob AOT — ver débito documentado em
`backend/infra/CLAUDE.md`, "Gotchas conhecidos") esteja correto. Hoje
esse tipo de erro só é descoberto depois do deploy em homologação ou
produção.

A esteira de CI/CD atual (FEAT-14) tem três workflows:
`backend-deploy-hom.yml` (deploy a cada push em `develop` + rascunho de
release), `backend-deploy-prod.yml` (deploy disparado por GitHub
Release `backend-v*`) e `backend-feature-pr.yml`. Nenhum deles executa
teste contra a API real depois do deploy — o gate de qualidade hoje é
só `dotnet build` + `dotnet test` (unitários + componente) antes de
publicar.

### Decisões já confirmadas com o usuário para esta feature

- **Ambiente local roda a imagem real da Lambda, não só `dotnet run`**:
  o teste local sobe o binário Native AOT publicado dentro da mesma
  base de execução da Lambda (`public.ecr.aws/lambda/provided:al2023`
  + Lambda Runtime Interface Emulator — RIE), apontando para
  LocalStack + cognito-local (mesma infra da FEAT-18), para pegar erro
  de AOT antes de qualquer push.
- **Isolamento de dados em hom/prod (ambientes 100% reais) via conta de
  teste dedicada**: cada execução da suíte cria seu próprio usuário via
  `POST /auth/register`, confirma via `AdminConfirmSignUp` (API do
  Cognito que confirma sem precisar do código por e-mail — evita
  depender de caixa de entrada real), roda os testes, e ao final remove
  tudo que criou (`AdminDeleteUser` no Cognito + exclusão direta dos
  itens no DynamoDB, já que não existe endpoint de exclusão de conta) —
  independentemente de sucesso ou falha dos testes.
- **Gate de produção via checagem explícita no próprio workflow de
  deploy de prod**: `backend-deploy-prod.yml`, ao disparar (release
  publicada ou `workflow_dispatch`), verifica via API do GitHub que
  existe uma execução **bem-sucedida** de `backend-deploy-hom.yml`
  (incluindo o job de teste integrado) para o **mesmo commit** apontado
  pela tag da release — não apenas que a release existe. Sem isso, o
  workflow falha antes de buildar/deployar.
- **Cobertura desta feature**: entrega a infraestrutura completa
  (projeto de testes, execução local via Docker/AOT, gates em
  hom/prod, workflow avulso de teste em prod) funcionando ponta a
  ponta, com o módulo **Auth** (`register`/`login`) como primeiro
  módulo coberto. Os demais módulos existentes (categorias,
  transações, membros, resumo, relatórios, export CSV, perfil) **não**
  ganham teste integrado nesta feature — viram débito técnico
  registrado em `backend/docs/backlog.md`, a preencher
  incrementalmente. Dali em diante, porém, passa a valer como regra:
  todo **endpoint novo** exige teste integrado como parte da definição
  de pronto, junto com o teste de componente já exigido desde a FEAT-03
  (ver "Requisitos" abaixo).

## Requisitos de negócio / restrições

- **Um único projeto, três alvos**: a mesma suíte
  (`GastosApp.IntegrationTests`) roda contra os três ambientes,
  parametrizada pela URL base (local via RIE, hom, prod) — não três
  suítes distintas. Regra de negócio validada é a mesma nos três
  lugares; o que muda é o alvo.
- **Local exercita o binário real, não código-fonte via `dotnet run`**:
  os testes locais precisam invocar a aplicação da mesma forma que o
  API Gateway invocaria a Lambda real (formato de evento HTTP API,
  através do Lambda Runtime Interface Emulator rodando na imagem base
  `provided.al2023`), não diretamente via Kestrel — é esse caminho que
  expõe erro de AOT que só se manifesta no binário publicado.
- **Sem dependência de rede/credencial AWS real para o ambiente
  local**: o container local aponta para LocalStack + cognito-local
  (mesmos endpoints/õ princípios da FEAT-18), mantendo custo zero e
  isolamento total de hom/prod — nenhuma chamada à conta AWS real
  acontece ao rodar a suíte localmente.
- **Um único comando local**: seguindo o princípio já estabelecido na
  FEAT-18 ("um comando para subir tudo"), deve existir um único script
  que builda a imagem AOT, sobe LocalStack/cognito-local (se não
  estiverem no ar) e roda a suíte contra o container, sem passos
  manuais adicionais.
- **Isolamento de dados em hom/prod**: nenhuma execução da suíte pode
  deixar rastro permanente (usuário, conta, categoria, transação) nos
  ambientes reais. Toda execução cria sua própria conta de teste e a
  remove ao final, mesmo se algum teste falhar no meio do caminho
  (limpeza roda sempre, tipo `finally`/`IAsyncLifetime.DisposeAsync`).
- **Credenciais de administração do Cognito e limpeza direta do
  DynamoDB exigem permissão IAM nova**: a role de CI/CD
  (`gastosapp-backend-cicd`, `backend/infra/terraform/cicd/`) hoje não
  tem permissão para `cognito-idp:AdminConfirmSignUp`,
  `cognito-idp:AdminDeleteUser` nem para excluir itens do DynamoDB
  diretamente. Adicionar essas permissões é **infraestrutura AWS com
  implicação de segurança** — precisa de aprovação explícita do
  usuário antes de qualquer `terraform apply`, tratada em detalhe no
  `plan.md`.
- **Gate de homologação**: o job de teste integrado roda **depois** do
  deploy de hom ter sido publicado com sucesso e **antes** do rascunho
  de release (`backend-vX.Y.Z`) ser criado/atualizado em
  `backend-deploy-hom.yml`. Se o teste falhar, nenhum rascunho novo é
  criado/atualizado — o rascunho pendente anterior (se houver)
  permanece como estava.
- **Gate de produção**: `backend-deploy-prod.yml` (disparado por
  release publicada ou `workflow_dispatch`) verifica, antes de
  `quality`/`deploy`, que existe uma execução bem-sucedida de
  `backend-deploy-hom.yml` — job de teste integrado incluído — para o
  commit exato apontado pela tag da release (ou pelo input `tag` do
  `workflow_dispatch`). Sem essa verificação passar, o workflow falha
  sem buildar nem deployar nada.
- **Teste de produção sob demanda, isolado da pipeline de deploy**: um
  workflow novo, disparável manualmente pela aba Actions do GitHub
  (`workflow_dispatch`, sem gatilho automático), roda a suíte só
  contra `https://api.jrnexpenses.com`, sem tocar em build/deploy —
  permite validar produção a qualquer momento sem re-executar toda a
  pipeline.
- **Nenhuma mudança de contrato de API**: esta feature não adiciona,
  remove nem altera nenhum endpoint ou comportamento observável da
  API — é só infraestrutura de teste e pipeline.
- **Definição de pronto, dali em diante**: toda spec de backend que
  introduzir endpoint novo passa a exigir, como critério de aceite,
  teste integrado cobrindo pelo menos o fluxo de sucesso (além do
  teste de componente já exigido desde a FEAT-03) — regra a ser
  adicionada em `backend/docs/constitution.md`.

## User stories

### US1 — Erro de Native AOT é detectado localmente, antes do push
**Given** um desenvolvedor faz uma alteração no backend que quebra sob
Native AOT (ex.: uso de reflection não suportada, `services.Configure<T>()`
não lido corretamente)
**When** ele roda a suíte de testes integrados localmente (contra o
binário publicado rodando na imagem base da Lambda, via Runtime
Interface Emulator)
**Then** o teste falha localmente, evidenciando o erro específico de
AOT, antes de qualquer push ou deploy real

### US2 — Gate automático pós-deploy em homologação
**Given** um push em `develop` dispara `backend-deploy-hom.yml` e o
deploy da Lambda de homologação é publicado com sucesso
**When** o job de teste integrado roda em seguida contra
`https://api-hom.jrnexpenses.com`, usando uma conta de teste dedicada
criada e confirmada para essa execução
**Then**, se os testes passarem, o rascunho de release
(`backend-vX.Y.Z`) é criado/atualizado normalmente, e a conta de teste
é removida (Cognito + DynamoDB) ao final; se falharem, nenhum rascunho
novo é criado/atualizado, e a conta de teste também é removida

### US3 — Deploy de produção bloqueado sem teste de hom bem-sucedido
**Given** uma GitHub Release `backend-vX.Y.Z` é publicada, apontando
para um commit específico
**When** `backend-deploy-prod.yml` dispara
**Then**, antes de buildar ou deployar qualquer coisa, o workflow
verifica (via API do GitHub) se existe uma execução **bem-sucedida**
de `backend-deploy-hom.yml` (incluindo o job de teste integrado) para
esse mesmo commit; se não existir (ou tiver falhado), o workflow falha
imediatamente, sem tocar na Lambda de produção

### US4 — Teste integrado de produção sob demanda
**Given** o backend já está publicado em produção
**When** alguém com acesso ao repositório abre a aba Actions no GitHub
e dispara manualmente o novo workflow de teste integrado de produção
**Then** a suíte roda contra `https://api.jrnexpenses.com`, usando uma
conta de teste dedicada criada/confirmada/removida só para essa
execução, sem exigir rodar build ou deploy

### US5 — Definição de pronto passa a exigir teste integrado (endpoints novos)
**Given** uma spec futura de backend introduz um endpoint novo
**When** a feature é implementada
**Then** ela só é considerada concluída com teste de componente **e**
teste integrado cobrindo pelo menos o fluxo de sucesso do novo
endpoint, executados com sucesso localmente antes do merge

## Comportamento observável da pipeline

- `backend-deploy-hom.yml` ganha um job **`integration-tests`** entre
  `deploy` e `draft-release` — só roda se `deploy` for bem-sucedido, e
  `draft-release` passa a depender também dele.
- `backend-deploy-prod.yml` ganha um job/etapa **antes** de `quality`
  que falha o workflow se não encontrar uma execução bem-sucedida de
  `integration-tests` de `backend-deploy-hom.yml` para o commit da
  release/tag sendo deployada.
- Novo workflow **`backend-integration-tests-prod.yml`**, gatilho
  único `workflow_dispatch`, sem gatilho automático — roda a suíte
  contra produção isoladamente, visível e disparável pela aba Actions
  do GitHub.
- Nenhum workflow existente do frontend é afetado.

## Critérios de aceite

- [x] `GastosApp.IntegrationTests` (hoje vazio) passa a conter testes
      reais do módulo Auth (`register` + `login`, sucesso e pelo menos
      um erro mapeado), parametrizados por URL base de ambiente
- [x] Existe um único comando/script local que builda a imagem Native
      AOT, sobe (ou reaproveita) LocalStack + cognito-local, e roda a
      suíte contra o binário publicado via Lambda Runtime Interface
      Emulator — sem exigir credencial/rede AWS real (`run-local.sh`,
      validado ao vivo de ponta a ponta: os 3 testes de Auth passando
      contra o container)
- [ ] `backend-deploy-hom.yml` roda a suíte contra
      `https://api-hom.jrnexpenses.com` depois do deploy e antes do
      rascunho de release; falha no teste impede novo rascunho —
      **job implementado, não validado ao vivo ainda** (depende de
      push real em `develop` após merge do PR desta feature e da
      permissão IAM da Fase 5 estar aplicada)
- [ ] `backend-deploy-prod.yml` verifica, antes de buildar/deployar,
      que o teste integrado de hom passou para o commit da release
      sendo publicada; falha a verificação impede build/deploy —
      **job implementado, não validado ao vivo ainda** (mesma
      dependência acima)
- [ ] Workflow novo `backend-integration-tests-prod.yml`
      (`workflow_dispatch`) roda a suíte contra
      `https://api.jrnexpenses.com` isoladamente, sem build/deploy —
      **workflow criado, não disparado ainda**
- [ ] Toda execução da suíte contra hom/prod cria uma conta de teste
      dedicada (confirmada via `AdminConfirmSignUp`) e a remove ao
      final (Cognito + DynamoDB), mesmo em caso de falha nos testes —
      **implementado e validado contra o ambiente local (LocalStack +
      cognito-local); ainda não validado contra Cognito/DynamoDB reais
      de hom/prod**, que dependem da permissão IAM da Fase 5
- [ ] Permissões IAM novas na role `gastosapp-backend-cicd`
      (`AdminConfirmSignUp`, `AdminDeleteUser`, exclusão de itens
      DynamoDB) aplicadas via Terraform só após aprovação explícita do
      usuário — **`.tf` escrito e validado (`fmt`/`validate`), mas
      `terraform apply` ainda não rodou** (sem credenciais AWS válidas
      nesta sessão — SSO expirado); aprovação de princípio já dada
      pelo usuário no `/plan`, falta rodar `terraform plan`/`apply` de
      fato
- [x] `backend/docs/constitution.md` atualizado com a regra de teste
      integrado obrigatório (junto com componente) para endpoints
      novos daqui pra frente
- [x] `backend/docs/backlog.md` ganha os itens de débito técnico para
      os módulos ainda sem teste integrado (categorias, transações,
      membros, resumo, relatórios, export CSV, perfil)
- [x] `backend/infra/CLAUDE.md` (seção CI/CD) e `backend/CLAUDE.md`
      (estrutura de projetos) atualizados refletindo o novo uso de
      `GastosApp.IntegrationTests` e os novos jobs/workflow
- [x] Nenhum contrato de API existente foi alterado
- [x] `dotnet test GastosApp.sln` continua rodando unitários e
      componente normalmente, sem exigir Docker/rede — a suíte
      integrada roda separada (script próprio), não faz parte do
      `dotnet test` padrão (469 unitários + 205 componente passando,
      0 falhas)

## Fora do escopo

- Teste integrado dos módulos além de Auth (categorias, transações,
  membros, resumo, relatórios, export, perfil) — vira débito técnico
  no backlog, a preencher incrementalmente
- Mudança de contrato de API, de regra de negócio ou de infraestrutura
  de produção/homologação já provisionada (tabelas, Cognito, API
  Gateway, domínio) — esta feature só adiciona testes e pipeline
- Testes de carga/performance — escopo é correção funcional +
  compatibilidade AOT, não throughput/latência
- Revisão do fluxo de "resolução idempotente" de conta no primeiro
  login (mencionado em `backend/docs/backlog.md`, FEAT-19) — fora de
  escopo aqui
