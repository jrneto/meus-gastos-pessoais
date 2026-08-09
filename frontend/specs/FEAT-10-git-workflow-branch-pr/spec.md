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

- [x] `/CLAUDE.md` raiz documenta a convenção de nomear a branch como
      a pasta de spec, para os dois contextos
- [x] Push numa branch `FEAT-XX-nome` (frontend) que altera
      `frontend/app/**` roda o gate de qualidade (lint + testes) —
      validado ao vivo (`FEAT-10-git-workflow-branch-pr`, commit
      `274bd1b`)
- [x] Se o gate passar e não existir PR aberto para essa branch →
      `develop`, um PR é aberto automaticamente — validado ao vivo
      (PR #1)
- [~] Pushes subsequentes na mesma branch não criam PR duplicado —
      lógica implementada e revisada (`gh pr list` antes de `gh pr
      create`), mas não exercitada com um 2º push real nesta rodada
      (o PR #1 já foi aprovado e mergeado depois do 1º push) — ver
      "Status"
- [x] Deploy de produção bem-sucedido (`frontend-deploy-prod.yml`)
      abre automaticamente um PR `develop → main`, se ainda não existir
      um aberto — validado ao vivo durante a FEAT-11: publicar a
      release `v0.1.1` disparou o deploy de prod, que abriu o PR #3
      (`develop → main`), mergeado manualmente pelo usuário
- [ ] Releases/deploys de produção subsequentes não criam PR
      `develop → main` duplicado — ainda não exercitado (só uma
      release foi publicada até agora); validação real fica pra
      próxima release
- [x] Nenhum dos dois PRs automáticos faz merge sozinho — ambos
      aguardam aprovação manual (confirmado nos dois: PR #1 e PR #3
      foram mergeados manualmente pelo usuário, não automaticamente)
- [x] Nenhum recurso novo (AWS ou de terceiros) foi criado; consumo de
      minutos de Actions permanece dentro da cota gratuita
- [x] A permissão do repositório para Actions abrirem PR foi habilitada
      só após aprovação explícita do usuário

## Status

**Totalmente validado ao vivo** (a automação `develop → main`, que
ficou pendente no fechamento inicial desta feature, acabou sendo
validada durante a FEAT-11 — ver atualização abaixo).

- **Bootstrap bem-sucedido**: esta própria feature nasceu numa branch
  (`FEAT-10-git-workflow-branch-pr`, criada a partir de `develop` já
  no `/specify`) e foi a primeira a validar o mecanismo que ela mesma
  introduz — um push trivial em `frontend/app/**` nessa branch disparou
  o `frontend-feature-pr.yml` recém-criado, que abriu o **PR #1**
  automaticamente. Revisado e mergeado manualmente pelo usuário.
- **Primeiro merge commit real do histórico do projeto**
  (`a86067e`, "Merge pull request #1..."), rompendo o padrão de
  histórico 100% linear (commits direto em `develop`) que valia até a
  FEAT-09.
- **Idempotência do PR branch→develop**: implementada e revisada em
  código (`gh pr list` antes de `gh pr create`), mas não forçada com
  um segundo push nesta rodada — o PR #1 foi aprovado e mergeado logo
  depois do primeiro push. Validação real fica para a próxima feature
  que receber mais de um push antes do merge.
- **Atualização (durante a FEAT-11)**: a automação `develop → main`
  (job `open-pr-main`) foi validada ao vivo — a FEAT-11
  (`frontend/specs/FEAT-11-release-rascunho-automatica/`) introduziu
  um mecanismo que gera releases em rascunho após cada deploy de hom;
  o usuário publicou o rascunho `v0.1.1` manualmente para testar a
  cadeia completa, disparando `frontend-deploy-prod.yml` de verdade
  (deploy real em produção) e, por consequência, o PR #3
  (`develop → main`), mergeado manualmente. Único ponto ainda não
  exercitado: releases subsequentes não duplicarem esse PR (só uma
  release foi publicada até agora).
- Config de repositório (`Allow GitHub Actions to create and approve
  pull requests`) habilitada manualmente pelo usuário, com aprovação
  prévia, antes de qualquer teste.

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
