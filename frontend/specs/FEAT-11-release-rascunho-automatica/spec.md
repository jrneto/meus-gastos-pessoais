# FEAT-11: Release de homologação automática (rascunho)

## Objetivo

Automatizar a criação de uma **GitHub Release em modo rascunho
(draft)** a cada deploy bem-sucedido em homologação, com versão
semântica sugerida e changelog gerado automaticamente a partir dos PRs
mergeados — sem publicar sozinha. O usuário continua testando em hom e
decidindo quando promover para produção; a única mudança é que, na
hora de promover, ele só precisa **revisar e clicar em "Publish
release"**, em vez de preencher tag/notas do zero.

## Contexto

Desde a FEAT-09 (`frontend/specs/FEAT-09-cicd-github-actions/`), o
deploy de produção é disparado por uma GitHub Release publicada
manualmente. Desde a FEAT-10
(`frontend/specs/FEAT-10-git-workflow-branch-pr/`), features do
frontend passam por PR antes de chegar em `develop` — o que agora
alimenta o recurso nativo do GitHub de gerar notas de release
automaticamente a partir dos PRs mergeados desde a última release
(`gh release create --generate-notes`).

Esta feature conecta as duas: a cada push em `develop` que resulta num
deploy de hom bem-sucedido (`frontend-deploy-hom.yml`, FEAT-09), um
novo job cria (ou atualiza) uma release em rascunho, pronta para
publicação. Não há mudança no gatilho de produção — continua sendo
exclusivamente `release: published`
(`frontend-deploy-prod.yml`), sem alteração.

## Requisitos de negócio / restrições

- **Gatilho**: novo job em `frontend-deploy-hom.yml`, rodando só
  depois que o job `deploy` (hom) tiver sucesso — se o deploy falhar,
  nenhum rascunho é criado/atualizado.
- **Versão sugerida**: patch bump da última release **publicada**
  (não-draft) existente no repositório (ex.: última publicada
  `v0.1.0` → rascunho sugere `v0.1.1`). Se não existir nenhuma release
  publicada ainda, usar `v0.0.1` como fallback.
- **Editável antes de publicar**: por ser um rascunho, o usuário pode
  ajustar a tag sugerida (ex.: para um bump de minor/major) antes de
  publicar — esta feature não tenta decidir automaticamente entre
  patch/minor/major a partir do conteúdo dos commits/PRs.
- **Changelog automático**: as notas do rascunho são geradas pelo
  recurso nativo do GitHub (`--generate-notes`), compilando os PRs
  mergeados em `develop` desde a última release publicada.
- **Idempotência sem acúmulo**: se já existir um rascunho pendente
  (não publicado), ele é **atualizado** a cada novo deploy de hom bem-
  sucedido (mesma tag sugerida se a última release publicada não
  mudou, notas regeneradas) — nunca se acumulam múltiplos rascunhos
  pendentes ao mesmo tempo.
- **Nunca publica sozinho**: o job só cria/atualiza o rascunho —
  publicar continua sendo uma ação manual do usuário no GitHub,
  exatamente como hoje.
- **Sem mudança no gatilho de produção**: `frontend-deploy-prod.yml`
  continua dependendo só de `release: published`, sem nenhuma alteração
  — publicar o rascunho criado por esta feature dispara o deploy de
  produção do mesmo jeito que publicar uma release criada manualmente
  hoje.
- **Custo zero**: usa a API nativa de Releases do GitHub via `gh` CLI
  (já disponível nos runners, mesma cota gratuita de Actions já usada).
  Nenhum recurso AWS ou de terceiros.

## User Stories

**US1 — Rascunho criado após deploy de hom bem-sucedido**
- Given um push em `develop` que resulta num deploy de hom
  bem-sucedido
- When o job de deploy termina com sucesso
- Then uma GitHub Release em modo rascunho é criada, com tag sugerida
  e notas geradas automaticamente

**US2 — Versão sugerida por patch bump**
- Given a última release publicada no repositório é `vX.Y.Z`
- When um novo rascunho é criado
- Then a tag sugerida é `vX.Y.(Z+1)`

**US3 — Changelog automático a partir dos PRs**
- Given PRs mergeados em `develop` desde a última release publicada
- When o rascunho é criado/atualizado
- Then as notas de release listam esses PRs automaticamente (recurso
  nativo do GitHub)

**US4 — Rascunho atualizado, não duplicado**
- Given um rascunho pendente já existente
- When um novo deploy de hom bem-sucedido acontece
- Then esse mesmo rascunho é atualizado (notas/tag sugerida
  recalculadas) — nenhum rascunho adicional é criado

**US5 — Nunca publica sozinho**
- Given um rascunho criado/atualizado por esta automação
- When ele é criado
- Then nenhuma publicação automática acontece — fica aguardando ação
  manual do usuário

