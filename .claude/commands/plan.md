---
description: Gera o plan.md técnico de uma feature a partir da sua spec.md
---

Feature: $ARGUMENTS

Contexto padrão: **backend** (`/backend`), salvo indicação explícita de `/frontend` ou `/infra`.

1. Resolva a pasta da feature: `{contexto}/specs/{FEAT-XX-nome-feature}/`. Se o argumento não bater exatamente com uma pasta existente, procure por correspondência parcial (número ou slug) e confirme com o usuário antes de prosseguir. Se a pasta não existir, avise que é preciso rodar `/specify` antes.
2. Leia, nesta ordem: `{contexto}/specs/{feature}/spec.md`, `{contexto}/docs/constitution.md`, `{contexto}/CLAUDE.md`, e (se existir) `{contexto}/docs/architecture.md`.
3. Gere `plan.md` **na mesma subpasta** da feature (nunca fora dela), contendo:
   - Camadas afetadas (ex.: Api / Application / Domain / Infrastructure, ou o equivalente no frontend) e o que muda em cada uma
   - Contratos técnicos detalhados: assinatura de Commands/Queries/Handlers, DTOs, e para o backend também o padrão de acesso ao DynamoDB (PK/SK/GSI de entrada e saída) quando aplicável
   - Decisões técnicas relevantes (bibliotecas, padrões a seguir, trade-offs) — sempre respeitando as regras imutáveis da constitution
   - Quais recursos AWS são usados ou afetados (ex.: nova tabela/índice, novo App Client do Cognito, novo parâmetro no Parameter Store) — se nenhum recurso novo for necessário, declare isso explicitamente
   - Mapeamento de erros de negócio para `Error`/`ErrorType`/status HTTP, quando aplicável
4. Não implemente código neste comando — apenas produza o plano técnico.
5. Ao final, resuma as principais decisões tomadas e sinalize pontos que precisam de confirmação do usuário antes do `/tasks`.
