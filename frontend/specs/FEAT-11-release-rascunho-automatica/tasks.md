# Tasks — FEAT-11: Release de homologação automática (rascunho)

## `.github/workflows/frontend-deploy-hom.yml` (código)

- [ ] 1. Adicionar o job `draft-release` (`needs: deploy`,
      `permissions: contents: write`): calcula a versão sugerida
      (patch bump da última release publicada, via `gh release list
      --exclude-drafts`), remove rascunho pendente existente (se
      houver) e cria um novo com `gh release create --draft --target
      develop --generate-notes`
- [ ] 2. Validar sintaxe YAML do arquivo modificado (`js-yaml` ou
      equivalente)

## Validação end-to-end (toca o GitHub real)

- [ ] 3. Fazer uma alteração trivial em `frontend/app/**` nesta branch
      (`FEAT-11-release-rascunho-automatica`) e dar push — dispara
      `frontend-feature-pr.yml` (FEAT-10), que roda o gate de
      qualidade e abre o PR automaticamente pra `develop`
- [ ] 4. Confirmar que o PR foi aberto automaticamente (mecanismo já
      validado na FEAT-10, mas confirmar de novo não custa nada)
- [ ] 5. Revisar e mergear manualmente o PR (decisão do usuário)
- [ ] 6. O merge em `develop` dispara `frontend-deploy-hom.yml`
      automaticamente — confirmar que o job `deploy` (hom) passa
- [ ] 7. Confirmar que o job `draft-release` roda em seguida e cria um
      rascunho de release: tag = patch bump de `v0.1.0` (deve ser
      `v0.1.1`, já que essa é a única release publicada hoje), target
      `develop`, notas geradas automaticamente
- [ ] 8. Fazer uma segunda alteração trivial em `frontend/app/**`
      **numa nova branch** (ex.: pequeno ajuste qualquer, só pra gerar
      um novo push em `develop`) e confirmar que o rascunho pendente é
      **atualizado** (mesmo processo: rascunho antigo removido, novo
      criado) em vez de duplicado — valida US4 de verdade, diferente
      da FEAT-10 (onde a idempotência não chegou a ser testada com um
      2º push)
- [ ] 9. Confirmar que nenhum dos rascunhos criados foi publicado
      automaticamente em nenhum momento (US5)

## Documentação e fechamento

- [ ] 10. Atualizar
      `frontend/specs/FEAT-11-release-rascunho-automatica/spec.md`,
      marcando os critérios de aceite concluídos (`- [x]`) e
      preenchendo uma seção "Status" com o resumo do que foi
      implementado/validado
