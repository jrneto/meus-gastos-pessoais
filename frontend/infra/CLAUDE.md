# Infra do Frontend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs.

## Estado atual

Hosting (S3 + CloudFront + ACM + WAF WebACL) e DNS (hosted zone +
records em Route 53) estão em produção
(`jrnexpenses.com`/`www.jrnexpenses.com`) e homologação
(`hom.jrnexpenses.com`), geridos por Terraform em
`frontend/infra/terraform/`, em **duas configurações independentes**
(mesmo princípio `bootstrap/`/config principal do backend), cada uma
com seu próprio state, ambas no bucket de state do backend
(`gastosapp-terraform-state-648443184523`, `key`s distintas):

- **`dns/`** — camada **persistente**, nunca destruída por um futuro
  pipeline de destroy/recreate. Gerencia a hosted zone
  `jrnexpenses.com.` (`lifecycle { prevent_destroy = true }`) e os
  records de prod (6, incl. `www`) e hom (A/AAAA + CNAME de validação
  ACM, sem `www.hom`). Lê CloudFront/ACM via `terraform_remote_state`
  de cada `environments/{prod,hom}` — se a infra principal for
  recriada, os records se atualizam sozinhos ao rodar `apply` aqui.
- **`environments/prod/`** — camada **efêmera**, destruível/recriável.
  Bucket S3, distribuição CloudFront, certificado ACM
  (`jrnexpenses.com`), WAF WebACL. Trazida via `terraform import`
  (nenhum recurso recriado).
- **`environments/hom/`** — mesma estrutura, provisionada do zero
  (`bucket S3 gastosapp-frontend-hom`, CloudFront, OAC, ACM
  `hom.jrnexpenses.com`), **+ WAF WebACL próprio** (`aws_wafv2_web_acl.hom`,
  mesmos 3 Managed Rule Groups de prod — prod não tem `waf.tf` porque o
  dele foi importado já associado à distribuição). Assinada ao plano
  flat-rate **Free** do CloudFront (2º dos 3 planos Free da conta,
  cobre distribuição+WAF+DDoS a US$0/mês, dentro de 1M req/100GB por
  mês) — assinatura feita **manualmente no console** (recurso Terraform
  `aws_pricingplanmanager_subscription` ainda não lançado em nenhuma
  versão do provider, [PR #49235](https://github.com/hashicorp/terraform-provider-aws/pull/49235)
  aberto; trazer via `import` quando disponível, prod e hom).
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
  console (`frontend/infra/terraform/cicd/` mantido só como
  referência) — `apply`/`import` falham com `AccessDenied` em ações de
  IAM (`Create`/`Get`/`List` de OpenIDConnectProvider/Role/RolePolicy);
  o perfil `agent-toolkit` não tem essas permissões mesmo sendo
  "Admin-Desenvolvedor" (guardrail intencional contra federação de
  identidade). Detalhes/ARNs: `frontend/infra/terraform/README.md`,
  seção "cicd/".
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
