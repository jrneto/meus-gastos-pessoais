# Tasks — FEAT-10: Fluxo de branch por feature + PRs automáticos

## Configuração de repositório (fora do Terraform/AWS, toca o GitHub real)

- [ ] 1. Apresentar ao usuário e obter aprovação explícita antes de
      habilitar **Settings → Actions → General → Workflow
      permissions → "Allow GitHub Actions to create and approve pull
      requests"** — pré-requisito pros dois workflows funcionarem
- [ ] 2. Habilitar essa opção (feito manualmente pelo usuário, `gh`
      CLI não disponível no ambiente de execução)

## `.github/workflows/frontend-feature-pr.yml` (novo, código)

- [ ] 3. Criar o workflow: `on: push: branches: ["FEAT-*"], paths:
      ["frontend/app/**"]`, job `quality` (checkout, `setup-node`,
      `npm ci`, `npm run lint`, `npm run test` — mesmo padrão de
      `frontend-deploy-hom.yml`)
- [ ] 4. Adicionar job `open-pr` (`needs: quality`,
      `permissions: pull-requests: write, contents: read`): checkout +
      step que roda `gh pr list --head <branch> --base develop --state
      open` e só chama `gh pr create` se não existir PR aberto —
      título = nome da branch, corpo fixo simples
- [ ] 5. Validar sintaxe YAML do arquivo (`js-yaml` ou equivalente,
      sem precisar de execução real)

## `.github/workflows/frontend-deploy-prod.yml` (modificado, código)

- [ ] 6. Adicionar job `open-pr-main` (`needs: deploy`,
      `permissions: pull-requests: write, contents: read`): mesma
      lógica de idempotência, checando PR aberto `develop → main`
      antes de criar; título inclui a tag da release
      (`${{ github.event.release.tag_name }}`)
- [ ] 7. Validar sintaxe YAML do arquivo modificado

## Validação end-to-end (toca o GitHub real)

- [ ] 8. Push da branch `FEAT-10-git-workflow-branch-pr` pro remoto
      (primeira vez que essa branch existe no GitHub)
- [ ] 9. Fazer uma alteração trivial em `frontend/app/**` nesta mesma
      branch (ex.: comentário) e dar push — isso deve dar match no
      `paths` do `frontend-feature-pr.yml` recém-criado e disparar o
      workflow na própria branch que o introduziu (self-test natural:
      valida o mecanismo e já cumpre o propósito de abrir o PR real
      desta feature pra `develop`)
- [ ] 10. Confirmar: job `quality` passa (lint + testes) e job
      `open-pr` abre automaticamente um PR
      `FEAT-10-git-workflow-branch-pr → develop` — validar
      título/corpo do PR gerado
- [ ] 11. Fazer um segundo push trivial na mesma branch e confirmar que
      **nenhum PR duplicado** é criado (idempotência — `gh pr list`
      encontra o PR já aberto)
- [ ] 12. Revisar e mergear manualmente o PR aberto na task 10 (decisão
      do usuário, sem merge automático — conforme US5 da spec)

## Validação da automação `develop → main` (pendente de uma release real)

- [ ] 13. Documentar em `spec.md`/`tasks.md` que a validação completa
      do job `open-pr-main` (`frontend-deploy-prod.yml`) fica pendente
      até a próxima GitHub Release real ser publicada — não é
      simulável sem disparar um deploy de produção de verdade, e não
      faz sentido criar uma release só pra esse teste

## Documentação e fechamento

- [ ] 14. Conferir se `/CLAUDE.md` raiz (já atualizado no `/specify`)
      continua consistente com o que foi implementado — ajustar se
      algum detalhe mudou entre o `plan.md` e a implementação real
- [ ] 15. Atualizar
      `frontend/specs/FEAT-10-git-workflow-branch-pr/spec.md`,
      marcando os critérios de aceite concluídos (`- [x]`) e
      preenchendo uma seção "Status" com o resumo do que foi
      implementado/validado — deixando explícito o que depende da
      task 13 (validação de `open-pr-main` pendente de release real)
