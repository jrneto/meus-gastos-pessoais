# FEAT-06: Consulta de Despesas

## Objetivo

Permitir que o usuário autenticado consulte as despesas já registradas
(FEAT-04), de forma flexível — por mês, categoria, intervalo de datas,
faixa de valor, ou qualquer combinação desses filtros — com resultados
paginados e ordenados cronologicamente. Esta feature é somente leitura:
não altera nem cria despesas.

## Contexto

Hoje as despesas são gravadas (FEAT-04) mas não existe nenhuma forma de
consultá-las — a listagem foi explicitamente deixada fora do escopo
daquela feature. Esta feature cobre exclusivamente a consulta.

Durante o desenho desta feature identificamos que a ordenação
cronológica correta dos resultados (mais recente primeiro) depende de um
ajuste na chave de ordenação da tabela `GastosApp`, hoje granular por mês
mas não por dia. Foi decidido junto ao usuário ajustar essa chave como
parte desta feature, o que exige migrar/reindexar as despesas já
registradas para o novo formato. O detalhamento técnico dessa mudança de
modelagem (nova chave, estratégia de migração) fica em `plan.md` — aqui
registramos apenas que ela é uma dependência desta feature e um ponto de
atenção para o usuário, conforme solicitado.

## Requisitos de negócio

- Uma consulta só retorna despesas do `userId` extraído do JWT (claim
  `sub`) — nunca despesas de outro usuário, mesmo que filtros tentem
  contornar isso
- Todos os filtros são opcionais e combináveis entre si:
  - `yearMonth` (`YYYY-MM`)
  - `category` (um dos valores do enum `ExpenseCategory`)
  - intervalo de datas (`dateFrom`/`dateTo`, ISO 8601, `YYYY-MM-DD`)
  - faixa de valor (`minAmountInCents`/`maxAmountInCents`)
