# FEAT-08: Atualização de Despesa

## Objetivo

Permitir que o usuário autenticado atualize os dados de uma despesa que ele mesmo registrou.

## Regras de negócio

- Atualização é uma substituição completa dos dados: todos os campos (`description`, `amountInCents`, `category`, `expenseDate`) são obrigatórios em toda chamada, sem suporte a atualização parcial
- Mesmas regras de validação do registro de despesa (FEAT-04):
  - Descrição: obrigatória, texto não vazio, limite de 200 caracteres
  - Valor: obrigatório, inteiro positivo em centavos (`long`), maior que zero
  - Categoria: obrigatória, deve ser um dos valores do enum fechado (`Alimentacao`, `Transporte`, `Moradia`, `Saude`, `Educacao`, `Lazer`, `ComprasEServicos`, `Outros`)
  - Data da despesa: obrigatória, formato ISO 8601, retroativa ou futura são permitidas
- Só o `userId` dono da despesa (extraído do JWT, claim `sub`) pode atualizá-la
- Atualizar uma despesa inexistente ou uma despesa pertencente a outro usuário retorna o mesmo resultado (404), para não revelar a um usuário se um determinado `id` existe na base de outro usuário
- O `id` da despesa nunca muda; a data de criação (`createdAt`) original é preservada mesmo após a atualização

## User Stories

**US1 — Atualizar despesa própria com dados válidos**
- Given um usuário autenticado dono de uma despesa
- When ele envia uma requisição para atualizar essa despesa com descrição, valor, categoria e data válidos
- Then a despesa é atualizada e a API retorna 200 com os dados atualizados

**US2 — Impedir atualização sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta atualizar uma despesa
- Then a API retorna 401 e nenhuma despesa é alterada

**US3 — Validar dados obrigatórios**
- Given um usuário autenticado dono de uma despesa
- When ele envia a requisição faltando campo obrigatório ou com valor inválido (ex.: valor <= 0, categoria fora do enum)
- Then a API retorna 400 com detalhe do(s) campo(s) inválido(s) e a despesa não é alterada

**US4 — Atualizar despesa inexistente**
- Given um usuário autenticado
- When ele solicita a atualização de um `id` que não corresponde a nenhuma despesa
- Then a API retorna 404 e nenhum estado é alterado

**US5 — Impedir atualização de despesa de outro usuário**
- Given dois usuários autenticados diferentes, onde um deles registrou uma despesa
- When o outro usuário tenta atualizar essa despesa pelo `id`
- Then a API retorna 404 (sem diferenciar de "inexistente"), a despesa não é alterada e permanece intacta para o usuário dono

## Contratos da API

### PUT /expenses/{id}

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 5290,
  "category": "Alimentacao",
  "expenseDate": "2025-06-16"
}
```

Response 200:
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 5290,
  "category": "Alimentacao",
  "expenseDate": "2025-06-16",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "Um ou mais campos são inválidos."
}
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

- [x] PUT /expenses/{id} de uma despesa própria com dados válidos retorna 200 com os dados atualizados
- [x] Despesa atualizada reflete os novos valores em consultas subsequentes (GET /expenses), preservando `id` e `createdAt` originais
- [x] PUT /expenses/{id} sem token retorna 401 e nenhuma despesa é alterada
- [x] PUT /expenses/{id} com descrição vazia/ausente retorna 400
- [x] PUT /expenses/{id} com valor <= 0 retorna 400
- [x] PUT /expenses/{id} com categoria fora do enum fechado retorna 400
- [x] PUT /expenses/{id} com `id` inexistente retorna 404
- [x] PUT /expenses/{id} de uma despesa pertencente a outro usuário retorna 404 (não 403) e a despesa permanece intacta

## Status

Implementado. `Expense.Restore` (Domain), `UpdateExpenseCommand`/
`UpdateExpenseCommandHandler`/`UpdateExpenseCommandValidator`
(Application), `DynamoDbExpenseRepository.UpdateAsync` (Infrastructure —
lookup via `GSI2`, checagem de posse, `GetItem` para preservar
`CreatedAt`, `PutItem` in-place quando a data não muda ou
`TransactWriteItems` — Delete+Put atômico — quando a data muda) e
`PUT /expenses/{id}` (Api) implementados conforme `plan.md`. Nenhuma
mudança de infraestrutura AWS foi necessária (reaproveita a tabela e o
`GSI2` já provisionados na FEAT-07). Suíte completa (`dotnet test` na
solução) passa: 166/166 (114 UnitTests + 1 IntegrationTests + 51
ComponentTests).

## Fora do escopo deste FEAT

- Atualização parcial (PATCH) — apenas substituição completa (PUT)
- Atualização em lote (múltiplas despesas de uma vez)
- Histórico de alterações / auditoria de edições
