# FEAT-10: Fluxo de branch por feature + PRs automáticos

## Objetivo

Formalizar e automatizar o fluxo de Git do monorepo: toda feature em
Fluxo Completo passa a nascer numa branch própria (nomeada como a
pasta de spec), e dois momentos do ciclo de vida passam a abrir PR
automaticamente — quando a implementação passa nos testes (branch da
feature → `develop`) e quando um deploy de produção é bem-sucedido
(`develop` → `main`). O objetivo central é garantir que **`main` sempre
reflita o que está de fato em produção** — hoje ela pode ficar
desatualizada indefinidamente, sem nenhum mecanismo que force a
sincronização (achado registrado durante a FEAT-09).

## Contexto

Até aqui, todo o histórico do projeto (frontend e backend, incluindo
features de infra) foi commitado direto em `develop`, sem branch de
feature nem PR (histórico linear, confirmado via `git log`). A branch
`main` nunca recebeu merge de nada — está parada no commit inicial.

Esta feature muda esse padrão a partir de agora, só para o contexto
**frontend** (que já tem CI/CD desde a FEAT-09,
`frontend/specs/FEAT-09-cicd-github-actions/`). O backend ainda não
tem nenhum workflow de qualidade — a convenção de nomear a branch como
a pasta de spec vale para os dois contextos desde já (é só um hábito
de nomenclatura, documentado em `/CLAUDE.md` raiz), mas a automação de
abrir PR fica restrita ao frontend por enquanto.

## Requisitos de negócio / restrições

- **Nascimento da branch**: toda feature em Fluxo Completo (frontend)
  nasce numa branch a partir de `develop`, criada já no `/specify`
  (antes de qualquer código), nomeada exatamente igual à pasta criada
  em `frontend/specs/` (ex.: `FEAT-10-git-workflow-branch-pr`).
  `spec.md`/`plan.md`/`tasks.md` e todo o código da feature vivem
  nessa branch — `develop` só recebe tudo de uma vez quando o PR é
  mergeado.
- **PR automático branch → `develop`**: a cada push na branch da
  feature que altere `frontend/app/**`, o mesmo gate de qualidade já
  usado no deploy de homologação (lint + testes) roda; se passar, um
  PR da branch para `develop` é aberto automaticamente — **só se ainda
  não existir um PR aberto** para esse par de branches (idempotente:
  pushes seguintes atualizam o PR já existente via commits novos,
  nunca criam um segundo).
- **PR automático `develop` → `main`**: quando o workflow de deploy de
  produção (`frontend-deploy-prod.yml`, disparado por uma GitHub
  Release) termina com sucesso, um PR de `develop` para `main` é
  aberto automaticamente — mesma regra de idempotência (não duplica se
  já existir um aberto).
- **Merge sempre manual**: nenhum dos dois PRs automáticos faz merge
  sozinho — ambos ficam aguardando revisão e aprovação manual do
  usuário. Nenhuma mudança entra em `develop` ou `main` sem esse
  checkpoint humano.
- **Custo zero**: abrir PR via GitHub Actions usa a API nativa do
  GitHub (`GITHUB_TOKEN` da própria Action ou `gh` CLI, já disponível
  nos runners hospedados) — sem infraestrutura nova, sem custo
  adicional, dentro da mesma cota gratuita de Actions já usada pela
  FEAT-09.
- **Configuração de repositório necessária**: por padrão, o GitHub
  bloqueia que uma Action crie PRs usando o `GITHUB_TOKEN` automático
  (`Settings → Actions → General → "Allow GitHub Actions to create and
  approve pull requests"`, hoje provavelmente desligada). Habilitar
  essa opção é uma mudança de configuração do repositório — **exige
  aprovação explícita do usuário antes de ser feita**, mesmo não tendo
  custo, seguindo a mesma cultura de aprovação já usada para recursos
  AWS.
- **Modo Leve não usa esse fluxo**: como não cria pasta em `specs/`,
  não há nome de branch a seguir — continua indo direto para
  `develop`, sem mudança.
- **Backend fora do escopo desta automação**: a convenção de
  nomenclatura de branch vale para o backend também (documentada no
  `/CLAUDE.md` raiz), mas a automação de PR não é implementada lá
  nesta feature — o backend não tem workflow de qualidade ainda; até
  ter, o PR de uma feature de backend continua manual.

## User Stories

**US1 — PR automático para `develop` quando a feature passa nos testes**
- Given uma branch `FEAT-XX-nome` com mudanças em `frontend/app/**`
- When um push acontece nessa branch e o gate de qualidade (lint +
  testes) passa
