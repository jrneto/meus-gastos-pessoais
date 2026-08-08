# Tasks — FEAT-09: Esteira de CI/CD (GitHub Actions)

## `frontend/app/` — rastreabilidade de versão (código)

- [x] 1. Estender `ImportMetaEnv` (`frontend/app/src/vite-env.d.ts`) com
      `VITE_APP_VERSION?: string` e `VITE_APP_COMMIT_SHA?: string`
- [x] 2. Criar `frontend/app/src/lib/appVersion.ts` com
      `getAppVersion()` (lê as duas env vars, calcula `isRelease` via
      regex `^v\d+\.\d+\.\d+$`, monta `url` — release do GitHub se
      `isRelease`, commit se não — e aplica fallback `dev-local`/`local`
      quando as env vars não existem, ex.: `npm run dev`)
- [x] 3. Criar `frontend/app/src/components/AppVersion.tsx` (componente
      compartilhado em `components/`, não `features/`, conforme regra
      de dependência do projeto): exibe `version` de `getAppVersion()`
      como link (`<a target="_blank" rel="noreferrer">`) para `url`,
      sem link quebrado no fallback local
- [x] 4. Integrar `<AppVersion />` em
      `frontend/app/src/routes/SettingsPage.tsx`, abaixo do botão
      "Sair"

## `frontend/infra/terraform/cicd/` — arquivos Terraform (código, sem `apply`)

- [x] 5. Criar `frontend/infra/terraform/cicd/versions.tf` (mesmo
      provider/backend S3 das demais configs,
      `key = "gastosapp-frontend/cicd/terraform.tfstate"`)
- [x] 6. Criar `frontend/infra/terraform/cicd/variables.tf`
      (`github_org_repo` default `"jrneto/meus-gastos-pessoais"`,
      nomes dos buckets hom/prod, ARNs/IDs das distribuições
      CloudFront de hom/prod)
- [x] 7. Criar `frontend/infra/terraform/cicd/oidc.tf`
      (`aws_iam_openid_connect_provider` para
      `token.actions.githubusercontent.com` — comentário no arquivo
      documentando que a existência prévia deve ser checada antes do
      `apply`, ver task 11)
- [x] 8. Criar `frontend/infra/terraform/cicd/iam-role.tf`
      (`aws_iam_role` `gastosapp-frontend-cicd`, trust policy via
      `assume_role_policy` condicionada ao claim `sub` do OIDC,
      restrita a `repo:jrneto/meus-gastos-pessoais:ref:refs/heads/develop`
      e `repo:jrneto/meus-gastos-pessoais:ref:refs/tags/v*`)
- [x] 9. Criar `frontend/infra/terraform/cicd/iam-policy.tf`
      (`aws_iam_role_policy` com `s3:PutObject`/`DeleteObject`/`ListBucket`
      restritos aos ARNs dos buckets `gastosapp-frontend-hom`/`-prod`,
      e `cloudfront:CreateInvalidation` restrito aos ARNs das
      distribuições de hom/prod)
- [x] 10. Criar `frontend/infra/terraform/cicd/outputs.tf`
      (`cicd_role_arn`)

## `frontend/infra/terraform/cicd/` — provisionamento (toca a conta AWS real)

- [x] 11. Rodar `aws iam list-open-id-connect-providers` — retornou
      `AccessDenied` (perfil `agent-toolkit` sem essa permissão de
      leitura, mesmo sendo admin). Não bloqueou o `plan` (task 13), mas
      impediu confirmar de forma independente se já existia provider
      prévio
- [x] 12. Rodar `terraform init` + `terraform validate` em `cicd/` —
      passou (thumbprint inicial com 39 chars corrigido para o valor
      documentado de 40 chars da CA intermediária do GitHub)
- [x] 13. Apresentar o `terraform plan` de `cicd/` ao usuário e obter
      aprovação explícita antes de executar — 3 a criar, 0 a
      alterar/destruir, aprovado
