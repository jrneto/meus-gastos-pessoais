# FEAT-16: CRUD de Categorias

## Objetivo

Permitir que o usuário autenticado consulte, crie, edite e exclua suas
próprias categorias de despesa.

## Contexto

Hoje `ExpenseCategory` (`Alimentacao`, `Transporte`, `Moradia`, `Saude`,
`Educacao`, `Lazer`, `ComprasEServicos`, `Outros`) é um enum fechado
usado apenas em `POST`/`GET /expenses` (FEAT-04/FEAT-06). A própria
FEAT-04 deixou explicitamente fora de escopo o "cadastro dinâmico de
categorias". Esta feature cobre esse CRUD como uma entidade própria
(`Categoria`), já prevista em `backend/docs/data-model.md`.

A criação automática das 8 categorias padrão do sistema para todo
cliente novo (mencionada no pedido inicial) foi conscientemente adiada:
por ora este CRUD não pré-popula nada — o usuário só vê as categorias
que ele mesmo criar. O mecanismo de seed (quando e como as categorias
padrão passam a existir por padrão) fica para uma decisão futura, ver
"Fora do escopo".

**Esta feature não altera o contrato nem o comportamento de
`POST`/`GET /expenses`**: a validação de categoria em despesas continua
usando exclusivamente o enum fechado `ExpenseCategory`, independente do
que existir cadastrado via este CRUD. A ligação entre despesas e
categorias cadastradas dinamicamente fica para uma feature futura — foi
uma decisão explícita do usuário para manter o escopo desta feature
restrito ao CRUD.

## Requisitos de negócio

- Categoria é sempre vinculada ao `userId` extraído do JWT (claim
  `sub`), nunca informado no body
- Não há criação automática de categoria nesta feature: `GET
  /categories` só retorna o que o próprio cliente já tiver criado
  (lista vazia para um cliente novo, até ele criar sua primeira
  categoria via `POST /categories`)
- `nome`: obrigatório, texto não vazio, até 50 caracteres, único por
  cliente (comparação sem diferenciar maiúsculas/minúsculas — não pode
  existir "Lazer" e "lazer" ao mesmo tempo para o mesmo cliente)
- `cor`: obrigatória, formato hexadecimal `#RRGGBB`
- `icone`: obrigatório, identificador textual livre, até 50 caracteres
  (sem catálogo fechado de ícones nesta feature — validação é só de
  presença/tamanho)
- Exclusão de categoria é bloqueada quando o cliente já possui ao menos
  uma despesa registrada com `category` igual ao `nome` da categoria
  (comparação exata com o valor gravado em `Expense.category`, que hoje
  só assume valores do enum `ExpenseCategory`): a API retorna 422
  informando que a categoria não pode ser excluída enquanto houver
  despesas associadas — o cliente precisa excluir/mover as despesas
  primeiro e só então excluir a categoria
- Um cliente nunca pode consultar, editar ou excluir categoria de outro
  cliente (garantido por `userId` do JWT, nunca por dado do request)

## User Stories

**US1 — Consultar sem categorias cadastradas**
- Given um usuário autenticado que nunca criou nenhuma categoria
- When ele chama `GET /categories`
- Then a API retorna uma lista vazia, sem criar nada automaticamente

**US2 — Consultar categorias já cadastradas**
- Given um usuário autenticado que já possui categorias criadas por ele
- When ele chama `GET /categories`
- Then a API retorna todas as suas categorias

**US3 — Criar categoria própria**
- Given um usuário autenticado
- When ele envia `POST /categories` com `nome`, `cor` e `icone` válidos
  e não usados por nenhuma categoria sua
- Then a categoria é criada vinculada ao seu `userId` e a API retorna
  201 com os dados da categoria criada

**US4 — Impedir nome de categoria duplicado**
- Given um usuário autenticado que já possui uma categoria chamada
  "Lazer"
- When ele tenta criar outra categoria com `nome` "Lazer" ou "lazer"
- Then a API retorna 422 informando que já existe uma categoria com
  esse nome, e nenhuma categoria é criada

**US5 — Validar dados obrigatórios na criação**
- Given um usuário autenticado
- When ele envia `POST /categories` com campo obrigatório ausente ou
  inválido (`nome` vazio, `cor` fora do formato `#RRGGBB`, `icone`
  vazio)
- Then a API retorna 400 com detalhe do(s) campo(s) inválido(s) e
  nenhuma categoria é criada

**US6 — Editar categoria existente**
- Given um usuário autenticado com uma categoria sua já cadastrada
- When ele envia `PUT /categories/{id}` alterando `nome`, `cor` e/ou
  `icone` com dados válidos
- Then a categoria é atualizada e a API retorna 200 com os dados
  atualizados

**US7 — Impedir edição para nome duplicado**
- Given um usuário autenticado com duas categorias, "Lazer" e "Viagem"
- When ele tenta editar "Viagem" para `nome` "Lazer"
- Then a API retorna 422 informando que já existe uma categoria com
  esse nome, e a categoria não é alterada

**US8 — Editar/excluir categoria inexistente ou de outro usuário**
- Given um usuário autenticado
- When ele tenta editar ou excluir um `id` de categoria que não existe,
  ou que pertence a outro usuário
- Then a API retorna 404 e nenhuma alteração é feita

**US9 — Excluir categoria sem despesas associadas**
- Given um usuário autenticado com uma categoria sua sem nenhuma
  despesa registrada com aquele nome
- When ele envia `DELETE /categories/{id}`
- Then a categoria é excluída e a API retorna 204

