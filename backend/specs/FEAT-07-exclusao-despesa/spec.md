# FEAT-07: Exclusão de Despesa

## Objetivo

Permitir que o usuário autenticado exclua uma despesa que ele mesmo registrou, removendo-a definitivamente do seu histórico de gastos.

## Regras de negócio

- Exclusão é definitiva (hard delete) — não existe estado "excluído"/lixeira, nem possibilidade de desfazer
- Só o `userId` dono da despesa (extraído do JWT, claim `sub`) pode excluí-la
- Tentar excluir uma despesa inexistente ou uma despesa pertencente a outro usuário retorna o mesmo resultado (404), para não revelar a um usuário se um determinado `id` existe na base de outro usuário
- Uma vez excluída, uma nova tentativa de excluir a mesma despesa (mesmo `id`) retorna 404, não 204
- Despesa excluída deixa de aparecer em consultas subsequentes (`GET /expenses`)

## User Stories

**US1 — Excluir despesa própria com sucesso**
- Given um usuário autenticado dono de uma despesa
- When ele solicita a exclusão dessa despesa pelo seu `id`
- Then a API retorna 204 sem corpo, e a despesa deixa de existir (não aparece mais em consultas subsequentes)

**US2 — Impedir exclusão sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta excluir uma despesa
- Then a API retorna 401 e nenhuma despesa é excluída

**US3 — Excluir despesa inexistente**
- Given um usuário autenticado
- When ele solicita a exclusão de um `id` que não corresponde a nenhuma despesa
- Then a API retorna 404 e nenhum estado é alterado

**US4 — Impedir exclusão de despesa de outro usuário**
- Given dois usuários autenticados diferentes, onde um deles registrou uma despesa
- When o outro usuário tenta excluir essa despesa pelo `id`
- Then a API retorna 404 (sem diferenciar de "inexistente"), a despesa não é excluída e permanece intacta para o usuário dono

## Contratos da API

### DELETE /expenses/{id}

Sem request body.

Response 204 (sem corpo):
```
(vazio)
```

Response 401 (unauthorized):
```json
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401
}
```

Response 404 (not-found):
```json
{
  "type": "https://gastosapp.dev/errors/not-found",
  "title": "Not Found",
  "status": 404,
  "detail": "Despesa não encontrada."
}
```

## Critérios de aceite

- [x] DELETE /expenses/{id} de uma despesa própria e existente retorna 204 sem corpo
- [x] Despesa excluída deixa de ser retornada por GET /expenses em consultas subsequentes
- [x] DELETE /expenses/{id} sem token retorna 401 e nenhuma despesa é excluída
- [x] DELETE /expenses/{id} com `id` inexistente retorna 404
- [x] DELETE /expenses/{id} de uma despesa pertencente a outro usuário retorna 404 (não 403) e a despesa permanece intacta
- [x] Excluir a mesma despesa uma segunda vez retorna 404 na segunda chamada

## Status

Implementado. Novo GSI2 (`GSI2PK = ID#{id}`, projeção `KEYS_ONLY`) em
`backend/infra/terraform/dynamodb.tf` permite localizar a chave real
(`PK`/`SK`) de uma despesa a partir do `id`. `DeleteExpenseCommand`/
`DeleteExpenseCommandHandler`/`ExpenseErrors` (Application),
`DynamoDbExpenseRepository.DeleteAsync` (Infrastructure, com checagem de
posse via comparação de `PK` e `DeleteItem` condicional) e
`DELETE /expenses/{id}` (Api) implementados conforme `plan.md`. A
"despesa deixa de ser retornada por GET /expenses" é garantida pela
própria natureza do hard delete (`DeleteItemAsync` remove o item da
tabela consultada por `QueryAsync`), sem necessidade de teste dedicado
para esse critério além do smoke manual. Suíte completa (`dotnet test`
na solução) passa: 145/145 (1 IntegrationTests + 100 UnitTests + 44
ComponentTests).

**Pendências fora do código** (ver `plan.md`): aplicar o `GSI2` na tabela
real via `terraform apply` e executar o runbook de migração manual do
`GSI2PK` para despesas já persistidas antes desta feature — ambos a
critério do usuário.

## Fora do escopo deste FEAT

- Exclusão em lote (múltiplas despesas de uma vez)
- Soft-delete / lixeira / possibilidade de desfazer a exclusão
- Edição de despesa (feature futura separada)
