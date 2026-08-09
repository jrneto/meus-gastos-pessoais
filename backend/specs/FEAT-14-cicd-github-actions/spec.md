# FEAT-14: Esteira de CI/CD (GitHub Actions) para o backend

## Objetivo

Automatizar o deploy do backend em homologação e produção via GitHub
Actions, substituindo o processo manual atual (`./infra/lambda/build.sh`
+ `terraform apply` executados à mão em cada ambiente). O pipeline deve
garantir qualidade (nenhum deploy com build/testes quebrados), custo
zero, e replicar — adaptadas ao contexto backend — as mesmas regras já
validadas end-to-end no frontend: gate de qualidade antes de publicar,
deploy automático em homologação, deploy de produção atrelado a uma
GitHub Release, fluxo de branch por feature com PR automático
(feature → `develop` e `develop` → `main`) e release em rascunho
automática após cada deploy de homologação bem-sucedido.

## Contexto

Hoje existem dois ambientes de backend já provisionados via Terraform
(`backend/infra/terraform/environments/{prod,hom}/`,
`backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/` e
`backend/specs/FEAT-13-ambiente-homologacao/`):
`https://api.jrnexpenses.com` (produção) e
`https://api-hom.jrnexpenses.com` (homologação). O deploy do código
(build Native AOT via `infra/lambda/build.sh` + `terraform apply` para
atualizar a Lambda a partir do `function.zip` gerado) continua 100%
manual — essa feature ataca exatamente essa lacuna, sem alterar a
infraestrutura já provisionada (tabelas, Cognito, Parameter Store,
Lambda, API Gateway, domínio customizado).

O frontend já validou esse mesmo desenho end-to-end
(`frontend/specs/FEAT-09-cicd-github-actions/`,
`frontend/specs/FEAT-10-git-workflow-branch-pr/`,
`frontend/specs/FEAT-11-release-rascunho-automatica/`). Esta feature
replica as mesmas regras de negócio para o backend, num único ciclo
spec → plan → tasks, adaptando os detalhes técnicos ao stack .NET/Lambda
(gate de qualidade é `dotnet build` + `dotnet test`, não lint/vitest; o
artefato publicado é `function.zip`, não um bundle estático em S3).

O repositório (`jrneto/meus-gastos-pessoais`) é um monorepo privado
compartilhado com o frontend — os workflows desta feature devem disparar
apenas a partir de mudanças em `backend/**`, sem interferir nos
pipelines do frontend já existentes (`.github/workflows/frontend-*.yml`)
nem duplicar ações que já rodam para o par de branches `develop → main`
quando o gatilho vier do lado do frontend.

## Requisitos de negócio / restrições

