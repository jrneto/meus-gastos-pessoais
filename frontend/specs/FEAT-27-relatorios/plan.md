# Plan — FEAT-27: Relatórios

## Camadas afetadas

Nova feature `features/reports/` (primeira a consumir `GET /reports`),
substituindo `routes/ReportsComingSoonPage.tsx` por
`routes/ReportsPage.tsx` de verdade, e um ajuste pontual em
`styles/modernist/modernist.css` (uma classe do bundle base ainda não
vendorizada). Sem tocar `features/summary`, `features/transactions`,
`features/categories` nem qualquer outra feature — `GET /reports` é
autocontido (não depende de `GET /categories` nesta tela, decisão 2 da
spec).

| Arquivo | O que muda |
|---|---|
| `features/reports/api/reportsApi.ts` (novo) | `reportsApi.getReports(token, period, date)` → `GET /reports?period=&date=`; tipos `ReportPeriod`, `ReportsResponse`, `ReportCategoryItem`, `ReportTopCategory` |
| `features/reports/errors/reportsErrors.ts` (novo) | `SessionExpiredError`, `NetworkError`, `UnknownReportsError` |
| `features/reports/hooks/useReports.ts` (novo) | `useReports(period, date)` → `{ data, isLoading, error }` |
| `features/reports/utils/period.ts` (novo) | `getCurrentDate()` (`YYYY-MM-DD` do dispositivo), `formatComparisonLabel(variacaoPercentual, period)`, `formatPercent(value)` |
| `features/reports/components/PeriodToggle.tsx` (novo) | Seletor Semana/Mês/Ano (`.seg`/`.seg-opt`, já vendorizados desde a FEAT-14) |
| `features/reports/components/CategoryReportList.tsx` (novo) | Lista "Gasto por categoria" (barra proporcional ao maior gasto) + estado vazio |
| `features/reports/components/TotalPeriodCard.tsx` (novo) | Card "Total no período" + linha de comparação condicional |
| `features/reports/components/TopCategoryCard.tsx` (novo) | Card "Maior gasto" + estado vazio |
| `routes/ReportsComingSoonPage.tsx` → `routes/ReportsPage.tsx` (renomeado) | Implementa a tela de verdade: mantém `period` em estado local, busca o relatório, orquestra os componentes acima |
| `app/router.tsx` | Import/uso de `ReportsPage` no lugar de `ReportsComingSoonPage` |
| `styles/modernist/modernist.css` | Nova classe `.card-title` (do bundle base do design system, ainda não vendorizada — usada pelo nome da categoria no card "Maior gasto") |

Não muda: `backend` (tudo já implementado), `components/nav/navConfig.ts`
(item `reports` já é `status: 'active'` desde a FEAT-15, só a página
por trás dele era fake), `lib/categories/*` (não usado nesta tela,
decisão 2 da spec).

## Contratos técnicos

### `features/reports/api/reportsApi.ts`

```ts
export type ReportPeriod = 'week' | 'month' | 'year'

export interface ReportCategoryItem {
  categoryId: string
  nome: string
  gastoCents: number
}

export interface ReportTopCategory {
  categoryId: string
  nome: string
  gastoCents: number
  percentualOrcamento: number | null
}

export interface ReportsResponse {
  period: ReportPeriod
  startDate: string
  endDate: string
  totalCents: number
  variacaoPercentual: number | null
  porCategoria: ReportCategoryItem[]
  maiorGasto: ReportTopCategory | null
}

async function getReports(token: string, period: ReportPeriod, date: string): Promise<ReportsResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/reports?period=${period}&date=${date}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response) // 401 → SessionExpiredError; !ok → UnknownReportsError
  return response.json() as Promise<ReportsResponse>
}

export const reportsApi = { getReports }
```

Mesmo padrão de `safeFetch`/`assertOk` de `summaryApi.ts` — sem checagem
de `400` dedicada (não esperado em uso normal, `period` vem de um
seletor fechado e `date` é sempre calculada pelo client).

### `features/reports/hooks/useReports.ts`

```ts
interface UseReportsResult {
  data: ReportsResponse | null
  isLoading: boolean
  error: Error | null
}

export function useReports(period: ReportPeriod, date: string): UseReportsResult {
  // busca ao montar e sempre que `period`/`date` mudarem (useEffect com
  // [period, date, token] nas deps) — mesmo esqueleto de useSummary.ts,
  // sem `refetch`: esta tela não tem nenhuma ação que crie/edite dado
  // (decisão 6 da spec, sem interação nos itens; sem botão de nova
  // transação no design desta tela), então nada dispararia um refetch
}
```