**US10 — Impedir exclusão de categoria com despesas associadas**
- Given um usuário autenticado com uma categoria cujo `nome` coincide
  com o `category` de ao menos uma despesa sua já registrada
- When ele envia `DELETE /categories/{id}`
- Then a API retorna 422 informando que a categoria não pode ser
  excluída enquanto houver despesas associadas, e a categoria
  permanece intacta

**US11 — Isolamento entre usuários**
- Given dois usuários autenticados diferentes, cada um com suas
  próprias categorias
- When qualquer um deles consulta, cria, edita ou exclui categorias
- Then a operação nunca afeta nem expõe categorias do outro usuário

**US12 — Impedir qualquer operação sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta consultar, criar, editar ou excluir categorias
- Then a API retorna 401 e nenhum dado é retornado ou alterado

## Contratos da API

### GET /categories

Retorna somente as categorias que o próprio usuário já criou (lista
vazia se nenhuma).

Response 200:
```json
{
  "items": [
    {
      "id": "...",
      "nome": "Alimentacao",
      "cor": "#F97316",
      "icone": "utensils",
      "createdAt": "2025-06-15T12:34:56Z"
    }
  ]
}
```

### POST /categories

Request:
```json
{
  "nome": "Viagem",
  "cor": "#0EA5E9",
  "icone": "plane"
}
```

Response 201 (Location: /categories/{id}):
```json
{
  "id": "...",
  "nome": "Viagem",
  "cor": "#0EA5E9",
  "icone": "plane",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 400 (validation-error): campo obrigatório ausente/inválido.
Response 422 (name-conflict): já existe categoria com esse nome.

### PUT /categories/{id}

Request:
```json
{
  "nome": "Viagens",
  "cor": "#0EA5E9",
  "icone": "plane"
}
```

Response 200: dados atualizados da categoria (mesmo formato do POST).
Response 400 (validation-error): campo obrigatório ausente/inválido.
Response 404 (not-found): categoria não existe ou não pertence ao usuário.
Response 422 (name-conflict): nome já usado por outra categoria do usuário.

### DELETE /categories/{id}

Response 204: categoria excluída com sucesso.
Response 404 (not-found): categoria não existe ou não pertence ao usuário.
Response 422 (category-in-use): existem despesas associadas ao nome da
categoria.

```json
{
  "type": "https://gastosapp.dev/errors/category-in-use",
  "title": "Category In Use",
  "status": 422,
  "detail": "A categoria não pode ser excluída enquanto houver despesas associadas a ela."
}
```

### Erros comuns a todas as rotas

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
  "status": 404
}
```

## Critérios de aceite

- [x] GET /categories para um usuário sem categorias retorna lista
      vazia, sem criar nada
- [x] GET /categories retorna todas as categorias já criadas pelo
      usuário
- [x] POST /categories com dados válidos retorna 201 com a categoria
      criada, vinculada ao `userId` do token
- [x] POST /categories com `nome` já usado (mesmo usuário, sem
      diferenciar maiúsculas/minúsculas) retorna 422
- [x] POST /categories com campo obrigatório ausente/inválido retorna
      400
- [x] PUT /categories/{id} com dados válidos atualiza e retorna 200
- [x] PUT /categories/{id} para nome já usado por outra categoria do
      mesmo usuário retorna 422
- [x] PUT/DELETE /categories/{id} para id inexistente ou de outro
      usuário retorna 404
- [x] DELETE /categories/{id} sem despesas associadas retorna 204 e
      remove a categoria
- [x] DELETE /categories/{id} com despesas associadas (mesmo `nome` da
      categoria) retorna 422 e não remove a categoria
- [x] Nenhuma rota expõe ou altera categoria de outro usuário
- [x] Todas as rotas sem token retornam 401

## Status

Implementado. `Category`/`CategorySlug` (Domain), `CreateCategoryCommand`/
`UpdateCategoryCommand`/`DeleteCategoryCommand`/`GetCategoriesQuery` +
Handlers + Validators + `CategoryErrors`/`ICategoryRepository`/
`CategoryWriteResult` (Application), `DynamoDbCategoryRepository` +
`DynamoDbExpenseRepository.ExistsByCategoryAsync` (Infrastructure),
`GET`/`POST`/`PUT`/`DELETE /categories` (Api) implementados conforme
`plan.md`. Novo `ErrorType.UnprocessableEntity` mapeado para 422 em
`ResultHttpExtensions`. Nenhum recurso AWS novo — reaproveita a tabela
`GastosApp` e os índices `GSI1`/`GSI2` já provisionados. Suíte completa
(`dotnet test` na solução) passa: 277/277 (1 IntegrationTests
placeholder + 85 ComponentTests + 191 UnitTests).

## Fora do escopo

- Qualquer alteração em `POST`/`GET /expenses` ou no enum
  `ExpenseCategory` — despesas continuam validando categoria pelo enum
  fechado, independente deste CRUD
- Vincular despesas às categorias cadastradas dinamicamente por este
  CRUD (feature futura)
- Campo `ativo`/soft-delete de categoria — exclusão é definitiva
  (bloqueada apenas quando há despesas associadas)
- Criação automática das 8 categorias padrão do sistema para todo
  cliente (seed lazy no primeiro `GET`, trigger de signup do Cognito,
  ou qualquer outro mecanismo) — decisão adiada para uma feature futura;
  por ora `GET /categories` só reflete o que o cliente criou manualmente
- Catálogo fechado de ícones (validação de `icone` é só presença/tamanho)
- Reordenação/exibição customizada de categorias
