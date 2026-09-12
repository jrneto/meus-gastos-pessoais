# Infra do Frontend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs.

## Estado atual

Hosting (S3 + CloudFront + ACM + WAF WebACL) está em produção
(`jrnexpenses.com`/`www.jrnexpenses.com`) e homologação
(`hom.jrnexpenses.com`). Desde a FEAT-34 (extração de infra para
`infra-jrnexpenses`, etapas 1-4 concluídas — falta só a etapa 7, de
fechamento de docs), a infra está dividida em dois repositórios:
**workload** (bucket S3 + PAB + SSE + bucket policy, por ambiente)
segue gerido por Terraform aqui, em `frontend/infra/terraform/`;
**plataforma** (CloudFront/OAC/ACM/WAF, `dns/`, `cicd/`) vive em
`infra-jrnexpenses`. Duas configurações independentes neste monorepo,
cada uma com seu próprio state, ambas no bucket de state do backend
(`gastosapp-terraform-state-648443184523`, `key`s distintas):

- **`dns/`** — movida para o repositório `infra-jrnexpenses`
  (`terraform/dns/`) na FEAT-34, etapa 2
  (`frontend/specs/FEAT-34-extracao-infra-repo-apartado/`). Gerencia a
  hosted zone `jrnexpenses.com.` (`lifecycle { prevent_destroy = true }`)
  e os records de prod (6, incl. `www`) e hom (A/AAAA + CNAME de
  validação ACM, sem `www.hom`). Lê CloudFront/ACM via
  `terraform_remote_state` de cada `environments/{prod,hom}`, ambas as
  `key`s já apontando para `infra-jrnexpenses/{hom,prod}/terraform.tfstate`
  (etapas 3 e 4).
- **`environments/prod/`** — desde a FEAT-34, etapa 4, gerencia **só o
  workload** (bucket S3 `gastosapp-frontend-prod` + PAB + SSE + bucket
  policy). A **plataforma** (OAC, distribuição CloudFront
  `E2YCZNS0F94SCU`, ACM `jrnexpenses.com` + SAN `www`, WAF
  `CreatedByCloudFront-8ee8deea`) vive em
  `infra-jrnexpenses/terraform/environments/prod/`.
- **`environments/hom/`** — mesma estrutura desde a etapa 3: bucket S3
  `gastosapp-frontend-hom` + PAB + SSE + bucket policy. A plataforma
  (OAC, distribuição `ELE195A1APCLB`, ACM `hom.jrnexpenses.com`, WAF
  `aws_wafv2_web_acl.hom` com os mesmos 3 Managed Rule Groups de prod)
  vive em `infra-jrnexpenses/terraform/environments/hom/`.
- Em ambos, a bucket policy deste monorepo lê o ARN da distribuição via
  `data.terraform_remote_state.infra` (`remote_state.tf`) — a infra
  nunca lê state do monorepo, só o inverso. Assinatura ao plano
  flat-rate **Free** do CloudFront (2º dos 3 planos Free da conta,
  cobre distribuição+WAF+DDoS a US$0/mês, dentro de 1M req/100GB por
  mês) — assinatura feita **manualmente no console** (recurso Terraform
  `aws_pricingplanmanager_subscription` ainda não lançado em nenhuma
  versão do provider, [PR #49235](https://github.com/hashicorp/terraform-provider-aws/pull/49235)
  aberto; trazer via `import` quando disponível — hoje seria no
  `infra-jrnexpenses`, prod e hom).
- CORS do backend para `hom.jrnexpenses.com` já liberado
  (`backend/infra/terraform/environments/hom/variables.tf`,
  `frontend_origins`).

**Fora do Terraform, permanecem manuais**: records `NS`/`SOA` da zona,
o record de `api.jrnexpenses.com`/`api-hom.jrnexpenses.com` (contexto
backend) e o registro do domínio em si. Passo a passo de
`init`/`import`: `frontend/infra/terraform/README.md`.

## CI/CD (GitHub Actions)

- **`.github/workflows/frontend-deploy-hom.yml`**: push em `develop`
  tocando `frontend/app/**`. Job `quality` (lint+testes) precisa passar
  antes do `deploy`, que builda com
  `VITE_API_BASE_URL=https://api-hom.jrnexpenses.com`, versão
  `dev-<short-sha>`, publica em `gastosapp-frontend-hom` e invalida o
  cache.
- **`.github/workflows/frontend-deploy-prod.yml`**: dispara em GitHub
  Release (tag `vX.Y.Z`) — builda o código da tag, aponta pra
  `https://api.jrnexpenses.com`, publica em `gastosapp-frontend-prod`.
  A release em si é o gate de promoção (sem "required reviewer" — pago
  em repo privado).
- **Rastreabilidade de versão**: `src/lib/appVersion.ts` +
  `src/components/AppVersion.tsx` (`SettingsPage`) — linka pra release
  do GitHub em prod, pro commit em hom.
- **Auth via OIDC**: IAM Role `gastosapp-frontend-cicd`, sem access key
  de longa duração em secret.
- **GitHub Environments** `hom`/`prod`, variáveis `BUCKET_NAME`/
  `DISTRIBUTION_ID`/`CICD_ROLE_ARN` (cadastradas manualmente, `gh` CLI
  indisponível no ambiente de execução).

## Gotchas conhecidos

- **OIDC Provider + Role fora do Terraform**: criados manualmente no
  console (código de referência movido para
  `infra-jrnexpenses/terraform/cicd/frontend/` na FEAT-34, etapa 1) —
  `apply`/`import` falham com `AccessDenied` em ações de IAM
  (`Create`/`Get`/`List` de OpenIDConnectProvider/Role/RolePolicy); o
  perfil `agent-toolkit` não tem essas permissões mesmo sendo
  "Admin-Desenvolvedor" (guardrail intencional contra federação de
  identidade). Detalhes/ARNs: `README.md` do `infra-jrnexpenses`.
- **Assinatura ao plano Free do CloudFront** (hom e prod) é manual no
  console — ver acima.

## Princípios gerais (herdados do monorepo)

- Toda infraestrutura é AWS.
- IaC exclusivamente Terraform — não gerar/alterar `.tf` para um
  recurso novo sem pedido explícito do usuário.
- Qualquer criação/alteração de recurso AWS que impacte custo ou
  segurança exige aprovação explícita do usuário antes da execução (ver
  `frontend/docs/constitution.md`) — vale também para `terraform
  import`/`apply`.

## Specs

Specs próprias de infraestrutura seguem o mesmo padrão do restante do
frontend: `frontend/specs/{FEAT-XX-nome}/{spec.md, plan.md, tasks.md}`,
nunca arquivo solto (ex.: `FEAT-07-terraform-import-infra`,
`FEAT-08-ambiente-homologacao`, `FEAT-09-cicd-github-actions`).
