# FEAT-23: Resumo mensal (dashboard)

## Objetivo

Expor `GET /summary?month=YYYY-MM`, agregando as transações e categorias
da conta ativa de um mês específico num único payload: saldo, receitas,
gastos, orçamento total, restante, gasto por categoria (com orçamento) e
últimos lançamentos — dados suficientes para renderizar a tela "Resumo"
(dashboard) do design system sem o frontend precisar combinar múltiplas
chamadas (`/transactions` + `/categories`) nem fazer a agregação
client-side.

## Contexto

O design system (`frontend/design-system/screenshots/03-dashboard.png`)
mostra a tela inicial "Resumo" com seis blocos: "Saldo do mês",
"Receitas no mês", "Gasto no mês", "Orçamento total", "Restante" (com
barra de progresso), uma lista "Por categoria" (nome + gasto/orçamento +
barra de progresso, ex.: "Alimentação R$ 306,70 / R$ 800,00") e uma
lista "Últimos lançamentos" com link "Ver todas". Todos os números vêm
de dados que hoje já existem em `/transactions` (FEAT-22) e `/categories`
(FEAT-21, que introduziu `orcamentoMensalCents`), mas nenhuma rota atual
os agrega — o frontend precisaria buscar todas as transações do mês e
todas as categorias e somar na mão.

