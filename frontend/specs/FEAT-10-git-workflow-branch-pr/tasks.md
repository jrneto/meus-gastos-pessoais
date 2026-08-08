# Tasks — FEAT-10: Fluxo de branch por feature + PRs automáticos

## Configuração de repositório (fora do Terraform/AWS, toca o GitHub real)

- [x] 1. Apresentar ao usuário e obter aprovação explícita antes de
      habilitar **Settings → Actions → General → Workflow
      permissions → "Allow GitHub Actions to create and approve pull
      requests"** — pré-requisito pros dois workflows funcionarem
- [x] 2. Habilitar essa opção — feito manualmente pelo usuário

## `.github/workflows/frontend-feature-pr.yml` (novo, código)

- [x] 3. Criar o workflow: `on: push: branches: ["FEAT-*"], paths:
      ["frontend/app/**"]`, job `quality` (checkout, `setup-node`,
      `npm ci`, `npm run lint`, `npm run test` — mesmo padrão de
      `frontend-deploy-hom.yml`)
- [x] 4. Adicionar job `open-pr` (`needs: quality`,
      `permissions: pull-requests: write, contents: read`): checkout +
      step que roda `gh pr list --head <branch> --base develop --state
      open` e só chama `gh pr create` se não existir PR aberto —
      título = nome da branch, corpo fixo simples
- [x] 5. Validar sintaxe YAML do arquivo (`js-yaml`) — passou

## `.github/workflows/frontend-deploy-prod.yml` (modificado, código)

- [x] 6. Adicionar job `open-pr-main` (`needs: deploy`,
      `permissions: pull-requests: write, contents: read`): mesma
      lógica de idempotência, checando PR aberto `develop → main`
      antes de criar; título inclui a tag da release
      (`${{ github.event.release.tag_name }}`)
- [x] 7. Validar sintaxe YAML do arquivo modificado — passou

## Validação end-to-end (toca o GitHub real)

- [x] 8. Push da branch `FEAT-10-git-workflow-branch-pr` pro remoto —
      feito
- [x] 9. Alteração trivial em `frontend/app/src/lib/appVersion.ts`
      (comentário, commit `274bd1b`) nesta mesma branch, com push —
      deu match no `paths` do `frontend-feature-pr.yml` e disparou o
      workflow na própria branch que o introduziu
- [x] 10. Confirmado pelo usuário: job `quality` passou e job
      `open-pr` abriu automaticamente o PR #1
      (`FEAT-10-git-workflow-branch-pr → develop`)
- [~] 11. **Não exercitado formalmente nesta rodada** — o usuário já
      aprovou e mergeou o PR após o primeiro push (task 12), sem um
      segundo push antes do merge pra forçar o teste de duplicidade.
      Lógica de idempotência (`gh pr list` antes de `gh pr create`)
      revisada em código; validação real de "múltiplos pushes não
      duplicam PR" fica pra próxima feature branch que receber mais de
      um push antes do merge
- [x] 12. PR #1 revisado e mergeado manualmente pelo usuário —
      **primeiro merge commit real do histórico do projeto**
      (`a86067e`, "Merge pull request #1 from
      jrneto/FEAT-10-git-workflow-branch-pr"), rompendo o padrão de
      histórico 100% linear que existia até aqui

## Validação da automação `develop → main` (pendente de uma release real)

- [x] 13. Documentado: a validação completa do job `open-pr-main`
      (`frontend-deploy-prod.yml`) fica pendente até a próxima GitHub
      Release real ser publicada — não é simulável sem disparar um
      deploy de produção de verdade, e não faz sentido criar uma
      release só pra esse teste (ver seção "Status" do `spec.md`)

## Documentação e fechamento

- [x] 14. Conferido: `/CLAUDE.md` raiz continua consistente com a
      implementação real — nenhum ajuste necessário
- [x] 15. `spec.md` atualizado: critérios de aceite marcados e seção
      "Status" preenchida com o resumo do que foi
      implementado/validado, deixando explícito o que depende da
      task 13
