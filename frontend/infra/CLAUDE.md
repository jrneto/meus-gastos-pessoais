# Infra do Frontend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs.

## Estado atual

A infra de hosting do frontend (S3 + CloudFront + ACM + WAF WebACL) e o
DNS (hosted zone + records do frontend em Route 53) já estão em
produção (`jrnexpenses.com`/`www.jrnexpenses.com`) e são geridos por
Terraform desde a FEAT-07 (`frontend/specs/FEAT-07-terraform-import-infra/`),
trazidos via `terraform import` — nenhum recurso foi criado, recriado ou
destruído nessa migração.

Terraform vive em `frontend/infra/terraform/`, em **duas configurações
independentes** (mesmo princípio de separar `bootstrap/`/config
principal já usado no backend), cada uma com seu próprio state, ambas no
bucket de state já existente do backend
(`gastosapp-terraform-state-648443184523`, `key`s distintas — nenhum
novo bootstrap foi criado):

- **`dns/`** — camada **persistente**, nunca destruída por um futuro
  pipeline de CI/CD com `destroy`/recreate. Gerencia a hosted zone
  `jrnexpenses.com.` (`aws_route53_zone`, com
  `lifecycle { prevent_destroy = true }`) e os 6 records DNS do
  frontend. Lê o domínio do CloudFront e os dados de validação do
  certificado ACM via `terraform_remote_state`, apontando para o state
  de `environments/prod/` — se a infra principal for recriada no
  futuro, os records se atualizam automaticamente ao rodar `apply` aqui,
  sem passo manual.
- **`environments/prod/`** — camada **efêmera**, destruível/recriável
  por esse pipeline futuro. Gerencia o bucket S3, a distribuição
  CloudFront, o certificado ACM (`jrnexpenses.com`) e o WAF WebACL.

Passo a passo de `init`/`import` e detalhes de cada recurso:
`frontend/infra/terraform/README.md`.

**Fora do Terraform, permanecem manuais**: records `NS`/`SOA` da zona,
o record de `api.jrnexpenses.com` (pertence ao contexto backend) e o
registro do domínio em si (`jrnexpenses.com`, transferência de
registrador — fora do alcance de qualquer IaC).

## Ambiente de homologação

Desde a FEAT-08 (`frontend/specs/FEAT-08-ambiente-homologacao/`),
existe também um ambiente de **homologação**, isolado de produção,
provisionado do zero via `terraform apply` (diferente de
`environments/prod/`, que foi só `import`):

- **`environments/hom/`** — mesma estrutura de `environments/prod/`
  (bucket S3 `gastosapp-frontend-hom`, distribuição CloudFront, OAC,
  certificado ACM `hom.jrnexpenses.com`), **mais um WAF WebACL próprio**
  (`aws_wafv2_web_acl.hom`, mesmos 3 AWS Managed Rule Groups de
  produção) — produção não tinha `waf.tf` na config principal porque o
  WebACL foi importado já associado à distribuição pela FEAT-07; aqui
  ele é criado do zero junto com o resto.
- **`dns/`** ganha um segundo `data "terraform_remote_state"` (`"hom"`,
  desacoplado do `"prod"`) e os records de `hom.jrnexpenses.com`
  (A/AAAA + CNAME de validação do certificado ACM), na mesma hosted
  zone já gerenciada. Sem variante `www.hom` (sem necessidade
  identificada para um ambiente de homologação).
- **Custo**: a distribuição de hom está assinada ao plano flat-rate
  **Free** do CloudFront (o 2º dos 3 planos Free disponíveis na conta —
  produção já usa 1), que cobre a distribuição + o WAF WebACL + DDoS
  protection a US$0/mês, dentro de 1M requisições/100GB de transferência
  por mês (folgado para tráfego de homologação).