- Se nenhum filtro for informado, retorna todas as despesas do usuário,
  paginadas (uso pessoal, volume esperado baixo — ver "Avisos de custo e
  performance")
- Resultado sempre ordenado por data da despesa (`expenseDate`),
  decrescente (mais recente primeiro); em caso de empate na mesma data,
  desempate por `createdAt` decrescente
- Paginação obrigatória, baseada em cursor opaco (`cursor`/`nextCursor`),
  não em número de página
- Tamanho de página (`limit`) configurável pelo cliente, com valor padrão
  e máximo definidos pela API (evita páginas excessivamente grandes)
- Combinações de filtro inconsistentes são rejeitadas com erro de
  validação (400): `dateFrom` posterior a `dateTo`, `minAmountInCents`
  maior que `maxAmountInCents`, `yearMonth` fora do formato esperado,
  `category` fora do enum fechado, `cursor` inválido/corrompido

## User Stories

**US1 — Consultar despesas de um mês**
- Given um usuário autenticado com despesas registradas em vários meses
- When ele consulta despesas informando apenas `yearMonth`
- Then a API retorna somente as despesas daquele mês, ordenadas da mais
  recente para a mais antiga

**US2 — Consultar despesas por categoria**
- Given um usuário autenticado com despesas em categorias diferentes
- When ele consulta despesas informando apenas `category`
- Then a API retorna somente as despesas daquela categoria, de todos os
  meses, ordenadas da mais recente para a mais antiga

**US3 — Consultar despesas por categoria e mês combinados**
- Given um usuário autenticado
- When ele consulta despesas informando `category` e `yearMonth` juntos
- Then a API retorna somente as despesas daquela categoria naquele mês

**US4 — Consultar despesas por intervalo de datas**
- Given um usuário autenticado
- When ele consulta despesas informando `dateFrom` e `dateTo`
- Then a API retorna somente despesas com `expenseDate` dentro do
  intervalo (inclusive nas duas pontas)

**US5 — Consultar despesas por faixa de valor**
- Given um usuário autenticado
- When ele consulta despesas informando `minAmountInCents` e/ou
  `maxAmountInCents`
- Then a API retorna somente despesas cujo `amountInCents` está dentro da
  faixa informada

**US6 — Combinar múltiplos filtros**
- Given um usuário autenticado
- When ele consulta despesas combinando `category`, `yearMonth`,
  intervalo de datas e faixa de valor ao mesmo tempo
- Then a API retorna apenas as despesas que satisfazem todos os filtros
  informados simultaneamente

**US7 — Consultar sem nenhum filtro**
- Given um usuário autenticado com despesas registradas
- When ele consulta despesas sem informar nenhum filtro
- Then a API retorna todas as suas despesas, paginadas, ordenadas da mais
  recente para a mais antiga

**US8 — Paginar resultados**
- Given um usuário autenticado com mais despesas do que o tamanho de
  página
- When ele consulta despesas e usa o `nextCursor` retornado para buscar a
  página seguinte
- Then a API retorna a próxima página de resultados, continuando a
  ordenação sem repetir nem pular itens

**US9 — Isolamento entre usuários**
- Given dois usuários autenticados diferentes, cada um com suas próprias
  despesas
- When um deles consulta despesas
- Then a API retorna somente as despesas do usuário autenticado, nunca as
  do outro usuário, independentemente dos filtros usados

**US10 — Impedir consulta sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta consultar despesas
- Then a API retorna 401 e nenhum dado é retornado

**US11 — Validar filtros inconsistentes**
- Given um usuário autenticado
- When ele consulta despesas com filtros inconsistentes (ex.: `dateFrom`
  posterior a `dateTo`, `minAmountInCents` maior que
  `maxAmountInCents`, `category` fora do enum, `yearMonth` em formato
  inválido, `cursor` inválido)
- Then a API retorna 400 com detalhe do(s) filtro(s) inválido(s)

## Contratos da API

### GET /expenses

Query params (todos opcionais, combináveis):

| Param | Tipo | Formato |
|---|---|---|
| `yearMonth` | string | `YYYY-MM` |
| `category` | string | um dos valores de `ExpenseCategory` |
| `dateFrom` | string | `YYYY-MM-DD` |
| `dateTo` | string | `YYYY-MM-DD` |
| `minAmountInCents` | long | > 0 |
| `maxAmountInCents` | long | > 0 |
| `cursor` | string | token opaco retornado por uma consulta anterior |
| `limit` | int | tamanho de página desejado (sujeito a padrão/máximo da API) |

Response 200:
```json
{
  "items": [
    {
      "id": "...",
      "description": "Almoço no restaurante",
      "amountInCents": 4590,
      "category": "Alimentacao",
      "expenseDate": "2025-06-15",
      "createdAt": "2025-06-15T12:34:56Z"
    }
  ],
  "nextCursor": "opaque-token-or-null"
}
```

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "Um ou mais filtros são inválidos."
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

## Avisos de custo e performance (DynamoDB)

- Consultas por `yearMonth`, `category` ou `category`+`yearMonth` juntos
  são as mais baratas: usam chave de partição/índice diretamente, sem
  varrer itens que não interessam.
- Consultas por intervalo de datas (`dateFrom`/`dateTo`) fora de um único
  `yearMonth`, ou por faixa de valor (`minAmountInCents`/
  `maxAmountInCents`), e a combinação de múltiplos meses sem `category`,
  exigem ler mais itens do que o filtro final devolve (filtragem aplicada
  sobre o resultado da consulta, não diretamente pela chave) — ainda é
  `Query`, nunca `Scan` (proibido pela constitution), mas o custo cresce
  com o volume de despesas do usuário no período consultado.
- Consulta sem nenhum filtro (US7) lê todas as despesas do usuário; para
  o uso pessoal previsto neste projeto o volume tende a ser baixo e o
  custo insignificante, mas é o padrão de consulta que mais cresce junto
  com o histórico do usuário ao longo do tempo.
- Não há, nesta feature, necessidade de novo índice (GSI) além do já
  existente — a mudança de modelagem necessária é apenas no formato da
  chave de ordenação para viabilizar a ordenação cronológica (ver
  "Contexto"), detalhada em `plan.md`.

## Critérios de aceite

- [x] GET /expenses sem filtros retorna todas as despesas do usuário
      autenticado, paginadas e ordenadas da mais recente para a mais
      antiga
- [x] GET /expenses?yearMonth=YYYY-MM retorna somente despesas daquele
      mês
- [x] GET /expenses?category=X retorna somente despesas daquela
      categoria
- [x] GET /expenses?category=X&yearMonth=YYYY-MM retorna somente
      despesas daquela categoria naquele mês
- [x] GET /expenses?dateFrom=...&dateTo=... retorna somente despesas com
      `expenseDate` dentro do intervalo (inclusive)
- [x] GET /expenses?minAmountInCents=...&maxAmountInCents=... retorna
      somente despesas com valor dentro da faixa
- [x] Filtros combinados (categoria + mês + datas + valor) retornam
      apenas despesas que satisfazem todos simultaneamente
- [x] Paginação via `cursor`/`nextCursor` percorre todos os resultados
      sem repetir nem pular itens
- [x] Cada usuário só vê suas próprias despesas, independentemente dos
      filtros usados
- [x] GET /expenses sem token retorna 401
- [x] GET /expenses com filtros inconsistentes retorna 400 com detalhe do
      campo inválido
- [ ] Despesas registradas antes desta feature (FEAT-04) aparecem
      corretamente ordenadas após a migração de modelagem — depende da
      execução manual do runbook de migração (`plan.md`), fora deste
      código; despesas novas já são gravadas no formato correto

## Status

Implementado. `GetExpensesQuery`/`GetExpensesQueryHandler`/
`GetExpensesQueryValidator`/`GetExpensesResult`/`ExpenseSummary`
(Application), `ExpenseQueryFilter`/`ExpenseQueryItem`/`ExpenseQueryPage`/
`ExpenseCursorCodec` (Application), `DynamoDbExpenseRepository.QueryAsync`
+ `SaveAsync` com SK/GSI1SK diários (Infrastructure) e `GET /expenses`
(Api) implementados conforme `plan.md`. Suíte completa (`dotnet test` na
solução) passa: 133/133 (1 IntegrationTests placeholder + 39
ComponentTests + 93 UnitTests).

Migração dos dados gravados antes desta feature (FEAT-04) para o novo
formato de SK/GSI1SK segue pendente de execução manual pelo usuário,
conforme runbook em `plan.md` — despesas novas já são persistidas
corretamente pela `SaveAsync` atualizada.

## Fora do escopo deste FEAT

- Criação, edição ou exclusão de despesas (cobertas por outras features)
- Endpoints agregados/analíticos (resumo mensal, totais por categoria,
  evolução anual) — dependem do item `SUMMARY` já descrito em
  `data-model.md`, fora desta feature
- Ordenação por campo diferente de data (ex.: ordenar por valor)
- Busca textual livre na descrição da despesa
- Filtro por tipo `despesa`/`receita` — hoje só existe `despesa`
  registrada (FEAT-04); `receita` ainda não foi implementada
- Exportação dos resultados (CSV, PDF etc.)