- [x] 14. **Bloqueado**: `terraform apply` falhou com `AccessDenied` em
      `iam:CreateOpenIDConnectProvider` (mesma restrição de permissão
      da task 11, agora em escrita). Nenhum recurso real foi criado
      (state ficou limpo). Decisão do usuário: criar os 2 recursos
      manualmente no console AWS (trust policy e policy inline
      copiadas byte a byte do `.tf`, conferidas visualmente) e
      documentar como recurso fora do state, em vez de investigar/
      corrigir a permissão agora. `terraform import` também tentado e
      igualmente bloqueado (`AccessDenied` em
      `iam:GetOpenIDConnectProvider`) — detalhes e ARNs reais em
      `frontend/infra/terraform/README.md`, seção "cicd/"

## `.github/workflows/` — workflow de homologação (código)

- [x] 15. Criar `.github/workflows/frontend-deploy-hom.yml`:
      `on: push: branches: [develop], paths: ['frontend/app/**']`;
      `permissions: id-token: write, contents: read`;
      `environment: hom`; job `quality` (checkout, `setup-node` com
      cache npm, `npm ci`, `npm run lint`, `npm run test`) → job
      `deploy` (depende de `quality`; `npm run build` com
      `VITE_API_BASE_URL=https://api-hom.jrnexpenses.com`,
      `VITE_APP_VERSION=dev-${GITHUB_SHA::7}`,
      `VITE_APP_COMMIT_SHA=$GITHUB_SHA`; `configure-aws-credentials`
      via OIDC assumindo `cicd_role_arn`; `aws s3 sync dist/
      s3://gastosapp-frontend-hom/ --delete`; `aws cloudfront
      create-invalidation` na distribuição de hom)

## `.github/workflows/` — workflow de produção (código)

- [x] 16. Criar `.github/workflows/frontend-deploy-prod.yml`:
      `on: release: types: [published]`; sem filtro de `paths`;
      `permissions: id-token: write, contents: read`;
      `environment: prod`; checkout em
      `ref: ${{ github.event.release.tag_name }}`; mesma estrutura de
      jobs `quality` → `deploy`, com `VITE_API_BASE_URL=https://api.jrnexpenses.com`,
      `VITE_APP_VERSION=${{ github.event.release.tag_name }}`,
      `VITE_APP_COMMIT_SHA` do commit da tag; publica em
      `gastosapp-frontend-prod` e invalida a distribuição de prod

## GitHub — configuração de Environments (toca o repositório real)

- [x] 17. Apresentar ao usuário e obter aprovação explícita antes de
      criar os GitHub Environments `hom` e `prod` no repositório
      (Settings → Environments) — sem "required reviewers" (decisão já
      confirmada)
- [x] 18. Criar os Environments `hom`/`prod` e cadastrar as variáveis
      (não-segredo) de cada um — feito manualmente pelo usuário
      (`gh` CLI não disponível neste ambiente): `BUCKET_NAME`,
      `DISTRIBUTION_ID`, `CICD_ROLE_ARN` (`arn:aws:iam::648443184523:role/gastosapp-frontend-cicd`,
      mesma Role nos dois — quem diferencia é a trust policy por `ref`)

## Testes

- [x] 19. Teste unitário de `appVersion.ts`: `isRelease` verdadeiro
      para `v1.4.0`, falso para `dev-a1b2c3d`; `url` de release monta
      o link correto (`.../releases/tag/v1.4.0`); `url` de commit
      monta o link correto (`.../commit/<sha>`); fallback sem env vars
      retorna `dev-local`/`local`
- [x] 20. Teste de componente de `AppVersion.tsx`: renderiza a versão e
      o link com `href` correto, mockando `import.meta.env` via
      `vi.stubEnv`
- [x] 21. Atualizar `SettingsPage.test.tsx` para cobrir a presença do
      `AppVersion` na tela
- [x] 22. Rodar `npm run lint` e `npm test` (`vitest run`) em
      `frontend/app/` e confirmar 100% dos testes passando (regra da
      constitution) — `oxlint`: só os 2 warnings pré-existentes, sem
      erros; `vitest run`: 126/126 testes passando (27 arquivos);
      `npm run build` (`tsc -b && vite build`) também validado, sem
      erro de tipos