- **Qualidade antes de publicar**: nenhum deploy (hom ou prod) pode
  acontecer se o build falhar ou se qualquer teste falhar. Aplica a
  mesma regra já vigente em `backend/docs/constitution.md` ("nenhuma
  feature é considerada concluída com testes falhando") ao pipeline:
  `dotnet build` e `dotnet test` (`GastosApp.sln`) rodam antes de
  qualquer publicação, e uma falha em qualquer etapa interrompe o
  pipeline sem tocar em Lambda/API Gateway.
- **Deploy automático em homologação**: uma alteração em `backend/**`
  integrada a `develop` dispara o pipeline; se as verificações de
  qualidade passarem, o novo build (Native AOT) é publicado
  automaticamente na Lambda de homologação
  (`https://api-hom.jrnexpenses.com`), sem intervenção manual.
- **Deploy em produção atrelado a uma release do GitHub**: a publicação
  em `https://api.jrnexpenses.com` acontece a partir de uma **GitHub
  Release** com tag de versão semântica, não a cada push. O pipeline
  builda o código exatamente na tag da release e publica esse artefato
  em produção — a forma exata de disparo e de convivência com o
  gatilho de release do frontend (ex.: prefixo de tag por contexto,
  paths filtrados) é decisão de `plan.md`.
- **Rastreabilidade de versão publicada**: tanto em hom quanto em
  prod, deve ser possível identificar externamente qual versão do
  backend está publicada (ex.: um endpoint/health-check ou header de
  resposta expondo a versão), com uma forma de relacionar esse
  identificador à release/commit correspondente no GitHub — mesmo
  princípio de rastreabilidade da FEAT-09 do frontend, adaptado a uma
  API sem interface visual própria. Formato exato é decisão de
  `plan.md`.
- **Custo zero**:
  - Nenhum runner self-hosted, nenhuma compra de minutos extras de
    Actions — o pipeline deve operar dentro da mesma cota gratuita de
    2.000 min/mês (plano GitHub Free, repositório privado) já
    compartilhada com o frontend.
  - Nenhum novo recurso AWS com custo fixo por hora/instância ligada.
    A autenticação do pipeline na AWS (para atualizar a Lambda) deve
    usar **OIDC** (GitHub Actions → IAM Role assumida via
    `aws-actions/configure-aws-credentials`), sem chave de acesso de
    longa duração armazenada em secret — reaproveitando o mesmo padrão
    (e, se possível, avaliando reaproveitar o OIDC Provider já criado
    para o frontend, decisão de `plan.md`) já validado na FEAT-09 do
    frontend.
  - A criação de qualquer recurso AWS novo que a esteira exija (IAM
    Role, e o OIDC Provider caso não seja reaproveitado) segue a mesma
    regra já vigente (`/CLAUDE.md` raiz, `backend/infra/CLAUDE.md`):
    **exige aprovação explícita do usuário antes de qualquer
    criação/alteração real**, nunca provisionado de forma autônoma.
- **Isolamento entre ambientes preservado**: o pipeline nunca publica o
  build de homologação na Lambda/API Gateway de produção nem
  vice-versa — mesma garantia de isolamento já estabelecida na
  FEAT-13.
- **Nenhuma execução destrutiva/real sem aprovação explícita**: a
  criação de qualquer recurso AWS necessário para a esteira (IAM Role,
  OIDC Provider) segue a mesma regra já usada nas specs de infra do
  backend e do frontend — nenhum `terraform apply`/comando equivalente
  roda de forma autônoma sem aprovação prévia.
- **Nascimento da branch**: toda feature em Fluxo Completo do backend
  nasce numa branch a partir de `develop`, criada já no `/specify`
  (antes de qualquer código), nomeada exatamente igual à pasta criada
  em `backend/specs/` — convenção já documentada no `/CLAUDE.md` raiz,
  agora automatizada via o gate de qualidade abaixo.
- **PR automático branch → `develop`**: a cada push na branch da
  feature que altere `backend/**`, o mesmo gate de qualidade usado no
  deploy de homologação (`dotnet build` + `dotnet test`) roda; se
  passar, um PR da branch para `develop` é aberto automaticamente —
  **só se ainda não existir um PR aberto** para esse par de branches
  (idempotente: pushes seguintes atualizam o PR já existente, nunca
  criam um segundo) — mesma regra da FEAT-10 do frontend.
- **PR automático `develop` → `main`**: quando o workflow de deploy de
  produção do backend termina com sucesso, um PR de `develop` para
  `main` é aberto automaticamente, se ainda não existir um aberto —
  convivendo com a automação equivalente já existente do lado do
  frontend (mesmo par de branches, gatilhos independentes; não deve
  duplicar PR se um já estiver aberto por qualquer um dos dois lados).
- **Merge sempre manual**: nenhum dos PRs automáticos (feature→develop,
  develop→main) faz merge sozinho — ambos ficam aguardando revisão e
  aprovação manual do usuário.
- **Release em rascunho automática após deploy de hom**: a cada deploy
  de homologação do backend bem-sucedido, uma GitHub Release em modo
  rascunho é criada ou atualizada, com tag sugerida (patch bump da
  última release publicada) e notas geradas automaticamente
  (`--generate-notes`) — mesma regra da FEAT-11 do frontend. Nunca
  publica sozinha; nunca acumula mais de um rascunho pendente.
  Convivência com o mecanismo de release do frontend (mesmo
  repositório, mesma numeração de tags) é decisão de `plan.md` — ex.:
  tags/prefixos distintos por contexto, ou um esquema de versionamento
  compartilhado; qualquer opção deve evitar colisão de tag entre
  backend e frontend.
- **Configuração de repositório necessária**: caso ainda não esteja
  habilitada, a opção "Allow GitHub Actions to create and approve pull
  requests" precisa estar ativa para esta automação funcionar — se já
  foi habilitada na FEAT-10 do frontend, nenhuma ação nova é
  necessária; qualquer mudança de configuração do repositório exige
  aprovação explícita do usuário.

## User Stories

**US1 — Deploy automático em homologação com gate de qualidade**
- Given uma alteração em `backend/**` integrada a `develop`
- When o pipeline roda `dotnet build` e `dotnet test`
- Then, se tudo passar, o novo artefato (`function.zip`) é publicado
  automaticamente na Lambda de homologação; se qualquer etapa falhar,
  nada é publicado e o pipeline reporta falha

**US2 — Deploy em produção a partir de uma release do GitHub**
- Given uma GitHub Release publicada com uma tag de versão semântica
  referente ao backend
- When o pipeline de produção do backend é disparado por essa release
- Then, após build/testes passarem para o código daquela tag, o
  artefato correspondente é publicado na Lambda de produção

**US3 — Rastreabilidade de versão publicada**
- Given um deploy bem-sucedido (hom ou prod)
- When alguém consulta a versão publicada da API (ex.: endpoint de
  health-check ou header de resposta)
- Then é possível identificar exatamente qual versão/commit está
  publicado em cada ambiente

**US4 — Isolamento entre ambientes**
- Given os pipelines de homologação e produção do backend configurados
- When um deploy de homologação roda
- Then ele nunca publica na Lambda/API Gateway de produção, e
  vice-versa

**US5 — Custo dentro da cota gratuita**
- Given o repositório privado no plano GitHub Free, já compartilhado
  com os pipelines do frontend
- When os pipelines de hom e prod do backend rodam ao longo de um mês
  de uso normal do projeto
- Then o consumo de minutos de Actions permanece dentro da cota
  gratuita (2.000 min/mês), sem uso de runner pago ou self-hosted

**US6 — Autenticação AWS sem credencial de longa duração**
- Given o pipeline precisa atualizar o código da Lambda
- When ele se autentica na AWS
- Then usa uma IAM Role assumida via OIDC (GitHub Actions), sem chave
  de acesso de longa duração armazenada em secret

**US7 — Nenhuma criação de recurso AWS sem aprovação**
- Given a necessidade de criar/reaproveitar IAM Role/OIDC Provider (ou
  qualquer outro recurso AWS novo) para a esteira funcionar
- When esse recurso está prestes a ser criado ou alterado
- Then o usuário é consultado e aprova explicitamente antes de
  qualquer `apply`/criação real

**US8 — PR automático para `develop` quando a feature passa nos testes**
- Given uma branch `FEAT-XX-nome` (backend) com mudanças em
  `backend/**`
- When um push acontece nessa branch e o gate de qualidade
  (`dotnet build` + `dotnet test`) passa
- Then um PR dessa branch para `develop` é aberto automaticamente,
  caso ainda não exista um aberto para esse par de branches

**US9 — Sem PR duplicado em pushes subsequentes**
- Given um PR já aberto de `FEAT-XX-nome` para `develop`
- When novos pushes (verdes) acontecem na mesma branch
- Then nenhum PR novo é criado — o PR existente recebe os commits
  novos

**US10 — PR automático para `main` após deploy de produção bem-sucedido**
- Given uma GitHub Release do backend publicada, disparando o deploy
  de produção
- When esse workflow termina com sucesso (build/testes + deploy OK)
- Then um PR de `develop` para `main` é aberto automaticamente, caso
  ainda não exista um aberto (independente de ter sido aberto por essa
  automação ou pela equivalente do frontend)

**US11 — Merge sempre manual**
- Given qualquer um dos PRs automáticos (feature→develop,
  develop→main)
- When ele é aberto
- Then nenhum merge automático acontece — o PR fica aguardando revisão
  e aprovação manual do usuário

**US12 — Rascunho de release criado após deploy de hom bem-sucedido**
- Given um push em `develop` que resulta num deploy de hom do backend
  bem-sucedido
- When o job de deploy termina com sucesso
- Then uma GitHub Release em modo rascunho referente ao backend é
  criada ou atualizada, com tag sugerida (patch bump da última release
  publicada do backend) e notas geradas automaticamente, sem colidir
  com o esquema de tags do frontend

**US13 — Rascunho nunca publica sozinho**
- Given um rascunho criado/atualizado por esta automação
- When ele é criado
- Then nenhuma publicação automática acontece — fica aguardando ação
  manual do usuário; publicá-lo dispara o deploy de produção do
  backend normalmente

## Contratos observáveis

Não há mudança nos endpoints de negócio já documentados em
`backend/docs/openapi.json`. As mudanças observáveis são:
- **Rastreabilidade de versão**: um novo endpoint ou header de resposta
  expondo a versão publicada da API (formato exato — endpoint
  dedicado vs. header em endpoint já existente — decisão de `plan.md`,
  já que altera a superfície de contrato observável e deve ser
  documentado em `openapi.json` se for um endpoint novo).
- **Novo comportamento de publicação**: pushes/releases do backend
  passam a resultar em deploy automático, onde antes exigiam comando
  manual.
- **Fluxo de Git/GitHub**: branches `FEAT-XX-nome` do backend passam a
  abrir PR automaticamente para `develop` (após gate de qualidade) e
  `develop` passa a abrir PR automaticamente para `main` (após deploy
  de produção bem-sucedido) — mesmo padrão observável já existente
  para o frontend.
- **Releases em rascunho**: a aba Releases do repositório passa a
  ganhar/atualizar um rascunho referente ao backend a cada deploy de
  hom bem-sucedido.

## Critérios de aceite

- [x] Pipeline de homologação do backend: dispara automaticamente a
      partir de uma alteração em `backend/**` integrada a `develop`,
      roda `dotnet build` + `dotnet test`, e só publica na Lambda de
      hom se tudo passar — validado ao vivo (após corrigir 2 bugs reais
      encontrados no processo, ver "Status")
- [x] Pipeline de produção do backend: dispara a partir de uma GitHub
      Release com tag semântica referente ao backend, roda
      build/testes para o código da tag, e só publica na Lambda de
      prod se tudo passar — validado ao vivo (release `backend-v0.0.1`
      publicada, deploy de prod passou 100%; achado colateral real
      documentado em "Status" — `frontend-deploy-prod.yml` também
      disparou por falta de guard, corrigido e remediado)
- [ ] Falha em qualquer etapa de qualidade (build/teste) impede o
      deploy, em ambos os pipelines — não exercitado com falha real de
      propósito (só falhas reais de infra, já corrigidas)
- [x] É possível identificar externamente a versão publicada em cada
      ambiente (hom e prod) após um deploy — confirmado nos dois:
      `curl https://api-hom.jrnexpenses.com/health` → `dev-<sha>`;
      `curl https://api.jrnexpenses.com/health` → `backend-v0.0.1`
- [x] Deploy de homologação nunca afeta a Lambda/API Gateway de
      produção, e vice-versa — confirmado via `curl`: hom respondendo
      com o build novo, prod ainda `404` (código antigo, intocado)
- [x] Autenticação do pipeline na AWS feita via OIDC (IAM Role
      assumida), sem access key de longa duração em secret — confirmado
      (job `deploy` passou por `configure-aws-credentials@v4` só com
      `CICD_ROLE_ARN`, sem nenhuma access key em secret)
- [x] Nenhum recurso AWS novo (IAM Role, OIDC Provider) foi
      criado/alterado sem aprovação explícita do usuário no momento da
      execução — Role criada manualmente pelo usuário, com aprovação e
      conferência do JSON a cada etapa
- [ ] Consumo de minutos de GitHub Actions, somado ao já usado pelo
      frontend, permanece dentro da cota gratuita de 2.000 min/mês
      (repositório privado) — plausível, mas sem volume de uso real
      acumulado ainda pra confirmar com dados
- [x] Nenhum novo recurso AWS com custo fixo por hora/instância ligada
      foi introduzido
- [~] Push numa branch `FEAT-XX-nome` (backend) que altera
      `backend/**` roda o gate de qualidade e, se passar, abre PR
      automático para `develop` — validado ao vivo (push nesta própria
      branch disparou `backend-feature-pr.yml`, `quality` verde, PR #7
      aberto automaticamente:
      https://github.com/jrneto/meus-gastos-pessoais/pull/7). Ainda não
      exercitado: idempotência em push subsequente (sem duplicar PR)
- [x] Deploy de produção do backend bem-sucedido abre automaticamente
      um PR `develop → main`, se ainda não existir um aberto — validado
      ao vivo (job `open-pr-main` abriu o PR após o deploy de prod)
- [~] Nenhum dos PRs automáticos (feature→develop, develop→main) faz
      merge sozinho — confirmado pro PR #7 (feature→develop, mergeado
      manualmente pelo usuário); PR develop→main aberto e aguardando
      revisão manual (ainda não mergeado no momento do registro)
- [x] Deploy de hom do backend bem-sucedido cria/atualiza um rascunho
      de release referente ao backend, com tag sugerida e notas
      geradas automaticamente, sem colidir com tags do frontend —
      validado ao vivo: rascunho `backend-v0.0.1` criado, com notas
      geradas, prefixo correto
- [x] Um deploy de hom subsequente, com rascunho já pendente, atualiza
      esse rascunho em vez de criar um novo — validado ao vivo (2º
      deploy de hom manteve um único rascunho `backend-v0.0.1`)
- [x] Nenhum rascunho é publicado automaticamente; publicá-lo
      manualmente dispara o deploy de produção do backend normalmente
      — validado ao vivo (rascunho ficou em draft até publicação
      manual; publicar disparou `backend-deploy-prod.yml` normalmente)

## Status

**Implementado e validado end-to-end em hom e prod**, incluindo o
fluxo de branch/PR e release em rascunho — mesmo padrão de validação ao
vivo (com bugs reais encontrados e corrigidos no processo) já visto nas
FEAT-09/10/11 do frontend.

- **Endpoint `/health`**: implementado
  (`GetHealthQuery`/`GetHealthQueryHandler`, `HealthEndpoints.cs`),
  cobre US3. Testes 100% verdes localmente: 123 testes unitários (3
  novos) + 61 de componente (2 novos) + 1 de integração —
  `dotnet build`/`dotnet test GastosApp.sln` limpos.
- **`backend/docs/openapi.json`**: **regenerado** — `GET /health`
  presente no contrato (`scripts/export-openapi.sh` rodou contra o
  Cognito/Parameter Store reais, sem simulação), sem alterar nenhum
  endpoint existente. `dotnet test GastosApp.sln` confirmado 100% verde
  depois (123 unitários + 61 componente + 1 integração).
- **`backend-feature-pr.yml`**: validado ao vivo — push da branch
  `FEAT-14-cicd-github-actions` disparou o workflow, `quality` passou,
  PR #7 aberto automaticamente
  (https://github.com/jrneto/meus-gastos-pessoais/pull/7), mergeado
  manualmente pelo usuário.
- **`backend-deploy-hom.yml`**: validado ao vivo, **2 bugs reais
  encontrados e corrigidos durante a validação** (mesmo padrão já visto
  no frontend, FEAT-09):
  1. **`build.sh` sem bit de execução** — `infra/lambda/build.sh` está
     `100644` no repo (sempre foi invocado como `bash
     infra/lambda/build.sh` no fluxo manual); o workflow chamava
     `./infra/lambda/build.sh` direto, que exige `+x` →
     `Permission denied`. Corrigido em `771970a` (direto em `develop`,
     bugfix pontual de pipeline, Modo Leve).
  2. **Sintaxe do `--environment` no AWS CLI** — `aws lambda
     update-function-configuration --environment "Variables=$json"`
     falhava com `ParamValidation`: o prefixo `Variables=` faz o CLI
     tentar interpretar como *shorthand syntax*, que não entende JSON
     aninhado. Corrigido pra passar JSON puro (`{"Variables": {...}}`)
     via `jq -n --argjson`, em `141f943`.
  Depois dos 2 fixes, reexecução **passou 100%**
  (`quality`/`deploy`/`draft-release` verdes), confirmado ao vivo:
  `curl https://api-hom.jrnexpenses.com/health` →
  `{"status":"ok","version":"dev-141f943","commitSha":"141f9439ed43fc8aa0cab0d24394a44441774792","environment":"hom"}`
  (`commitSha` bate com `git rev-parse HEAD`); rascunho `backend-v0.0.1`
  criado com notas geradas, prefixo correto (sem afetar rascunhos do
  frontend); `api.jrnexpenses.com/health` (prod) continua `404` —
  confirma isolamento.
- **`backend-deploy-prod.yml`**: validado ao vivo — rascunho
  `backend-v0.0.1` publicado manualmente pelo usuário, disparou o
  workflow, `quality`/`deploy`/`open-pr-main` verdes. Confirmado via
  `curl https://api.jrnexpenses.com/health` →
  `{"status":"ok","version":"backend-v0.0.1","commitSha":"777b3d130198580f8f70d41f8fd10ad41a974c2a","environment":"prod"}`.
  PR `develop → main` aberto automaticamente.
- **3º bug real encontrado e corrigido**: publicar a release
  `backend-v0.0.1` também disparou `frontend-deploy-prod.yml` — esse
  workflow não tinha nenhum filtro de tag (só o `draft-release` do
  `frontend-deploy-hom.yml` havia sido protegido, ver decisão 5 do
  `plan.md`). Resultado real: `jrnexpenses.com` foi republicado com
  `VITE_APP_VERSION=backend-v0.0.1` — bundle correto (mesmo código),
  mas com a versão exibida errada. Corrigido em `e4b6f13` (guard
  `if: !startsWith(github.event.release.tag_name, 'backend-v')` no job
  `quality`, espelhando o guard já existente em
  `backend-deploy-prod.yml`). **Remediado ao vivo**: usuário re-rodou
  ("Re-run all jobs") o run original da última release legítima do
  frontend (`v0.1.2`); confirmado via bundle real que
  `jrnexpenses.com` voltou a mostrar `v0.1.2`.
- **Ajuste em `frontend-deploy-hom.yml`** (filtro de prefixo
  `backend-v` no job `draft-release`): implementado, não exercitado
  diretamente (nenhum deploy de hom do frontend rodou desde então), mas
  os 2 rascunhos `backend-v0.0.1` (criado e depois atualizado pelo
  backend) nunca apareceram confundidos com nenhum rascunho do
  frontend, indício indireto de que o filtro funciona.
- **Terraform `cicd/`** (`backend/infra/terraform/cicd/`): código
  criado e validado sintaticamente (`terraform fmt`/`validate`).
  `terraform plan` executado com credenciais AWS reais — **falhou
  confirmando o mesmo gap do frontend**:
  `AccessDenied: iam:ListOpenIDConnectProviders`, mesmo com o perfil
  admin. Nenhum recurso foi criado (falha ocorreu no `plan`, antes de
  qualquer `apply`). JSON exato da trust policy e da policy inline
  documentado em `backend/infra/terraform/README.md`, seção "cicd/".
- **Role `gastosapp-backend-cicd` criada manualmente pelo usuário**:
  `arn:aws:iam::648443184523:role/gastosapp-backend-cicd`.
  `terraform import` tentado em seguida — **também bloqueado**
  (`AccessDenied` em `iam:GetRole`/`ListOpenIDConnectProviders`); fica
  fora do state, mesma situação da Role do frontend.
- **GitHub Environments `backend-hom`/`backend-prod`**: **criados pelo
  usuário**, com `CICD_ROLE_ARN`/`FUNCTION_NAME` cadastrados em cada um
  (`gastos-app-api-hom`/`gastos-app-api`). Permissão "Allow GitHub
  Actions to create and approve pull requests" confirmada já ativa
  (herdada da FEAT-10 do frontend).
- **Validado ao vivo**: US1, US2, US3, US4, US6, US7, US8 (parcial),
  US10, US12, US13. PR #7 (feature→develop) mergeado manualmente; PR
  develop→main aberto automaticamente, aguardando revisão/merge manual
  do usuário (US11 confirmado no primeiro, consistente no segundo).
- **3 bugs reais encontrados e corrigidos durante a validação** (mesmo
  padrão de rigor já visto nas FEAT-09/10/11 do frontend): (1)
  `build.sh` sem bit de execução, (2) sintaxe do `--environment` no AWS
  CLI, (3) `frontend-deploy-prod.yml` sem guard de tag, disparando com
  a release do backend — os três detalhados acima, os três remediados
  ao vivo.
- **Ainda pendente**: idempotência de US9/US10 em releases
  subsequentes (só uma release publicada até agora), teste de falha
  proposital no gate de qualidade, e volume real de uso pra confirmar a
  cota de minutos do Actions (US5) — mesmos gaps residuais já aceitos
  nas specs do frontend nesse estágio.

## Fora do escopo

- **Mudança na infraestrutura já provisionada** (tabelas, Cognito,
  Parameter Store, Lambda, API Gateway, domínio customizado) — esta
  feature só automatiza a publicação de código nela, não altera o que
  já existe (`backend/specs/FEAT-09-*`, `FEAT-10-*`, `FEAT-12-*`,
  `FEAT-13-*`)
- **Ambientes efêmeros por Pull Request** (preview deployments) — não
  solicitado
- **Rollback automático** em caso de problema pós-deploy — não
  solicitado; rollback, se necessário, continua manual
- **Notificações** (Slack, e-mail, etc.) sobre status do pipeline — não
  solicitado
- **Decisão automática entre patch/minor/major** no rascunho de
  release — sempre sugere patch bump; ajuste manual antes de publicar,
  mesma regra da FEAT-11 do frontend
- **Deletar a branch automaticamente após o merge** — comportamento
  padrão do GitHub, não configurado por esta feature
- **Sincronizar retroativamente `main` com o estado atual de
  `develop`** — decisão separada do usuário, já registrada como
  pendente desde a FEAT-10 do frontend
- **Geração automática de changelog/release notes por conta própria**
  — usa o recurso nativo do GitHub (`--generate-notes`), mesma regra
  da FEAT-11 do frontend