**US6 — Publicar o rascunho dispara produção normalmente**
- Given um rascunho criado por esta automação
- When o usuário o publica manualmente pelo GitHub
- Then o `frontend-deploy-prod.yml` dispara exatamente como já
  acontece hoje com uma release criada do zero — sem nenhuma mudança
  nesse workflow

**US7 — Sem custo adicional**
- Given a automação de criação de rascunho
- When ela roda a cada deploy de hom
- Then nenhum recurso novo é criado e o consumo de minutos de Actions
  permanece dentro da cota gratuita já usada

## Contratos observáveis

Não há mudança de contrato de API. As mudanças observáveis são no
GitHub:
- Após cada deploy de hom bem-sucedido, a aba **Releases** do
  repositório passa a ter (ou atualizar) uma entrada em rascunho, com
  tag sugerida (`vX.Y.Z`) e notas geradas automaticamente.
- Nenhuma tag Git real é criada até o usuário publicar o rascunho
  (comportamento nativo de drafts do GitHub — a tag só é criada de
  fato na publicação).
- O fluxo de publicação manual → deploy de produção continua
  idêntico ao já existente desde a FEAT-09.

## Critérios de aceite

- [x] Deploy de hom bem-sucedido cria um rascunho de release, se não
      existir nenhum pendente — validado ao vivo (`v0.1.1` criado
      após o merge da própria FEAT-11 em `develop`)
- [x] A tag sugerida do rascunho é o patch bump da última release
      publicada (ou `v0.0.1` se não houver nenhuma publicada ainda) —
      `v0.1.0` → `v0.1.1` → `v0.1.2`, confirmado nas 2 rodadas de teste
- [x] As notas do rascunho são geradas automaticamente a partir dos
      PRs mergeados desde a última release publicada — confirmado
      (`v0.1.2` listou os PRs #4 e #5 corretamente em "What's Changed")
- [x] Um deploy de hom subsequente, com um rascunho já pendente,
      atualiza esse rascunho em vez de criar um novo — validado ao
      vivo com 2 pushes consecutivos sem publicar entre eles: só um
      rascunho (`v0.1.2`) permaneceu, com o changelog acumulando os 2
      PRs
- [x] Nenhum rascunho é publicado automaticamente em nenhum momento —
      confirmado (`v0.1.1` apareceu como Draft, precisou de clique
      manual em "Publish release")
- [x] Publicar manualmente um rascunho criado por esta automação
      dispara `frontend-deploy-prod.yml` normalmente, sem diferença
      em relação a uma release criada do zero — validado ao vivo
      (publicar `v0.1.1` disparou o deploy de produção normalmente,
      inclusive a automação `develop → main` da FEAT-10, antes
      pendente de teste)
- [x] Nenhum recurso novo (AWS ou de terceiros) foi criado; consumo de
      minutos de Actions permanece dentro da cota gratuita

## Status

**Implementado e validado end-to-end, incluindo idempotência.**

- Merge da própria FEAT-11 em `develop` (via PR automático da FEAT-10)
  disparou o primeiro deploy de hom real com o job `draft-release` —
  criou `v0.1.1` como rascunho, com notas geradas automaticamente.
- Usuário publicou `v0.1.1` manualmente para testar a cadeia completa:
  isso disparou `frontend-deploy-prod.yml` normalmente (deploy de
  produção real, confirmado via bundle ao vivo em `jrnexpenses.com`) e,
  por consequência, a automação `develop → main` da FEAT-10 (antes
  pendente de validação) — o PR `develop → main` foi aberto e
  mergeado, fechando também aquele gap em aberto.
- **Idempotência testada de propósito**: 2 branches de teste
  (`FEAT-99-teste-idempotencia-draft-release{,-2}`, sem spec própria —
  só validação, não features reais) geraram 2 pushes consecutivos em
  `develop` sem publicar o rascunho entre eles. Resultado: só um
  rascunho (`v0.1.2`) permaneceu, com o changelog acumulando os PRs
  #4 e #5 — confirma que o delete+recreate funciona como desenhado,
  sem acumular rascunhos.
- Rascunho de teste `v0.1.2` (só conteúdo trivial) deixado como está,
  por decisão do usuário — será substituído automaticamente na
  próxima mudança real.

## Fora do escopo

- **Publicação automática do rascunho** — não solicitado; publicar
  continua 100% manual
- **Decisão automática entre patch/minor/major** com base no conteúdo
  dos commits/PRs (ex.: conventional commits) — o rascunho sempre
  sugere patch bump; o usuário edita manualmente se quiser um
  incremento maior, antes de publicar
- **Deletar rascunhos manuais que o usuário eventualmente criar por
  conta própria** — esta automação só gerencia o rascunho que ela
  mesma cria/atualiza
- **Mudança em `frontend-deploy-prod.yml`** — o gatilho de produção
  continua exatamente como está, sem nenhuma alteração
