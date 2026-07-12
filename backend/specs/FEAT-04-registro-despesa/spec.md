# FEAT-04: Registro de Despesa

## Objetivo

Permitir que o usuário autenticado registre uma nova despesa (gasto), informando descrição, valor, categoria e data, para acompanhar seus gastos pessoais.

## Regras de negócio

- Despesa é sempre vinculada ao `userId` extraído do JWT (claim `sub`) — nunca informado no body
- Descrição: obrigatória, texto não vazio, limite razoável de tamanho (ex.: 200 caracteres)
- Valor: obrigatório, inteiro positivo em centavos (`long`), deve ser maior que zero
- Categoria: obrigatória, deve ser um dos valores do enum fechado:
  `Alimentacao`, `Transporte`, `Moradia`, `Saude`, `Educacao`, `Lazer`, `ComprasEServicos`, `Outros`
  (lista sujeita a ajuste na revisão da spec)
- Data da despesa: obrigatória, formato ISO 8601 (`"2025-06-15"`), pode ser diferente da data de criação (permite lançamento retroativo ou futuro)
- Um usuário nunca pode registrar despesa em nome de outro usuário (garantido por não aceitar `userId` no request)

## User Stories

**US1 — Registrar despesa com dados válidos**
- Given um usuário autenticado
- When ele envia uma requisição para registrar uma despesa com descrição, valor, categoria e data válidos
- Then a despesa é criada e vinculada ao seu `userId`, e a API retorna 201 com os dados da despesa criada

**US2 — Impedir registro sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta registrar uma despesa
- Then a API retorna 401 e nenhuma despesa é criada

**US3 — Validar dados obrigatórios**
- Given um usuário autenticado
- When ele envia a requisição faltando campo obrigatório ou com valor inválido (ex.: valor <= 0, categoria fora do enum)
- Then a API retorna 400 com detalhe do(s) campo(s) inválido(s) e nenhuma despesa é criada

**US4 — Isolamento entre usuários**
- Given dois usuários autenticados diferentes
- When cada um registra suas próprias despesas
- Then cada despesa fica associada apenas ao usuário que a criou, sem possibilidade de um usuário registrar despesa em nome do outro (userId nunca vem do body)

## Contratos da API

### POST /expenses

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "category": "Alimentacao",
  "expenseDate": "2025-06-15"
}
```

Response 201 (Location: /expenses/{id}):
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "category": "Alimentacao",
  "expenseDate": "2025-06-15",
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

## Critérios de aceite

- [x] POST /expenses com dados válidos e usuário autenticado retorna 201 com a despesa criada
- [x] Despesa criada é sempre associada ao `userId` do token JWT, nunca a um `userId` do body
- [x] POST /expenses sem token retorna 401 e nenhuma despesa é persistida
- [x] POST /expenses com descrição vazia/ausente retorna 400
- [x] POST /expenses com valor <= 0 retorna 400
- [x] POST /expenses com categoria fora do enum fechado retorna 400
- [x] POST /expenses com data retroativa (anterior a hoje) é aceito normalmente
- [x] POST /expenses com data futura (posterior a hoje) é aceito normalmente
- [x] Dois usuários distintos conseguem registrar despesas próprias sem interferência entre si

## Status

Implementado. `Expense`/`ExpenseCategory` (Domain), `RegisterExpenseCommand`/
`RegisterExpenseCommandHandler`/`ExpenseErrors` (Application),
`DynamoDbExpenseRepository`/`DynamoDbOptions` (Infrastructure) e
`POST /expenses` (Api) implementados conforme `plan.md`. Suíte completa
(`dotnet test` na solução) passa: 63/63 (1 IntegrationTests placeholder +
19 ComponentTests + 43 UnitTests).

## Fora do escopo deste FEAT

- Edição de despesa
- Exclusão de despesa
- Anexar comprovante
- Recorrência de despesa
- Cadastro dinâmico de categorias (CRUD de categoria) — categoria é enum fechado nesta feature
- Listagem/consulta de despesas (GET /expenses) — outra feature