- Then um PR dessa branch para `develop` é aberto automaticamente,
  caso ainda não exista um aberto para esse par de branches

**US2 — Sem PR duplicado em pushes subsequentes**
- Given um PR já aberto de `FEAT-XX-nome` para `develop`
- When novos pushes (verdes) acontecem na mesma branch
- Then nenhum PR novo é criado — o PR existente simplesmente recebe os
  commits novos (comportamento nativo do GitHub)

**US3 — PR automático para `main` após deploy de produção bem-sucedido**
- Given uma GitHub Release publicada, disparando
  `frontend-deploy-prod.yml`
- When esse workflow termina com sucesso (quality + deploy + invalidação
  de cache OK)
- Then um PR de `develop` para `main` é aberto automaticamente, caso
  ainda não exista um aberto

**US4 — Sem PR duplicado entre releases consecutivas**
- Given um PR já aberto de `develop` para `main`
- When uma nova release é publicada e o deploy de produção correspondente
  também é bem-sucedido
- Then nenhum PR novo é criado — o PR existente continua refletindo o
  estado atual de `develop`

**US5 — Merge sempre manual**
- Given qualquer um dos dois PRs automáticos (branch→develop,
  develop→main)
- When ele é aberto
- Then nenhum merge automático acontece — o PR fica aguardando revisão
  e aprovação manual do usuário

**US6 — Sem custo adicional**
- Given a automação de abertura de PR via GitHub Actions
- When os workflows rodam
- Then nenhum recurso novo (AWS ou de terceiros) é criado, e o consumo
  de minutos de Actions permanece dentro da cota gratuita já usada pela
  FEAT-09

**US7 — Habilitação de permissão do repositório com aprovação explícita**
- Given a necessidade de habilitar "Allow GitHub Actions to create and
  approve pull requests" nas configurações do repositório
- When essa mudança está prestes a ser feita
- Then o usuário é consultado e aprova explicitamente antes de
  qualquer alteração de configuração real

## Contratos observáveis

Não há mudança de contrato de API. As mudanças observáveis são todas
no fluxo de Git/GitHub:
- Branches de feature do frontend passam a existir nomeadas como
  `FEAT-XX-nome-feature`, criadas a partir de `develop`.
- Dois novos tipos de PR aparecem automaticamente no repositório:
  `FEAT-XX-nome-feature → develop` (após gate de qualidade passar) e
  `develop → main` (após deploy de produção bem-sucedido) — ambos
  identificáveis pelo autor ser a própria Action (`github-actions[bot]`
  ou equivalente) e por um título/corpo padronizado indicando a origem
  automática.
- Nenhum dos dois PRs mergeia sozinho.

## Critérios de aceite

- [ ] `/CLAUDE.md` raiz documenta a convenção de nomear a branch como
      a pasta de spec, para os dois contextos
- [ ] Push numa branch `FEAT-XX-nome` (frontend) que altera
      `frontend/app/**` roda o gate de qualidade (lint + testes)
- [ ] Se o gate passar e não existir PR aberto para essa branch →
      `develop`, um PR é aberto automaticamente
- [ ] Pushes subsequentes na mesma branch não criam PR duplicado
- [ ] Deploy de produção bem-sucedido (`frontend-deploy-prod.yml`)
      abre automaticamente um PR `develop → main`, se ainda não existir
      um aberto
- [ ] Releases/deploys de produção subsequentes não criam PR
      `develop → main` duplicado
- [ ] Nenhum dos dois PRs automáticos faz merge sozinho — ambos
      aguardam aprovação manual
- [ ] Nenhum recurso novo (AWS ou de terceiros) foi criado; consumo de
      minutos de Actions permanece dentro da cota gratuita
- [ ] A permissão do repositório para Actions abrirem PR foi habilitada
      só após aprovação explícita do usuário

## Fora do escopo

- **Automação equivalente no backend** — a convenção de nomear branch
  como a pasta de spec vale lá também, mas a automação de PR fica
  pendente até o backend ter seu próprio CI/CD (feature futura
  separada, nesse outro contexto)
- **Auto-merge dos PRs** — não solicitado; merge continua manual nos
  dois casos
- **Deletar a branch automaticamente após o merge** — não solicitado;
  comportamento padrão do GitHub (configurável manualmente nas
  configurações do repositório) cobre isso se o usuário quiser
- **Sincronizar `main` retroativamente com o estado atual de
  `develop`** (os 40 commits de atraso já existentes) — esta feature
  só garante que, a partir de agora, `main` avance a cada deploy de
  produção bem-sucedido; a sincronização do atraso já existente é uma
  decisão separada do usuário
- **Ambientes efêmeros por PR** (preview deployments) — não solicitado
