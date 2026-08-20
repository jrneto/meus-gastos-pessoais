# FEAT-17: Despesas vinculadas à Categoria dinâmica

## Objetivo

Fazer as rotas de despesas (`POST`/`GET`/`PUT /expenses`) pararem de
validar `category` contra o enum fechado `ExpenseCategory` e passarem a
referenciar uma `Categoria` de verdade — a entidade própria criada na
FEAT-16 (`GET`/`POST`/`PUT`/`DELETE /categories`).

## Contexto

A FEAT-16 criou o CRUD de categorias como uma entidade isolada,
propositalmente desacoplada de `Expense`: o enum `ExpenseCategory`
continuou sendo a única fonte de verdade para o campo `category` de uma
despesa, e a spec registrou essa ligação como "feature futura". Esta
feature é essa ligação.

Decisões tomadas com o usuário para esta feature:

- **O vínculo é por `id`, não por nome.** Categoria pode ser renomeada
  (`PUT /categories/{id}`) — se a despesa guardasse o nome, o vínculo
  quebraria silenciosamente a cada renomeio (a despesa ficaria
  apontando para um nome que não existe mais). Usar o `id` opaco da
  categoria mantém o vínculo estável independente de quantas vezes ela
  for renomeada. Consequência: o campo passa a se chamar `categoryId`
  em vez de `category` em todas as rotas de despesas, e passa a
  carregar o `id` retornado por `GET /categories`/`POST /categories`,
  não mais um valor do enum antigo (ex.: `"Alimentacao"`).
- **Enum `ExpenseCategory` é removido do projeto.** Deixa de existir
  qualquer lista fechada de categorias — toda categoria válida é uma
  que o próprio usuário criou via `POST /categories`.
- **Sem seed automático de categorias.** Continua sendo decisão fora de
  escopo (ver FEAT-16). Um usuário sem nenhuma categoria cadastrada
  precisa criar pelo menos uma (`POST /categories`) antes de conseguir
  registrar sua primeira despesa.
- **Sem migração de dados existentes.** As despesas hoje gravadas com
  valores do enum antigo (ex.: `category: "Alimentacao"`) serão
  removidas manualmente pelo usuário antes desta feature ir para
  homologação/produção — não há necessidade de compatibilidade retroativa.

## Requisitos de negócio

- `categoryId`: obrigatório em `POST`/`PUT /expenses`, deve
  referenciar uma categoria que **exista e pertença ao usuário
  autenticado** (mesmo `userId` do JWT); categoria de outro usuário é
  tratada como inválida, sem diferenciar de "não existe" (não vaza
  informação sobre categorias de terceiros)
- Uma despesa nunca pode ser criada ou atualizada apontando para uma
  categoria de outro usuário, mesmo que o `categoryId` seja válido para
  aquele outro usuário
- `GET /expenses` continua aceitando um filtro opcional por categoria —
  renomeado de `category` para `categoryId` — sem exigir que a
  categoria exista (filtrar por um `categoryId` que não existe/não tem
  despesas simplesmente retorna lista vazia, mesmo comportamento de
  qualquer outro filtro sem resultados)
- `DELETE /categories/{id}` (FEAT-16) continua bloqueando a exclusão
  com 422 quando existir despesa vinculada à categoria — a checagem
  passa a ser por `categoryId` (exata, sem ambiguidade de nome/case),
  mas o comportamento observável pela API não muda
- `userId` continua sempre extraído do JWT, nunca do body (regra
  imutável do projeto, sem mudança)

## User Stories

**US1 — Registrar despesa com categoria própria válida**
- Given um usuário autenticado com uma categoria própria já cadastrada
- When ele envia `POST /expenses` com `categoryId` dessa categoria e os
  demais campos válidos
- Then a despesa é criada vinculada a esse `categoryId`, e a API
  retorna 201 com os dados da despesa criada

**US2 — Impedir registro com categoria inexistente**
- Given um usuário autenticado
- When ele envia `POST /expenses` com `categoryId` que não existe
- Then a API retorna 400 com detalhe do campo inválido, e nenhuma
  despesa é criada

**US3 — Impedir registro com categoria de outro usuário**
- Given dois usuários autenticados, cada um com sua própria categoria
- When um deles envia `POST /expenses` usando o `categoryId` de uma
  categoria que pertence ao outro usuário
- Then a API retorna 400 (mesmo tratamento de categoria inexistente,
  sem revelar que o id pertence a outra conta), e nenhuma despesa é
  criada

**US4 — Editar despesa trocando de categoria**
- Given um usuário autenticado com uma despesa e duas categorias
  próprias
- When ele envia `PUT /expenses/{id}` com o `categoryId` da outra
  categoria
- Then a despesa passa a referenciar a nova categoria, e a API retorna
  200 com os dados atualizados

**US5 — Impedir edição com categoria inexistente ou de outro usuário**
- Given um usuário autenticado com uma despesa própria
- When ele envia `PUT /expenses/{id}` com `categoryId` inválido
  (inexistente ou de outro usuário)
- Then a API retorna 400 com detalhe do campo inválido, e a despesa não
  é alterada

**US6 — Consultar despesas filtrando por categoria**
- Given um usuário autenticado com despesas em categorias diferentes
- When ele consulta `GET /expenses?categoryId=...`
- Then a API retorna somente as despesas vinculadas àquele `categoryId`

**US7 — Consultar despesas com filtro de categoria sem resultados**
- Given um usuário autenticado
- When ele consulta `GET /expenses?categoryId=...` usando um id que não
  corresponde a nenhuma despesa sua (inexistente ou sem despesas
  vinculadas)