### `features/reports/utils/period.ts`

```ts
export function getCurrentDate(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

const PREVIOUS_PERIOD_LABEL: Record<ReportPeriod, string> = {
  week: 'semana passada',
  month: 'mês passado',
  year: 'ano passado',
}

// 1 casa decimal, vírgula (pt-BR) — mesma convenção de `formatCentsToCurrency`;
// `12` continua `"12"` (sem ".0"/",0" à toa), `54.4` vira `"54,4"`.
export function formatPercent(value: number): string {
  return value.toLocaleString('pt-BR', { maximumFractionDigits: 1 })
}

// Só chamada quando `variacaoPercentual !== null` (ver TotalPeriodCard).
// Sinal `+` explícito pra valores ≥ 0 (`0` inclusive — "sem variação"
// também é informação); `-` já vem embutido no número quando negativo.
export function formatComparisonLabel(variacaoPercentual: number, period: ReportPeriod): string {
  const sign = variacaoPercentual >= 0 ? '+' : ''
  return `${sign}${formatPercent(variacaoPercentual)}% vs ${PREVIOUS_PERIOD_LABEL[period]}`
}
```

Feature-scoped (não `lib/`), mesmo racional já usado pra
`features/summary/utils/month.ts` na FEAT-26 — só um consumidor hoje.

### `features/reports/components/PeriodToggle.tsx`

```tsx
interface PeriodToggleProps {
  value: ReportPeriod
  onChange: (period: ReportPeriod) => void
}
```
Três `<label className="seg-opt">` com `<input type="radio" name="period"
style={{ display: 'none' }} />`, mesmo padrão do `.dc.html` (bloco
`isRep`) — `.seg`/`.seg-opt` já vendorizados desde a FEAT-14 (usados
hoje só em filtros de transação), reaproveitados sem mudança.

### `features/reports/components/CategoryReportList.tsx`

```tsx
interface CategoryReportListProps {
  items: ReportCategoryItem[]
}
```
`items.length === 0` → mensagem de estado vazio ("Nenhuma despesa neste
período.", decisão 4 da spec). Caso contrário, `maxGastoCents =
items[0].gastoCents` (lista já vem ordenada decrescente pelo backend);
por item, largura da barra = `Math.round((item.gastoCents /
maxGastoCents) * 100)}%`, sempre `background: var(--color-neutral-800)`
(decisão 2 da spec — sem cruzar com `/categories` pra colorir por
orçamento).

### `features/reports/components/TotalPeriodCard.tsx`

```tsx
interface TotalPeriodCardProps {
  totalCents: number
  variacaoPercentual: number | null
  period: ReportPeriod
}
```
`.card.elev-sm` com `.card-kicker` "Total no período" +
`formatCentsToCurrency(totalCents)`; linha de comparação
(`formatComparisonLabel`) só renderizada quando `variacaoPercentual !==
null` (decisão 3 da spec).

### `features/reports/components/TopCategoryCard.tsx`

```tsx
interface TopCategoryCardProps {
  category: ReportTopCategory | null
}
```
`.card.elev-sm` com `.card-kicker` "Maior gasto". Quando `category` é
`null` → texto genérico "Nenhum gasto registrado" (decisão 4).
Caso contrário: `.card-title` com `category.nome`, e
`formatCentsToCurrency(category.gastoCents)` + (`" · " +
formatPercent(percentualOrcamento) + "% do orçamento"` só quando
`percentualOrcamento !== null`, decisão 3 originalmente pensada só pra
variação, mas o mesmo princípio de "esconder o que não é computável"
vale aqui pra orçamento ausente).

### `routes/ReportsPage.tsx` (era `ReportsComingSoonPage.tsx`)

```tsx
export function ReportsPage() {
  const [period, setPeriod] = useState<ReportPeriod>('month')
  const date = getCurrentDate() // recalculado a cada render, mesmo padrão de getCurrentYearMonth() na DashboardPage

  const { data, isLoading, error } = useReports(period, date)

  // cabeçalho: "Relatórios" + <PeriodToggle value={period} onChange={setPeriod} />

  // grid 2 colunas (1.3fr / 1fr, mesma proporção do .dc.html):
  // esquerda: "Gasto por categoria" + <CategoryReportList items={data.porCategoria} />
  // direita: <TotalPeriodCard .../> + <TopCategoryCard .../>
}
```
Loading/erro seguem o padrão textual já usado nas demais telas
(`"Carregando..."` / mensagem de erro com `error.message`), mesmo
bloco de `DashboardPage.tsx`.

### `styles/modernist/modernist.css` — nova classe

```css
/* — card-title (relatórios, FEAT-27) — do bundle base do design
   system, ainda não vendorizado (só .card/.card-kicker/.elev-sm
   vieram na FEAT-26) */