- **Gap conhecido**: a assinatura de uma distribuição ao plano Free é
  feita hoje **manualmente no console AWS** — o recurso Terraform
  correspondente (`aws_pricingplanmanager_subscription`) ainda não foi
  lançado em nenhuma versão do provider (PR aberto e não mesclado,
  [hashicorp/terraform-provider-aws#49235](https://github.com/hashicorp/terraform-provider-aws/pull/49235)).
  Quando disponível, trazer essa assinatura (tanto a de hom quanto a de
  prod) para o Terraform via `terraform import`.
- **Chamadas de API a partir de `hom.jrnexpenses.com` dependem de CORS
  no backend** liberando essa origem em `https://api-hom.jrnexpenses.com`
  — mudança do contexto backend, ainda não feita (fora do escopo da
  FEAT-08).

## CI/CD (FEAT-09)

Desde a FEAT-09 (`frontend/specs/FEAT-09-cicd-github-actions/`), o
deploy do frontend em hom/prod é automatizado via GitHub Actions —
substitui o processo manual descrito na FEAT-08.

- **`.github/workflows/frontend-deploy-hom.yml`**: dispara em push em
  `develop` que toque `frontend/app/**`. Job `quality` (lint + testes)
  precisa passar antes do job `deploy`, que builda com
  `VITE_API_BASE_URL=https://api-hom.jrnexpenses.com` e versão
  `dev-<short-sha>`, publica em `gastosapp-frontend-hom` e invalida o
  cache da distribuição de hom.
- **`.github/workflows/frontend-deploy-prod.yml`**: dispara em GitHub
  Release publicada (tag semântica `vX.Y.Z`) — builda exatamente o
  código da tag, aponta para `https://api.jrnexpenses.com`, publica em
  `gastosapp-frontend-prod` e invalida o cache de prod. A criação da
  release é o próprio gate de promoção pra produção (sem "required
  reviewer" adicional — recurso pago em repo privado, que se tornou
  relevante desde que o repositório deixou de ser público).
- **Rastreabilidade de versão no site**: `src/lib/appVersion.ts` +
  `src/components/AppVersion.tsx`, exibido na `SettingsPage`. Em prod,
  linka pra release do GitHub; em hom, pro commit (nunca sugere uma
  release formal que não existe).
- **Autenticação AWS via OIDC** (GitHub Actions → IAM Role
  `gastosapp-frontend-cicd`), sem access key de longa duração em
  secret.
- **Gap conhecido — OIDC Provider + Role fora do Terraform**: ambos
  foram **criados manualmente no console AWS**, não pelo Terraform
  (`frontend/infra/terraform/cicd/`, código mantido como referência).
  `terraform apply`/`import` falharam com `AccessDenied` em várias
  ações de IAM (`Create`/`Get`/`List` para
  OpenIDConnectProvider/Role/RolePolicy) — o perfil usado
  (`agent-toolkit`) não tem essas permissões, aparentemente um
  guardrail intencional (permission set/SCP) contra ações de federação
  de identidade, mesmo sendo um perfil "Admin-Desenvolvedor". Detalhes,
  ARNs reais e passo a passo de `import` (se a permissão for liberada
  no futuro) em `frontend/infra/terraform/README.md`, seção "cicd/".
- GitHub Environments `hom`/`prod` cadastrados manualmente (`gh` CLI
  não disponível no ambiente de execução), com variáveis não-segredo
  (`BUCKET_NAME`, `DISTRIBUTION_ID`, `CICD_ROLE_ARN`).

## Princípios gerais (herdados do monorepo)

- Toda infraestrutura é AWS.
- IaC feito exclusivamente em Terraform — não gerar/alterar código
  Terraform para um recurso novo sem pedido explícito do usuário.
- Qualquer criação/alteração de recurso AWS que impacte custo ou
  segurança exige aprovação explícita do usuário antes da execução (ver
  `frontend/docs/constitution.md`) — vale também para `terraform
  import`/`apply`.

## Specs

Quando este contexto começar a ter specs próprias de infraestrutura,
seguir o mesmo padrão do restante do frontend (a definir quando o
frontend for iniciado): `frontend/specs/{FEAT-XX-nome}/{spec.md, plan.md,
tasks.md}`, nunca arquivo solto.