- Then a API retorna 200 com lista vazia, sem erro

**US8 — Renomear categoria não quebra o vínculo das despesas**
- Given um usuário autenticado com uma despesa vinculada a uma
  categoria "Lazer"
- When ele renomeia essa categoria para "Lazer e Hobbies"
  (`PUT /categories/{id}`) e em seguida consulta a despesa
- Then a despesa continua vinculada ao mesmo `categoryId`, sem
  qualquer ação adicional do usuário

**US9 — Excluir categoria com despesas vinculadas continua bloqueado**
- Given um usuário autenticado com uma categoria vinculada a pelo menos
  uma despesa
- When ele tenta `DELETE /categories/{id}` dessa categoria
- Then a API retorna 422 (mesmo comportamento já coberto pela FEAT-16),
  e a categoria permanece intacta

## Contratos da API

Os endpoints e status codes de `POST`/`GET`/`PUT`/`DELETE /expenses`
não mudam — só o campo `category` é renomeado para `categoryId` e sua
validação muda de "um dos valores do enum fechado" para "uma categoria
existente e própria do usuário".

### POST /expenses

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "expenseDate": "2025-06-15"
}
```

Response 201 (Location: /expenses/{id}):
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "expenseDate": "2025-06-15",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 400 (validation-error): `categoryId` ausente, inexistente, ou
de outra categoria que não pertence ao usuário — mesmo tratamento dos
demais campos obrigatórios inválidos.

### GET /expenses

Mesmos filtros da FEAT-06, com `category` renomeado para `categoryId`:

| Param | Tipo | Formato |
|---|---|---|
| `categoryId` | string | id de uma categoria (não precisa existir — sem resultado, retorna lista vazia) |
| `yearMonth`, `dateFrom`, `dateTo`, `minAmountInCents`, `maxAmountInCents`, `cursor`, `limit` | — | inalterados (FEAT-06) |

Response 200 (item da lista):
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "expenseDate": "2025-06-15",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

### GET /expenses/{id}

Mesmo formato de resposta acima, campo `categoryId` no lugar de
`category`.

### PUT /expenses/{id}

Request: mesmo formato do `POST /expenses`.
Response 200: mesmo formato de item acima.
Response 400 (validation-error): `categoryId` ausente/inexistente/de
outro usuário.
Response 404 (not-found): despesa inexistente ou de outro usuário
(comportamento já existente, inalterado).

## Critérios de aceite

- [x] POST /expenses com `categoryId` de categoria própria válida
      retorna 201 e a despesa fica vinculada a esse id
- [x] POST /expenses com `categoryId` inexistente retorna 400
- [x] POST /expenses com `categoryId` de categoria de outro usuário
      retorna 400 (mesmo tratamento de inexistente)
- [x] PUT /expenses/{id} com `categoryId` válido troca a categoria da
      despesa e retorna 200
- [x] PUT /expenses/{id} com `categoryId` inexistente ou de outro
      usuário retorna 400
- [x] GET /expenses?categoryId=... retorna somente despesas vinculadas
      àquele id
- [x] GET /expenses?categoryId=... com id sem despesas correspondentes
      retorna 200 com lista vazia
- [x] GET /expenses e GET /expenses/{id} retornam `categoryId` (não
      mais `category`) no corpo da resposta
- [x] Renomear uma categoria não altera nem invalida o `categoryId` de
      despesas já vinculadas a ela
- [x] DELETE /categories/{id} com despesas vinculadas continua
      retornando 422 (comportamento da FEAT-16 preservado)
- [x] Nenhuma rota expõe se um `categoryId` inválido pertence a outro
      usuário ou simplesmente não existe (mesma resposta 400 nos dois
      casos)

## Status

Implementado. `Expense.CategoryId` (Domain, `ExpenseCategory` removido),
`RegisterExpenseCommand`/`UpdateExpenseCommand`/`GetExpensesQuery` +
Validators com checagem assíncrona via `ICategoryRepository`
(Application), `DynamoDbExpenseRepository` com atributo/`GSI1PK` por
`categoryId` (Infrastructure), `POST`/`GET`/`PUT`/`DELETE /expenses`
com `categoryId` no lugar de `category` (Api) implementados conforme
`plan.md`. `DeleteCategoryCommandHandler` (FEAT-16) ajustado para
checar despesas associadas por `categoryId` em vez de nome. Nenhum
recurso AWS novo — reaproveita a tabela `GastosApp` e o `GSI1` já
provisionados, só o valor indexado muda de formato. Suíte completa
(`dotnet test` na solução) passa: 280/280 (1 IntegrationTests
placeholder + 86 ComponentTests + 193 UnitTests).

## Fora do escopo

- Migração/backfill de despesas já gravadas com valores do enum antigo
  — dados serão apagados manualmente pelo usuário antes desta feature
  ir para homologação/produção
- Criação automática de categorias padrão para todo usuário (seed) —
  decisão adiada desde a FEAT-16, continua fora de escopo
- Qualquer mudança em `GET`/`POST`/`PUT`/`DELETE /categories` além do
  necessário internamente para a checagem de exclusão bloqueada (FEAT-16)
  passar a comparar por `categoryId` em vez de nome — contrato dessas
  rotas não muda
- Exibir dados da categoria (nome/cor/ícone) embutidos na resposta de
  despesa — cliente usa `GET /categories` para resolver `categoryId` em
  nome/cor/ícone, evitando duplicar informação que ficaria
  desatualizada a cada renomeio