## Validação manual end-to-end (toca AWS/GitHub reais)

- [x] 23. Push real em `develop` (commit `6f92f63`, aprovado pelo
      usuário) — validado com 2 bugs reais encontrados e corrigidos no
      processo:
      1. **Trust policy com `sub` errado**: 1º deploy falhou em
         `configure-aws-credentials` (`Not authorized to perform:
         sts:AssumeRoleWithWebIdentity`) — jobs com `environment:`
         trocam o claim `sub` do token OIDC de `ref:refs/heads/<branch>`
         para `environment:<nome>`. Corrigido em `iam-role.tf` (commit
         `50471e0`) e no console AWS (trust policy da role); "Re-run
         failed jobs" confirmou o fix (`quality` 126/126, `deploy` OK)
      2. **`DISTRIBUTION_ID` do Environment `hom` cadastrado com o ID
         de PROD** (`E2YCZNS0F94SCU` em vez de `ELE195A1APCLB`) — o
         `aws s3 sync` publicou certo no bucket de hom, mas a
         invalidação de cache atingiu a distribuição errada (efeito
         colateral inofensivo em prod: 1 invalidação à toa, sem
         mudança de conteúdo), deixando hom servindo um `index.html`
         em cache apontando pra um JS já deletado pelo `--delete`
         (`403` real). Corrigido: variável do Environment `hom`
         ajustada pelo usuário + invalidação manual de correção
         (`aws cloudfront create-invalidation --distribution-id
         ELE195A1APCLB`) rodada para destravar o estado já quebrado
      - **Resultado final validado**: `https://hom.jrnexpenses.com`
        serve o bundle novo (`index-CmAmxQA3.js`, `200`), só referencia
        `api-hom.jrnexpenses.com` (0 referências a prod), e exibe
        `Versão: dev-6f92f63` linkando pra
        `https://github.com/jrneto/meus-gastos-pessoais/commit/6f92f6345a4938e92288cd4cfbc0845961e932dd`
        (conferido direto no bundle minificado)
- [x] 24. Release `v0.1.0` publicada pelo usuário (tag criada a partir
      de `develop`, já que `main` estava 40 commits atrás — nunca
      recebeu merge, achado registrado abaixo). `frontend-deploy-prod.yml`
      disparou, `quality` (126/126) e `deploy` verdes (1m40s total).
      Validado ao vivo: `https://jrnexpenses.com` serve o bundle novo
      (`index-CUippbRX.js`, `200`, `X-Cache: Miss`), só referencia
      `api.jrnexpenses.com`, e exibe a versão `v0.1.0` linkando pra
      `https://github.com/jrneto/meus-gastos-pessoais/releases/tag/v0.1.0`
      — `DISTRIBUTION_ID` de prod já estava certo desde o início
      (só o de hom tinha o bug)
- [x] 25. Isolamento confirmado nos dois sentidos: deploy de hom não
      alterou conteúdo de prod (task 23) e deploy de prod não alterou
      conteúdo de hom (buckets/distribuições distintos, cada deploy só
      referencia sua própria API)

## Documentação e fechamento

- [ ] 26. Atualizar `frontend/infra/CLAUDE.md` documentando a esteira
      de CI/CD (`cicd/`, workflows, Environments) e removendo/ajustando
      a menção a "deploy continua manual"
- [ ] 27. Atualizar `frontend/infra/terraform/README.md` com o passo a
      passo de `cicd/` (incluindo a checagem de OIDC Provider
      pré-existente)
- [x] 28. Atualizar
      `frontend/specs/FEAT-09-cicd-github-actions/spec.md`, marcando
      os critérios de aceite concluídos (`- [x]`) e preenchendo uma
      seção "Status" com o resumo do que foi implementado/validado —
      1 critério (cota de minutos do Actions) deixado sem marcar, não
      confirmável sem volume de uso real acumulado
