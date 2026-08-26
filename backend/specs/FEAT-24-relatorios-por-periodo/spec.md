# FEAT-24: Relatórios por período

## Objetivo

Expor `GET /reports?period=week|month|year&date=YYYY-MM-DD`, agregando
as despesas e categorias da conta ativa num período (semana, mês ou
ano) que contém a data informada: gasto por categoria, total do
período, variação percentual vs o período anterior equivalente e a
categoria de maior gasto — dados suficientes para renderizar a tela
"Relatórios" do design system sem o frontend precisar buscar todas as
transações do período e agregar client-side.

## Contexto

O design system (`frontend/design-system/screenshots/10-relatorios.png`)
mostra a tela "Relatórios" com um seletor Semana/Mês/Ano, uma lista
"Gasto por categoria" (nome + barra proporcional + valor, ordenada do
maior pro menor gasto), um card "Total no período" (valor + variação
percentual textual, ex.: "+12% vs mês passado") e um card "Maior gasto"
(categoria + valor + percentual do orçamento daquela categoria, ex.:
"54% do orçamento"). Esses números hoje só existem espalhados em
`/transactions` (FEAT-22) e `/categories` (FEAT-21) — nenhuma rota
atual agrega por período nem calcula variação vs período anterior.

Segue `backend/docs/roadmap.md` (item "FEAT-24 — Relatórios por
período") e a mesma decisão de modelagem do FEAT-23: **sem tabela
agregada nem DynamoDB Streams** — o relatório é sempre calculado via
`Query` do período (e do período anterior, pra variação) + agregação em
memória na própria request.

**Decisões de escopo fechadas nesta spec (revisão com o usuário antes
de detalhar o contrato):**

1. **`period` e `date` são ambos obrigatórios**, sem default de "hoje" —
   mesma filosofia do `/summary` (`month` obrigatório): a API nunca
   depende implicitamente da data do servidor. `date` (formato
   `YYYY-MM-DD`) é a data de referência que ancora o período; `period`
   (`week`\|`month`\|`year`) escolhe a granularidade. O mockup não tem
   seletor de data porque o frontend, hoje, sempre chamaria com a data
   atual — mas isso é decisão do frontend (ainda não iniciado), fora do
   escopo desta spec.
2. **Cálculo do período a partir de `date`:**
   - `period=week`: semana ISO (segunda a domingo) que contém `date`.
   - `period=month`: mês calendário que contém `date`.
   - `period=year`: ano calendário que contém `date`.
3. **Período anterior** (para a variação): o período imediatamente
   anterior, de mesmo tipo e duração — semana ISO anterior, mês
   calendário anterior ou ano calendário anterior, conforme `period`.
4. **`totalCents`** é a soma de `amountInCents` de todas as transações
   `tipo="despesa"` da conta cuja `date` cai no período consultado
   (mesmo critério de soma usado em `gastoCents` no `/summary`, mas
   escopado ao período, não a um mês fixo). Receitas nunca entram nesta
   soma — este endpoint é só sobre gasto, diferente do `/summary`, que
   já cobre receitas/saldo.
5. **`porCategoria`** lista toda categoria `tipo="despesa"` da conta com
   `gastoCents > 0` no período consultado (diferente do `/summary`, que
   filtra por orçamento definido — aqui o critério é ter gasto real no
   período, com ou sem orçamento configurado), ordenada por
   `gastoCents` decrescente. Categorias sem gasto no período não
   aparecem (bate com o mockup: nenhuma barra zerada).
6. **`variacaoPercentual`** = `((totalCents - totalCentsPeríodoAnterior)
   / totalCentsPeríodoAnterior) * 100`, arredondado a 1 casa decimal.
   Regra pra divisão por zero: se o período anterior teve
   `totalCents = 0` e o período atual também teve `totalCents = 0`,
   `variacaoPercentual = 0`. Se o período anterior teve
   `totalCents = 0` e o período atual teve gasto (`> 0`), a variação
   não é computável matematicamente — `variacaoPercentual` retorna
   `null`.
7. **`maiorGasto`** é a categoria com maior `gastoCents` no período
   (primeiro item de `porCategoria`, já ordenado) — `null` quando não
   há nenhuma despesa no período (`porCategoria` vazio). Inclui
   `percentualOrcamento` = `(gastoCents da categoria /
   orcamentoMensalCents da categoria) * 100`, arredondado a 1 casa
   decimal, **`null` quando a categoria não tem `orcamentoMensalCents`
   definido** (o card ainda mostra categoria e valor gasto normalmente).
8. **Somente leitura, acessível a qualquer papel autenticado da conta**
   (`Leitura`, `Lancar`, `Total`, `Titular`) — mesmo padrão de
   `GET /summary`/`GET /transactions`, sem restrição adicional.

## Requisitos de negócio

- `period`: obrigatório em `GET /reports`, um dos valores exatos
  `week`, `month` ou `year`; ausente ou qualquer outro valor retorna 400
- `date`: obrigatório, formato `YYYY-MM-DD`, precisa ser uma data de
  calendário válida (ex.: `2026-02-30` é inválida); ausente, vazio ou
  fora do formato retorna 400
- Toda agregação é escopada à conta ativa do chamador (`accountId`
  resolvido do JWT, nunca do body) — nunca mistura dados de outra conta
- `totalCents`: soma de `amountInCents` de todas as transações
  `tipo="despesa"` da conta cuja `date` cai dentro do período calculado
  a partir de `period`+`date`
- `porCategoria`: uma entrada por categoria `tipo="despesa"` da conta
  com `gastoCents > 0` no período (soma de `amountInCents` das
  transações dessa categoria no período), ordenada por `gastoCents`
  decrescente; categorias sem gasto no período não aparecem
- `variacaoPercentual`: percentual de variação de `totalCents` vs o
  total do período imediatamente anterior de mesma duração; `null`
  quando o período anterior teve total zero e o período atual teve
  gasto (variação não computável); `0` quando os dois períodos têm
  total zero
- `maiorGasto`: a categoria de `porCategoria` com maior `gastoCents`
  (`null` se `porCategoria` estiver vazio), com `percentualOrcamento`
  calculado sobre o `orcamentoMensalCents` da própria categoria
  (`null` se a categoria não tiver orçamento definido)
- Qualquer papel autenticado da conta ativa pode consultar
  `GET /reports` (sem exigir `Total`/`Titular`)

## User Stories

**US1 — Consultar relatório mensal com dados**
- Given um usuário autenticado com despesas em várias categorias da
  conta ativa, todas com `date` dentro de agosto/2026
- When ele consulta `GET /reports?period=month&date=2026-08-15`
- Then a API retorna 200 com `totalCents` somando todas as despesas de
  agosto/2026, `porCategoria` com uma entrada por categoria com gasto,
  ordenada por `gastoCents` decrescente, e `maiorGasto` apontando pra
  categoria do topo dessa lista

**US2 — Consultar relatório semanal (semana ISO)**
- Given um usuário autenticado com despesas de segunda a domingo de uma
  semana específica
- When ele consulta `GET /reports?period=week&date=2026-08-19` (uma
  quarta-feira)
- Then a API retorna 200 com `totalCents`/`porCategoria` considerando
  somente despesas entre a segunda (2026-08-17) e o domingo
  (2026-08-23) daquela semana ISO

**US3 — Consultar relatório anual**
- Given um usuário autenticado com despesas em meses diferentes de 2026
- When ele consulta `GET /reports?period=year&date=2026-08-15`
- Then a API retorna 200 com `totalCents`/`porCategoria` somando todas
  as despesas com `date` entre 2026-01-01 e 2026-12-31

**US4 — Rejeitar period ausente ou inválido**
- Given um usuário autenticado
- When ele consulta `GET /reports?date=2026-08-15` (sem `period`) ou
  `GET /reports?period=dia&date=2026-08-15` (valor fora de
  `week`/`month`/`year`)
- Then a API retorna 400 e nenhum dado é retornado

**US5 — Rejeitar date ausente ou inválida**
- Given um usuário autenticado
- When ele consulta `GET /reports?period=month` (sem `date`) ou
  `GET /reports?period=month&date=2026-02-30` (data de calendário
  inválida)
- Then a API retorna 400 e nenhum dado é retornado

**US6 — Variação percentual positiva vs período anterior**
- Given um usuário autenticado cujo total de despesas de agosto/2026 é
  maior que o de julho/2026
- When ele consulta `GET /reports?period=month&date=2026-08-15`
- Then `variacaoPercentual` retorna um valor positivo correspondente ao
  aumento percentual entre os dois totais

**US7 — Variação percentual negativa vs período anterior**
- Given um usuário autenticado cujo total de despesas do período
  consultado é menor que o do período anterior
- When ele consulta `GET /reports` com esse período
- Then `variacaoPercentual` retorna um valor negativo, sem erro

**US8 — Variação não computável quando período anterior não teve gasto**
- Given um usuário autenticado sem nenhuma despesa no mês anterior ao
  consultado, mas com despesas no mês consultado
- When ele consulta `GET /reports?period=month&date=2026-08-15`
- Then `variacaoPercentual` retorna `null`, e os demais campos
  (`totalCents`, `porCategoria`, `maiorGasto`) continuam calculados
  normalmente

**US9 — Período consultado e anterior sem nenhuma despesa**
- Given um usuário autenticado sem nenhuma despesa registrada no
  período consultado nem no período anterior
- When ele consulta `GET /reports` com esse período
- Then a API retorna 200 com `totalCents=0`, `porCategoria=[]`,
  `maiorGasto=null` e `variacaoPercentual=0`

**US10 — Maior gasto com percentual do orçamento**
- Given um usuário autenticado cuja categoria de maior gasto no período
  tem `orcamentoMensalCents` definido
- When ele consulta `GET /reports` com esse período
- Then `maiorGasto.percentualOrcamento` retorna o percentual do
  `gastoCents` dessa categoria sobre o `orcamentoMensalCents` dela

**US11 — Maior gasto sem orçamento definido**
- Given um usuário autenticado cuja categoria de maior gasto no período
  não tem `orcamentoMensalCents` definido
- When ele consulta `GET /reports` com esse período
- Then `maiorGasto` retorna normalmente com `categoryId`/`nome`/
  `gastoCents`, e `percentualOrcamento=null`

**US12 — Gasto por categoria ordenado e sem categorias zeradas**
- Given um usuário autenticado com três categorias de despesa no
  período — duas com gasto (valores diferentes) e uma sem nenhum gasto
  no período
- When ele consulta `GET /reports` com esse período
- Then `porCategoria` traz somente as duas categorias com gasto,
  ordenadas da que mais gastou pra que menos gastou, sem incluir a
  categoria zerada

**US13 — Isolamento entre contas**
- Given dois usuários autenticados em contas diferentes, cada um com
  suas próprias despesas
- When cada um consulta `GET /reports` com o mesmo `period`/`date`
- Then a resposta de cada um reflete apenas os dados da sua própria
  conta ativa

**US14 — Acesso liberado para qualquer papel**
- Given um usuário autenticado com papel `Leitura` na conta ativa
- When ele consulta `GET /reports` com um `period`/`date` válidos
- Then a API retorna 200 normalmente (sem 403), mesmo comportamento para
  `Lancar`, `Total` e `Titular`

**US15 — Impedir consulta sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta `GET /reports` com um `period`/`date` válidos
- Then a API retorna 401 e nenhum dado é retornado

## Contratos da API

### GET /reports

Query params:

| Param | Tipo | Formato | Obrigatório |
|---|---|---|---|
| `period` | string | `week`\|`month`\|`year` | sim |
| `date` | string | `YYYY-MM-DD` | sim |

Response 200:
```json
{
  "period": "month",
  "startDate": "2026-08-01",
  "endDate": "2026-08-31",
  "totalCents": 138120,
  "variacaoPercentual": 12.0,
  "porCategoria": [
    {
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "nome": "Alimentacao",
      "gastoCents": 43510
    },
    {
      "categoryId": "8a4f0b21-5c3d-4e2b-9f8a-3d2c4b5e6f70",
      "nome": "Moradia",
      "gastoCents": 31020
    }
  ],
  "maiorGasto": {
    "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
    "nome": "Alimentacao",
    "gastoCents": 43510,
    "percentualOrcamento": 54.4
  }
}
```

`variacaoPercentual` pode ser `null` (período anterior sem gasto,
período atual com gasto). `maiorGasto` pode ser `null` (nenhuma despesa
no período). `maiorGasto.percentualOrcamento` pode ser `null` (categoria
sem orçamento definido).

Response 400 (validation-error): `period` ausente ou fora de
`week`/`month`/`year`; `date` ausente, vazia ou fora do formato
`YYYY-MM-DD` (inclui data de calendário inválida, ex.: `2026-02-30`).
Response 401 (unauthorized).

Sem 403 (qualquer papel autenticado da conta ativa pode consultar), sem
404 (período sem dados retorna 200 com valores zerados, nunca "não
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
  "detail": "O parâmetro period é obrigatório e deve ser week, month ou year."
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

- [x] `GET /reports?period=week&date=YYYY-MM-DD` retorna 200 com
      `totalCents`/`porCategoria`/`maiorGasto`/`variacaoPercentual`
      calculados a partir da semana ISO (segunda a domingo) que contém
      `date`
- [x] `GET /reports?period=month&date=YYYY-MM-DD` calcula o período
      pelo mês calendário que contém `date`
- [x] `GET /reports?period=year&date=YYYY-MM-DD` calcula o período pelo
      ano calendário que contém `date`
- [x] `GET /reports` sem `period`, ou com `period` fora de
      `week`/`month`/`year`, retorna 400
- [x] `GET /reports` sem `date`, ou com `date` fora do formato
      `YYYY-MM-DD` (incluindo data de calendário inválida), retorna 400
- [x] `porCategoria` inclui somente categorias `tipo="despesa"` com
      `gastoCents > 0` no período, ordenadas por `gastoCents`
      decrescente, sem categorias zeradas
- [x] `maiorGasto` reflete a categoria do topo de `porCategoria`, com
      `percentualOrcamento` calculado sobre o orçamento da própria
      categoria, ou `null` quando ela não tem orçamento definido
- [x] `maiorGasto` retorna `null` quando não há nenhuma despesa no
      período
- [x] `variacaoPercentual` reflete corretamente aumento e redução em
      relação ao período anterior de mesma duração
- [x] `variacaoPercentual` retorna `null` quando o período anterior tem
      total zero e o período atual tem gasto; retorna `0` quando os
      dois têm total zero
- [x] Dados de uma conta nunca aparecem no relatório de outra conta
- [x] Qualquer papel autenticado (`Leitura`, `Lancar`, `Total`,
      `Titular`) recebe 200 em `GET /reports`
- [x] Requisição sem token JWT válido retorna 401
- [x] `backend/docs/openapi.json` regenerado refletindo o novo endpoint
      `GET /reports` (parâmetros `period`/`date`, schema de response,
      `400`/`401`)

## Status

Implementado conforme `plan.md`/`tasks.md`. Novo módulo
`Reports/` (Application) — sem mudança em Domain nem Infrastructure,
reaproveitando `ITransactionRepository.QueryAsync` (com `DateFrom`/
`DateTo`, já suportado pelo repositório) e
`ICategoryRepository.ListAsync(accountId, "despesa")`.
`PeriodCalculator` (função pura, sem clock) calcula início/fim do
período atual e do anterior a partir de `date`, usando
`System.Globalization.ISOWeek` para a semana ISO. O Handler faz duas
chamadas a `QueryAsync` por request — período atual (com agrupamento
por categoria) e período anterior (só para o total, pra
`variacaoPercentual`) — cada uma já filtrando `Tipo="despesa"` na
própria query, sem precisar descartar receitas em memória.

Novo `GET /reports` (`ReportEndpoints`) sem `RoleEndpointFilters.Require`
— qualquer papel autenticado da conta ativa passa, mesmo padrão de
`GET /summary`/`GET /transactions`/`GET /categories`. Nenhum recurso AWS
novo.

Durante os testes de componente, um bug foi encontrado e corrigido no
próprio `GetReportsQueryValidator`: o `.When()` originalmente encadeado
depois de `Must(BeAValidDate)` aplicava a condição a toda a regra
(inclusive `NotEmpty()`), fazendo `date` vazio passar a validação sem
erro — mesma classe de bug já corrigida na FEAT-23
(`GetSummaryQueryValidator`). Corrigido removendo o `.When()`
(`Must(BeAValidDate)` já rejeita string vazia sozinho, já que
`DateOnly.TryParseExact("", ...)` retorna `false`). O teste de
regressão
`ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownNine`
(FEAT-03) foi atualizado para `...BeyondTheKnownTen`, incluindo
`GetReportsQueryValidator` na lista fechada de validators esperados.

`backend/docs/openapi.json` regenerado localmente (API rodando contra
LocalStack/cognito-local, `backend/infra/`) — `git diff` confirma só a
adição de `/reports` (`GET`, parâmetros `period`/`date`, schemas
`GetReportsResult`/`ReportCategoryItem`/`ReportTopCategory`,
`400`/`401`), sem tocar `/transactions`, `/categories`, `/members` ou
`/summary` (0 linhas removidas no diff).

Suíte completa (`dotnet test` na solução) passa: 558/558 (1
IntegrationTests placeholder + 379 UnitTests + 178 ComponentTests).

## Fora do escopo

- Histórico de múltiplos períodos anteriores numa única resposta (ex.:
  série de 12 meses) — cada request calcula só o período consultado e
  o imediatamente anterior, só pra variação
- Receitas no relatório — este endpoint é só sobre despesas; saldo e
  receitas já são cobertos por `GET /summary` (FEAT-23)
- Filtro por categoria específica — a resposta sempre traz todas as
  categorias de despesa com gasto no período
- Renderização de gráfico/barra — a API retorna só os valores
  agregados; a barra proporcional do mockup é cálculo do frontend
- Exportação (CSV) — FEAT-25
- Tabela agregada / DynamoDB Streams / qualquer pré-cálculo persistido
  — mesma decisão já fechada no roadmap: sempre `Query` + agregação em
  memória na própria request
- Seletor de data no frontend (o mockup hoje não expõe um, sempre
  chamaria com a data atual) — decisão de UI que cabe à spec do
  frontend quando ele for iniciado, não a este contrato de API
