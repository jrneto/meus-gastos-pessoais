# Infra do Backend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs, e
[`/backend/CLAUDE.md`](../CLAUDE.md) para o contexto geral do backend.

## Princípios

- Toda infraestrutura é **100% AWS**. Não há ambiente simulado: mesmo em
  desenvolvimento local, o backend se conecta diretamente aos recursos
  AWS reais (Cognito, DynamoDB, Parameter Store) — ver
  `backend/docs/constitution.md` e `backend/docs/architecture.md`.
- **Sem LocalStack, sem Kong.** Nenhuma simulação local de infraestrutura
  AWS deve ser (re)introduzida.
- IaC futuro será feito **exclusivamente em Terraform** — não
  CloudFormation, não CDK. **Não gerar código Terraform até que seja
  solicitado explicitamente.**
- Hoje não existe nenhum código de provisionamento de infraestrutura
  neste diretório (nenhum `.tf`).

## Estado legado (pendente de decisão)

`docker-compose.yml`, `kong.yml` e `scripts/` (incluindo
`scripts/localstack-init/`) são artefatos de uma abordagem anterior
baseada em LocalStack/Kong, que contradiz o princípio acima (infra 100%
AWS real, sem simulação). Não os use como referência para novo trabalho,
e não os modifique/estenda sem antes confirmar com o usuário — a remoção
ou substituição por Terraform é uma decisão pendente do usuário, ainda
não tomada.

## Specs

Quando este contexto começar a ter specs próprias de infraestrutura,
seguir o mesmo padrão do restante do backend:
`backend/specs/{FEAT-XX-nome}/{spec.md, plan.md, tasks.md}`, nunca
arquivo solto — não crie uma árvore de specs separada só para infra.
