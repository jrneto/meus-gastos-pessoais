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
