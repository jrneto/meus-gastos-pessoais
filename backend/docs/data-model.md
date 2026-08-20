# Data model — GastosApp

## Tabela: GastosApp (single-table, PAY_PER_REQUEST)

Chaves e índices provisionados (`backend/infra/terraform/environments/{hom,prod}/dynamodb.tf`):

| Atributo | Tipo | Papel |
|---|---|---|
| `PK` | S | Partition key da tabela base |
| `SK` | S | Sort key da tabela base |
| `GSI1PK` / `GSI1SK` | S | Índice `GSI1`, projeção `ALL` |
| `GSI2PK` | S | Índice `GSI2`, projeção `KEYS_ONLY` (só devolve `PK`/`SK`/`GSI2PK` — qualquer outro atributo exige `GetItem` complementar) |

## Tipos de item

### Despesa (`Expense`)

- `PK`: `USER#<userId>`
- `SK`: `TXN#<YYYY-MM-DD>#<id>` — granularidade diária (migrada de
  mensal para diária na FEAT-06, para permitir ordenação cronológica
  correta com `begins_with`/`BETWEEN` sem GSI extra)
- `GSI1PK`: `USER#<userId>#<Category>` (`Category` é o valor do enum
  `ExpenseCategory`, ex.: `Alimentacao`) — usado pelo filtro
  `category` de `GET /expenses` (FEAT-06)
- `GSI1SK`: `<YYYY-MM-DD>#<id>`
- `GSI2PK`: `ID#<id>` — adicionado na FEAT-07, resolve `GET/PUT/DELETE
  /expenses/{id}` a partir só do `id` (sem depender de conhecer a data
  para montar a `SK`); combinado com um `GetItem` seguinte, já que a
  projeção de `GSI2` é `KEYS_ONLY`
- Atributos: `Description` (string), `AmountInCents` (long, centavos),
  `Category` (string, enum fechado `ExpenseCategory`), `ExpenseDate`
  (string, `YYYY-MM-DD`), `Tipo` (string, hoje sempre `"despesa"` —
  `"receita"` ainda não implementado), `CreatedAt` (string, ISO 8601
  `DateTimeOffset`)

## Regras do modelo

- Valor sempre em centavos (`long`) — sem `float`/`decimal` no banco
- `SK` de despesa inclui o dia (não só o mês) para permitir ordenação
  cronológica e filtro por intervalo de datas via `begins_with`/`BETWEEN`
  na própria chave, sem GSI extra (FEAT-06)
- Atualizar uma despesa que muda `ExpenseDate` muda a `SK`/`GSI1SK` —
  não é `UpdateItem` in-place, é `TransactWriteItems` (`Delete` do item
  antigo + `Put` do novo); se a data não muda, é um `PutItem` simples
  sobrescrevendo o item (FEAT-08)
- `userId` vem do JWT (claim `sub`) — nunca do body
- Sem `Scan` — todo acesso é `Query` por `PK`/`SK` ou pelos GSIs

## Backlog (fora do MVP, não implementado)

- **Resumo mensal agregado** (`SUMMARY#<YYYY-MM>` por usuário, com
  `totalDespesas`/`totalReceitas`/`saldoMes`/`porCategoria`): segue só
  como ideia de modelagem futura — a tabela hoje não tem DynamoDB
  Streams habilitado nem Lambda trigger algum, então nenhuma agregação
  é calculada ou persistida automaticamente
- **Categoria como entidade própria** (CRUD dinâmico, hoje só o enum
  fechado `ExpenseCategory` existe): em especificação na FEAT-16
  (`backend/specs/FEAT-16-crud-categorias/`) — a modelagem definitiva
  (chave, índices) fica documentada aqui só depois de implementada
- Orçamento por categoria: item `BUDGET#<YYYY-MM>#<cat>`
- Tags em transações: requer GSI adicional ou filtro em memória
- Receita (`Tipo = "receita"`): hoje só `"despesa"` é gravado