.ds-modernist .card-title {
  font-family: var(--font-heading);
  font-weight: var(--font-heading-weight);
  font-size: 17px;
  line-height: 1.2;
}
```

## Decisões técnicas

1. **`features/reports` como feature nova**, mesmo racional de
   `summary`/`transactions`/`categories` levarem o nome do próprio
   recurso REST.
2. **Sem `refetch` em `useReports`** — diferente de `useSummary`
   (FEAT-26), que precisa recarregar após criar uma transação pelo
   dashboard. Esta tela não tem nenhuma ação de escrita (decisão 6 da
   spec), então não há gatilho que precise refazer a busca fora de
   `period`/`date` mudarem.
3. **`getCurrentDate()`/`formatComparisonLabel()`/`formatPercent()`
   ficam em `features/reports/utils/`, não em `lib/`** — só um
   consumidor hoje, mesmo racional já usado para `month.ts` (FEAT-26) e
   `currency.ts` antes da FEAT-22.
4. **`PeriodToggle` reaproveita `.seg`/`.seg-opt` sem mudança** — essas
   classes já existem desde a FEAT-14 (hoje usadas só no painel de
   filtros de transação); nenhuma modificação de CSS necessária pra
   elas.
5. **Barra de "Gasto por categoria" não busca `/categories`** — decisão
   2 da spec, já fechada com o usuário: o contrato de `/reports` não
   traz orçamento por categoria em `porCategoria`, e cruzar com
   `/categories` só pra colorir uma barra foi decidido como custo/
   benefício ruim. A barra é sempre neutra, proporcional ao maior gasto
   da própria lista.
6. **`ReportsComingSoonPage.tsx` renomeado para `ReportsPage.tsx`** —
   mesmo racional das renomeações anteriores (`ExpensesListPage` →
   `TransactionsListPage` na FEAT-23, `HomePage` → `DashboardPage` na
   FEAT-26): o componente deixa de ser um placeholder genérico.
7. **`.card-title` vendorizado agora** — única classe do bundle base
   ainda faltando pro padrão de card já em uso desde a FEAT-26; as
   demais (`.card`, `.card-kicker`, `.elev-sm`, `.je-track`/`.je-fill`,
   `.seg`/`.seg-opt`) já existem e são reaproveitadas sem mudança.

## Recursos AWS

Nenhum. Esta feature só consome `GET /reports` (já em produção, backend
FEAT-24) — nenhuma infraestrutura nova.

## Mapeamento de erros

| Origem | Condição | Exceção lançada | Mensagem exibida |
|---|---|---|---|
| `GET /reports` | `401` | `SessionExpiredError` (nova, mesma mensagem padrão) | "Sua sessão expirou. Faça login novamente." — limpa a sessão |
| `GET /reports` | falha de rede | `NetworkError` (nova, mesma mensagem padrão) | "Não foi possível conectar à API. Verifique sua conexão." |
| `GET /reports` | outro status (`400`/`500`, não esperado em uso normal) | `UnknownReportsError` (nova) | "Ocorreu um erro inesperado ao carregar o relatório. Tente novamente." |

## Pontos a confirmar antes do `/tasks`

1. **Formato de `formatComparisonLabel`/`formatPercent`** — optei por 1
   casa decimal condicional via `toLocaleString('pt-BR', {
   maximumFractionDigits: 1 })` (`12` fica `"12"`, `54.4` fica
   `"54,4"`), já que o backend manda até 1 casa decimal
   (`variacaoPercentual`/`percentualOrcamento`) e o protótipo estático
   só mostra inteiros por causa da massa de dados fake (mesmo racional
   já usado na decisão 2 do dashboard sobre `restanteCents` negativo:
   o protótipo simplifica, o contrato real é mais rico). Confirmar se
   esse formato serve ou se prefere sempre arredondar pra inteiro
   (perdendo a precisão que o backend já manda).
