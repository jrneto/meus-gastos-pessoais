# Tasks — FEAT-11: Release de homologação automática (rascunho)

## `.github/workflows/frontend-deploy-hom.yml` (código)

- [x] 1. Adicionar o job `draft-release` (`needs: deploy`,
      `permissions: contents: write`): calcula a versão sugerida
      (patch bump da última release publicada, via `gh release list
      --exclude-drafts`), remove rascunho pendente existente (se
      houver) e cria um novo com `gh release create --draft --target
      develop --generate-notes`
- [x] 2. Validar sintaxe YAML do arquivo modificado (`js-yaml`) — passou

## Validação end-to-end (toca o GitHub real)

- [x] 3. Alteração trivial em `frontend/app/src/lib/appVersion.ts`
      nesta branch, push — disparou `frontend-feature-pr.yml`
      (FEAT-10), gate de qualidade passou, PR aberto automaticamente
- [x] 4. PR #2 confirmado aberto automaticamente
- [x] 5. PR #2 revisado e mergeado manualmente pelo usuário
- [x] 6. Merge em `develop` disparou `frontend-deploy-hom.yml`; job
      `deploy` (hom) passou
- [x] 7. Job `draft-release` rodou em seguida: criou `v0.1.1` (patch
      bump de `v0.1.0`, a release publicada existente), target
      `develop`, notas geradas automaticamente — usuário publicou
      manualmente pra testar a cadeia completa (deploy prod +
      automação `develop → main` da FEAT-10, também validada nesse
      processo)
- [x] 8. Idempotência testada com 2 branches dedicadas
      (`FEAT-99-teste-idempotencia-draft-release`,
      `FEAT-99-teste-idempotencia-draft-release-2` — sem spec própria,
      só validação): 2 pushes consecutivos em `develop` sem publicar o
      rascunho entre eles. Resultado: só `v0.1.2` permaneceu (rascunho
      antigo removido e recriado a cada deploy), changelog acumulando
      os PRs #4 e #5 — confirmado via print da aba Releases
- [x] 9. Confirmado: nenhum rascunho (`v0.1.1` nem `v0.1.2`) foi
      publicado automaticamente — `v0.1.1` exigiu clique manual;
      `v0.1.2` permanece em Draft, deixado como está por decisão do
      usuário (conteúdo só de testes triviais)

## Documentação e fechamento

- [x] 10. `spec.md` atualizado: critérios de aceite marcados e seção
      "Status" preenchida com o resumo do que foi
      implementado/validado, incluindo a validação retroativa da
      automação `develop → main` da FEAT-10