Segue `backend/docs/roadmap.md` (item "FEAT-23 — Resumo mensal
(dashboard)") e a decisão de modelagem já fechada no roadmap: **sem
tabela agregada nem DynamoDB Streams** — o resumo é sempre calculado via
`Query` do período + agregação em memória na própria request (ponto de
reavaliação futuro só se uma conta acumular milhares de
transações/ano).

**Decisões de escopo fechadas nesta spec (revisão com o usuário antes de
detalhar o contrato):**

1. **`month` é obrigatório**, formato `YYYY-MM` (mesmo formato do filtro
   `yearMonth` já usado em `GET /transactions` desde a FEAT-06). Sem o
   parâmetro, ou em formato inválido, a API retorna 400 — sem default
   para "mês corrente", para não depender implicitamente da data do
   servidor.
2. **`saldoCents` = `receitasCents` - `gastoCents`** do mês consultado
   (pode ser negativo). **`restanteCents` = `orcamentoTotalCents` -
   `gastoCents`** (também pode ser negativo, quando o gasto ultrapassa
   o orçamento total — sem tratamento especial, o frontend decide como
   exibir estouro).
3. **`orcamentoTotalCents`** é a soma de `orcamentoMensalCents` de
   **todas as categorias de tipo `despesa` da conta que têm orçamento
   definido** (categorias sem orçamento não entram na soma; categorias
   de tipo `receita` nunca entram, mesmo que tenham
   `orcamentoMensalCents` definido). Não depende do mês — orçamento é
   um valor recorrente por categoria (FEAT-21), não um dado histórico
   do mês consultado.
4. **`porCategoria`** lista **somente categorias de tipo `despesa` com
   `orcamentoMensalCents` definido** (mesmo critério do item 3) — a
   barra de progresso do mockup só faz sentido tendo um orçamento pra
   comparar. Toda categoria nessas condições aparece, mesmo com
   `gastoCents=0` no mês (reflete o orçamento zerado, não é omitida).
   Categorias de tipo `despesa` sem orçamento definido, e todas as de
   tipo `receita`, não aparecem nesta lista (mas ainda contam para
   `gastoCents`/`receitasCents` do resumo geral). Ordenada por
   `gastoCents` decrescente (categoria com maior gasto no mês primeiro).
5. **`ultimosLancamentos`** traz as **5 transações mais recentes do mês
   consultado** (`despesa` e `receita` misturadas, mais recente
   primeiro — mesmo critério de ordenação de `GET /transactions`), no
   mesmo formato de item já retornado por `GET /transactions` (`id`,
   `description`, `amountInCents`, `categoryId`, `tipo`, `date`,
   `createdByUserId`, `createdByLabel`, `createdAt`). Sem paginação —
   é sempre um recorte fixo de até 5 itens; "ver todas" no frontend
   navega para `GET /transactions?yearMonth=...`, fora do escopo desta
   rota.
6. **Mês sem nenhuma transação/categoria com orçamento retorna 200 com
   valores zerados** (`saldoCents=0`, `receitasCents=0`, `gastoCents=0`,
   `orcamentoTotalCents=0`, `restanteCents=0`, `porCategoria=[]`,
   `ultimosLancamentos=[]`) — não é erro nem 404, o resumo é sempre
   calculável para qualquer mês válido.
7. **Somente leitura, acessível a qualquer papel autenticado da conta**
   (`Leitura`, `Lancar`, `Total`, `Titular`) — mesmo padrão de
   `GET /transactions`/`GET /categories`, sem restrição adicional.

## Requisitos de negócio

- `month`: obrigatório em `GET /summary`, formato `YYYY-MM`; ausente,
  vazio ou fora do formato (incluindo mês inválido, ex.: `2026-13`)
  retorna 400
- Toda agregação é escopada à conta ativa do chamador (`accountId`
  resolvido do JWT, nunca do body) — nunca mistura dados de outra conta
- `receitasCents`: soma de `amountInCents` de todas as transações
  `tipo="receita"` da conta cuja `date` cai no `month` informado
- `gastoCents`: soma de `amountInCents` de todas as transações
  `tipo="despesa"` da conta cuja `date` cai no `month` informado
- `saldoCents = receitasCents - gastoCents`
- `orcamentoTotalCents`: soma de `orcamentoMensalCents` de todas as
  categorias `tipo="despesa"` da conta com orçamento definido
  (independe do `month` informado)
- `restanteCents = orcamentoTotalCents - gastoCents`
- `porCategoria`: uma entrada por categoria `tipo="despesa"` da conta
  com orçamento definido, contendo o `gastoCents` daquela categoria no
  `month` informado (soma de `amountInCents` das transações dessa
  categoria no mês, `0` se nenhuma) e o `orcamentoMensalCents` da
  categoria; ordenada por `gastoCents` decrescente
- `ultimosLancamentos`: até 5 transações (qualquer `tipo`) da conta com
  `date` no `month` informado, ordenadas da mais recente para a mais
  antiga, no mesmo formato de item de `GET /transactions`
- Qualquer papel autenticado da conta ativa pode consultar
  `GET /summary` (sem exigir `Total`/`Titular`)

## User Stories

**US1 — Consultar resumo de um mês com dados**
- Given um usuário autenticado com receitas, despesas e categorias com
  orçamento definido na conta ativa, todas com `date`/vínculo no mês
  `2026-08`
- When ele consulta `GET /summary?month=2026-08`
- Then a API retorna 200 com `receitasCents`/`gastoCents` somando
  corretamente as transações daquele mês, `saldoCents` igual à
  diferença entre eles, `orcamentoTotalCents` somando o orçamento das
  categorias de despesa, e `restanteCents` igual à diferença entre
  orçamento total e gasto

**US2 — Rejeitar mês ausente**
- Given um usuário autenticado
- When ele consulta `GET /summary` sem o parâmetro `month`
- Then a API retorna 400 e nenhum dado é retornado

**US3 — Rejeitar formato de mês inválido**
- Given um usuário autenticado
- When ele consulta `GET /summary?month=2026-13` (ou qualquer valor fora
  do formato `YYYY-MM`, ex.: `2026/08`, `agosto-2026`)
- Then a API retorna 400 e nenhum dado é retornado

**US4 — Mês sem nenhuma transação**
- Given um usuário autenticado sem nenhuma transação registrada no mês
  `2026-01`
- When ele consulta `GET /summary?month=2026-01`
- Then a API retorna 200 com `saldoCents=0`, `receitasCents=0`,
  `gastoCents=0` e `ultimosLancamentos=[]` (categorias com orçamento,
  se existirem, ainda aparecem em `porCategoria` com `gastoCents=0`)

**US5 — Por categoria só inclui despesas com orçamento definido**
- Given um usuário autenticado com três categorias de despesa — uma com
  orçamento definido e gasto no mês, uma com orçamento definido e sem
  gasto no mês, e uma sem orçamento definido mas com gasto no mês — e
  uma categoria de receita com orçamento definido
- When ele consulta `GET /summary?month=2026-08`
- Then `porCategoria` traz somente as duas categorias de despesa com
  orçamento definido (uma com `gastoCents > 0`, outra com
  `gastoCents = 0`), e não inclui a categoria de despesa sem orçamento
  nem a categoria de receita

**US6 — Por categoria ordenada por gasto decrescente**
- Given um usuário autenticado com duas ou mais categorias de despesa
  com orçamento definido e gastos diferentes no mês consultado
- When ele consulta `GET /summary?month=2026-08`
- Then `porCategoria` retorna as categorias ordenadas da que mais gastou
  no mês para a que menos gastou

**US7 — Últimos lançamentos limitados a 5 e ordenados**
- Given um usuário autenticado com mais de 5 transações registradas no
  mês consultado (despesas e receitas misturadas)
- When ele consulta `GET /summary?month=2026-08`
- Then `ultimosLancamentos` retorna exatamente as 5 transações mais
  recentes daquele mês, ordenadas da mais recente para a mais antiga

**US8 — Restante negativo quando o gasto ultrapassa o orçamento total**
- Given um usuário autenticado cujo `gastoCents` do mês é maior que
  `orcamentoTotalCents`
- When ele consulta `GET /summary?month=2026-08`
- Then `restanteCents` é retornado como um valor negativo, sem erro

**US9 — Isolamento entre contas**
- Given dois usuários autenticados em contas diferentes, cada um com
  suas próprias transações e categorias
- When cada um consulta `GET /summary?month=2026-08`
- Then a resposta de cada um reflete apenas os dados da sua própria
  conta ativa

**US10 — Acesso liberado para qualquer papel**
- Given um usuário autenticado com papel `Leitura` na conta ativa
- When ele consulta `GET /summary?month=2026-08`
- Then a API retorna 200 normalmente (sem 403), mesmo comportamento para
  `Lancar`, `Total` e `Titular`

**US11 — Impedir consulta sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta `GET /summary?month=2026-08`
- Then a API retorna 401 e nenhum dado é retornado

## Contratos da API

### GET /summary

Query params:

| Param | Tipo | Formato | Obrigatório |
|---|---|---|---|
| `month` | string | `YYYY-MM` | sim |

Response 200:
```json
{
  "month": "2026-08",
  "saldoCents": 394720,
  "receitasCents": 520000,
  "gastoCents": 125280,
  "orcamentoTotalCents": 299000,
  "restanteCents": 173720,
  "porCategoria": [
    {
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "nome": "Alimentacao",
      "gastoCents": 30670,
      "orcamentoMensalCents": 80000
    }
  ],
  "ultimosLancamentos": [
    {
      "id": "...",
      "description": "Supermercado Pão de Açúcar",
      "amountInCents": 18790,
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "tipo": "despesa",
      "date": "2026-08-24",
      "createdByUserId": "a1b2c3d4-...",
      "createdByLabel": "Você",
      "createdAt": "2026-08-24T18:12:00Z"
    }
  ]
}
```

Response 400 (validation-error): `month` ausente, vazio ou fora do
formato `YYYY-MM` (inclui mês inválido, ex.: `2026-13`).
Response 401 (unauthorized).

Sem 403 (qualquer papel autenticado da conta ativa pode consultar), sem
404 (mês sem dados retorna 200 com valores zerados, nunca "não
encontrado").

### Erros comuns a todas as rotas

Formato padrão de erro do projeto (`ResultHttpExtensions.BuildProblem`):
`title` fixo e genérico por tipo de erro (RFC 9457), mensagem
específica sempre em `detail`. Fonte de verdade exata:
`backend/docs/openapi.json`.

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "O parâmetro month é obrigatório e deve estar no formato YYYY-MM."
}
```

Response 401 (unauthorized):
```json
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Não autorizado",
  "status": 401
}
```

## Critérios de aceite

- [x] `GET /summary?month=YYYY-MM` retorna 200 com `saldoCents`,
      `receitasCents`, `gastoCents`, `orcamentoTotalCents`,
      `restanteCents`, `porCategoria` e `ultimosLancamentos` calculados
      corretamente a partir das transações/categorias da conta ativa
- [x] `GET /summary` sem `month` retorna 400
- [x] `GET /summary?month=` com formato inválido (incluindo mês fora de
      01-12) retorna 400
- [x] Mês sem nenhuma transação retorna 200 com todos os totais
      zerados e `ultimosLancamentos=[]`
- [x] `porCategoria` inclui somente categorias `tipo="despesa"` com
      `orcamentoMensalCents` definido, mesmo com `gastoCents=0`;
      exclui despesas sem orçamento e qualquer categoria de receita
- [x] `porCategoria` retorna ordenada por `gastoCents` decrescente
- [x] `ultimosLancamentos` retorna no máximo 5 itens, os mais recentes
      do mês consultado, ordenados da mais recente para a mais antiga
- [x] `restanteCents` pode ser negativo quando `gastoCents` ultrapassa
      `orcamentoTotalCents`, sem erro
- [x] Dados de uma conta nunca aparecem no resumo de outra conta
- [x] Qualquer papel autenticado (`Leitura`, `Lancar`, `Total`,
      `Titular`) recebe 200 em `GET /summary`
- [x] Requisição sem token JWT válido retorna 401
- [x] `backend/docs/openapi.json` regenerado refletindo o novo endpoint
      `GET /summary` (parâmetro `month`, schema de response, `400`/`401`)

## Status

Implementado conforme `plan.md`/`tasks.md`. Novo módulo
`Summary/Queries/GetSummary/` (Application) — sem mudança em Domain nem
Infrastructure, reaproveitando `ITransactionRepository.QueryAsync`
(filtro `YearMonth`, `Limit = int.MaxValue` — busca o mês inteiro, sem
cap de negócio, ver `plan.md`/"Contexto técnico") e
`ICategoryRepository.ListAsync(accountId, "despesa")`. `porCategoria`/
`orcamentoTotalCents` filtram só categorias `tipo="despesa"` com
`orcamentoMensalCents` definido; `ultimosLancamentos` reaproveita
`TransactionSummary`/`CreatedByLabelResolver` do módulo `Transactions`
(mesmo cache-por-request de `GetTransactionsQueryHandler`), sem
duplicar o shape de item.

Novo `GET /summary` (`SummaryEndpoints`) sem `RoleEndpointFilters.Require`
— qualquer papel autenticado da conta ativa passa, mesmo padrão de
`GET /transactions`/`GET /categories`. Nenhum recurso AWS novo.

Durante os testes de componente, um bug foi encontrado e corrigido no
próprio `GetSummaryQueryValidator`: o `.When()` originalmente encadeado
depois de `Matches()` aplicava a condição a toda a regra (inclusive
`NotEmpty()`), fazendo `month` vazio passar a validação sem erro —
corrigido removendo o `.When()` (`Matches()` já rejeita string vazia
sozinho). O teste de regressão
`ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownEight`
(FEAT-03) foi atualizado para `...BeyondTheKnownNine`, incluindo
`GetSummaryQueryValidator` na lista fechada de validators esperados.

`backend/docs/openapi.json` regenerado localmente (API rodando contra
LocalStack/cognito-local, `backend/infra/`) — `git diff` confirma só a
adição de `/summary` (`GET`, parâmetro `month`, schemas
`GetSummaryResult`/`CategorySummaryItem`, `400`/`401`), sem tocar
`/transactions`, `/categories` ou `/members`.

Suíte completa (`dotnet test` na solução) passa: 508/508 (1
IntegrationTests placeholder + 350 UnitTests + 157 ComponentTests).

## Fora do escopo

- `GET /reports?period=week|month|year` (relatórios por período,
  variação vs. período anterior, maior gasto) — FEAT-24
- Exportação CSV — FEAT-25
- Tabela agregada / DynamoDB Streams / qualquer pré-cálculo persistido
  — decisão já fechada no roadmap: sempre `Query` + agregação em
  memória na própria request
- Cache do resultado entre requests — não avaliado nesta feature
- Resumo de mês futuro com projeção/previsão de gastos — o endpoint só
  agrega dados já lançados, sem nenhuma lógica preditiva
- Orçamento por categoria de tipo `receita` entrar em qualquer soma do
  resumo (`orcamentoTotalCents`/`porCategoria`) — mesmo que a categoria
  tenha `orcamentoMensalCents` definido, o resumo só considera despesa
- Paginação de `ultimosLancamentos` — é sempre um recorte fixo de até 5
  itens; navegação completa continua em `GET /transactions`
