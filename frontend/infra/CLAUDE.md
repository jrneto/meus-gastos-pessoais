# Infra do Frontend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs.

## Estado atual

O frontend ainda não foi iniciado (ver `/frontend/README.md`), então esta
pasta não tem conteúdo ainda. Quando o frontend e sua infraestrutura de
deploy (hosting estático, CDN, etc.) forem definidos, documente aqui as
decisões específicas do frontend — não herde automaticamente decisões da
infra do backend (`/backend/infra/CLAUDE.md`).

## Princípios gerais (herdados do monorepo)

- Toda infraestrutura é AWS.
- IaC futuro será feito exclusivamente em Terraform — não gerar código
  Terraform até que seja solicitado explicitamente.

## Specs

Quando este contexto começar a ter specs próprias de infraestrutura,
seguir o mesmo padrão do restante do frontend (a definir quando o
frontend for iniciado): `frontend/specs/{FEAT-XX-nome}/{spec.md, plan.md,
tasks.md}`, nunca arquivo solto.
