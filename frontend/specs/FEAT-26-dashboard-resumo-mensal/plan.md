# Plan — FEAT-26: Dashboard (Início) — resumo mensal

## Camadas afetadas

Nova feature `features/summary/` (primeira a consumir `GET /summary`),
a rota `HomePage`/`DashboardPage`, `app/router.tsx`,
`styles/modernist/modernist.css` (primeiras classes de card/barra de
progresso) e um ajuste em `features/transactions` +
`routes/TransactionsListPage.tsx` pra sustentar o "Ver todas" filtrado
por mês (decisão 3 da spec).

| Arquivo | O que muda |
|---|---|
| `features/summary/api/summaryApi.ts` (novo) | `summaryApi.getSummary(token, month)` → `GET /summary?month=`; tipos `SummaryResponse`, `CategorySummaryItem`, `SummaryTransactionItem` |
| `features/summary/errors/summaryErrors.ts` (novo) | `SessionExpiredError`, `NetworkError`, `UnknownSummaryError` |
| `features/summary/hooks/useSummary.ts` (novo) | `useSummary(month)` → `{ data, isLoading, error, refetch }` |
| `features/summary/utils/month.ts` (novo) | `getCurrentYearMonth()` (`YYYY-MM` do dispositivo), `formatMonthLabel(month)` (`"Agosto de 2026"`) |
| `features/summary/components/SummaryCards.tsx` (novo) | Os 5 cartões (Saldo/Receitas/Gasto/Orçamento total/Restante) + barra de progresso do Restante |
| `features/summary/components/CategorySpendingList.tsx` (novo) | Lista "Onde o dinheiro foi este mês" (barra por categoria) + estado vazio |
| `features/summary/components/RecentTransactionsList.tsx` (novo) | Lista "Últimos lançamentos" (usa `useCategories`/`CategoryLetterTile` de `lib/categories`) + estado vazio |
| `routes/HomePage.tsx` → `routes/DashboardPage.tsx` (renomeado) | Implementa a tela de verdade: busca o resumo, orquestra os componentes acima + `TransactionFormDialog` (de `features/transactions`, reaproveitado) |
| `app/router.tsx` | Import/uso de `DashboardPage` no lugar de `HomePage` |
| `components/nav/navConfig.ts` | Item `home`: `status: 'placeholder'` → `'active'` (metadata não usada em nenhuma renderização — busquei — mas fica incorreta se não atualizar) |
| `styles/modernist/modernist.css` | Novo: token `--shadow-sm`; classes `.card`/`.card-kicker`/`.elev-sm` (do bundle base do design system, ainda não vendorizadas); `.je-track`/`.je-fill` (barra de progresso — são classes do próprio `.dc.html`, não do bundle base, mas necessárias pro padrão visual) |
| `features/transactions/hooks/useTransactionsQuery.ts` | Ganha parâmetro opcional `initialFilters: GetTransactionsParams = {}`, usado na busca inicial (mount) no lugar do `{}` fixo |
| `features/transactions/components/TransactionFilters.tsx` | Ganha prop opcional `initialValues: Partial<TransactionFilterInput>`; painel "Filtros avançados" abre já expandido quando há valor inicial |
| `routes/TransactionsListPage.tsx` | Lê `yearMonth` da query string (`useSearchParams`, react-router-dom) e repassa como filtro inicial pros dois itens acima |

Não muda: `backend` (tudo já implementado), `features/categories/*`,
`lib/currency.ts`, `lib/httpClient`, demais componentes de
`features/transactions/*` além dos dois listados acima.

**`features/summary` nunca importa de `features/transactions`** (regra
da constitution: "uma feature nunca importa de dentro de outra
feature") — a composição entre as duas (abrir `TransactionFormDialog`
a partir do dashboard) acontece só em `routes/DashboardPage.tsx`, a
camada de rotas, que já tem esse papel hoje (`TransactionsListPage.tsx`
compõe vários componentes de `features/transactions` da mesma forma).
`RecentTransactionsList` duplica a lógica de sinal/cor por tipo (2
linhas) em vez de importar de `features/transactions` — mesmo padrão
de pequena duplicação já aceito entre `TransactionList`/
`TransactionDetailDialog` (ver `plan.md` da FEAT-23, decisão 3).

## Contratos técnicos

### `features/summary/api/summaryApi.ts`

```ts
export interface CategorySummaryItem {
  categoryId: string
  nome: string
  gastoCents: number
  orcamentoMensalCents: number
}

export interface SummaryTransactionItem {
  id: string
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
  createdByUserId: string
  createdByLabel: string
  createdAt: string
}

export interface SummaryResponse {
  month: string
  saldoCents: number
  receitasCents: number
  gastoCents: number
  orcamentoTotalCents: number
  restanteCents: number
  porCategoria: CategorySummaryItem[]
  ultimosLancamentos: SummaryTransactionItem[]
}

async function getSummary(token: string, month: string): Promise<SummaryResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/summary?month=${month}`, { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertOk(response) // 401 → SessionExpiredError; !ok → UnknownSummaryError
  return response.json() as Promise<SummaryResponse>
}

export const summaryApi = { getSummary }
```

Mesmo padrão de `safeFetch`/`assertOk` já usado em `transactionsApi.ts`/
`categoriesReadApi.ts` — sem checagem de `400` dedicada (não esperado
em uso normal, `month` sempre calculado pelo client).

### `features/summary/hooks/useSummary.ts`

```ts
interface UseSummaryResult {
  data: SummaryResponse | null
  isLoading: boolean
  error: Error | null
  refetch: () => void
}

export function useSummary(month: string): UseSummaryResult {
  // busca ao montar e sempre que `month` mudar (useEffect com [month]
  // nas deps); `refetch()` repete a busca com o `month` atual —
  // chamado pelo `onSaved` do TransactionFormDialog em DashboardPage
}
```

### `features/summary/utils/month.ts`

```ts
const MONTHS_PT = ['janeiro', 'fevereiro', 'março', 'abril', 'maio', 'junho',
  'julho', 'agosto', 'setembro', 'outubro', 'novembro', 'dezembro']

export function getCurrentYearMonth(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}

export function formatMonthLabel(month: string): string {
  const [year, m] = month.split('-').map(Number)
  const name = MONTHS_PT[m - 1]
  return `${name.charAt(0).toUpperCase()}${name.slice(1)} de ${year}`
}
```

Ambas feature-scoped (não `lib/`) por ora — mesma lógica já seguida
pelo `currency.ts` antes da FEAT-22 (só sobe pra `lib/` quando uma
segunda feature precisar; hoje só o dashboard usa formatação de mês).

### `features/summary/components/SummaryCards.tsx`

```tsx
interface SummaryCardsProps {
  summary: SummaryResponse
}
```
Saldo: sinal `-`/cor accent quando `saldoCents < 0`, cor positive
quando ≥ 0 (mesmo padrão de `TransactionList`). Restante: sinal/cor
igual ao Saldo quando negativo (decisão 2 da spec); barra de progresso
`Math.min(100, orcamentoTotalCents > 0 ? (gastoCents / orcamentoTotalCents) * 100 : 0)`,
cor da barra e do texto vira accent quando `gastoCents > orcamentoTotalCents`.

### `features/summary/components/CategorySpendingList.tsx`

```tsx
interface CategorySpendingListProps {
  items: CategorySummaryItem[]
}
```
Por item: `over = item.gastoCents > item.orcamentoMensalCents`; barra
`Math.min(100, orcamentoMensalCents > 0 ? (gastoCents / orcamentoMensalCents) * 100 : 0)`;
cor accent quando `over`, `--color-neutral-800`/`--color-text` caso
contrário (mesmo padrão do `computeCategories()` do `.dc.html`).
`items.length === 0` → mensagem de estado vazio.

### `features/summary/components/RecentTransactionsList.tsx`

```tsx
interface RecentTransactionsListProps {
  items: SummaryTransactionItem[]
}
```
Usa `useCategories()` (`lib/categories/useCategories`) pra resolver
nome/tile da categoria de cada item (`CategoryLetterTile`, sem prop
`tipo` — tile neutro, mesmo padrão de `TransactionDetailDialog`).
Sinal/cor do valor por `item.tipo`, mesma fórmula de
`TransactionList.tsx` (duplicada aqui, ver nota acima).
`items.length === 0` → mensagem de estado vazio. Itens não são
clicáveis (decisão 5 da spec — sem `onClick`, sem `cursor: pointer`).

### `routes/DashboardPage.tsx` (era `HomePage.tsx`)

```tsx
const month = getCurrentYearMonth() // calculado 1x por montagem do componente
const { data, isLoading, error, refetch } = useSummary(month)
const [formTarget, setFormTarget] = useState<{ tipo: 'despesa' | 'receita' } | null>(null)

// cabeçalho: "Resumo" + formatMonthLabel(month):
// botões "+ Nova receita" (secundário) / "+ Nova despesa" (primário) → setFormTarget({ tipo })
// mesma ordem/estilo já usada em TransactionsListPage (FEAT-24)

<SummaryCards summary={data} />
<CategorySpendingList items={data.porCategoria} />
{/* "Ver todas (N) →" → <Link to="/categories"> */}
<RecentTransactionsList items={data.ultimosLancamentos} />
{/* "Ver todas →" → <Link to={`/transactions?yearMonth=${month}`}> */}

<TransactionFormDialog
  open={formTarget !== null}
  tipo={formTarget?.tipo}
  onOpenChange={(open) => !open && setFormTarget(null)}
  onSaved={refetch}
/>
```
Loading/erro seguem o padrão textual já usado nas demais telas
(`"Carregando..."` / mensagem de erro com `error.message`).

### `features/transactions/hooks/useTransactionsQuery.ts` — filtro inicial

```ts
export function useTransactionsQuery(
  initialFilters: GetTransactionsParams = {},
): UseTransactionsQueryResult {
  const [filters, setFilters] = useState<GetTransactionsParams>(initialFilters)
  // ...
  useEffect(() => {
    fetchPage(initialFilters, null, false) // era fetchPage({}, null, false)
  }, []) // eslint-disable-line react-hooks/exhaustive-deps
  // resto inalterado
}
```
Chamar sem argumento continua idêntico ao comportamento de hoje
(default `{}`) — mudança 100% compatível com o uso atual em
`TransactionsListPage`.

### `features/transactions/components/TransactionFilters.tsx` — valor inicial

```tsx
interface TransactionFiltersProps {
  onApply: (filters: TransactionFilterOutput) => void
  initialValues?: Partial<TransactionFilterInput>
}

export function TransactionFilters({ onApply, initialValues }: TransactionFiltersProps) {
  const [advancedOpen, setAdvancedOpen] = useState(!!initialValues?.yearMonth)
  const { ... } = useForm<TransactionFilterInput, unknown, TransactionFilterOutput>({
    resolver: zodResolver(transactionFilterSchema),
    defaultValues: { yearMonth: '', categoryId: '', dateFrom: '', dateTo: '', minAmount: '', maxAmount: '', ...initialValues },
  })
  // resto inalterado
}
```

### `routes/TransactionsListPage.tsx` — lê `yearMonth` da URL

```tsx
const [searchParams] = useSearchParams()
const initialYearMonth = searchParams.get('yearMonth') ?? undefined
const query = useTransactionsQuery(initialYearMonth ? { yearMonth: initialYearMonth } : undefined)
// ...
<TransactionFilters onApply={query.applyFilters} initialValues={initialYearMonth ? { yearMonth: initialYearMonth } : undefined} />
```

### `styles/modernist/modernist.css` — novas classes

```css
.ds-modernist {
  /* ...tokens existentes... */
  --shadow-sm: 0 1px 2px color-mix(in srgb, var(--color-neutral-900) 14%, transparent);
}

/* — cards (FEAT-26) — */
.ds-modernist .card {
  display: flex; flex-direction: column; gap: var(--space-2);
  padding: var(--space-3); border-radius: var(--radius-md); background: var(--color-surface);
}
.ds-modernist .card-kicker {
  font-size: 10px; letter-spacing: 0.1em; text-transform: uppercase; color: var(--color-accent);
}
.ds-modernist .elev-sm { box-shadow: var(--shadow-sm); }

/* — barra de progresso (FEAT-26) — classes do próprio .dc.html,
   fora do bundle base do design system, mesmo racional de
   "vendorizar só o que é usado" */
.ds-modernist .je-track {
  height: 6px; background: var(--color-neutral-300); position: relative; overflow: hidden;
}
.ds-modernist .je-fill { height: 100%; }
```

## Decisões técnicas

1. **`features/summary` como feature nova**, nome alinhado ao recurso
   do backend (`/summary`), mesmo racional de `transactions`/
   `categories` levarem o nome do próprio recurso REST.
2. **`getCurrentYearMonth()`/`formatMonthLabel()` ficam em
   `features/summary/utils/`, não em `lib/`** — só um consumidor hoje;
   sobe pra `lib/` quando uma segunda feature precisar (mesmo racional
   já usado para `currency.ts` antes da FEAT-22).
3. **`RecentTransactionsList` duplica a fórmula de sinal/cor por tipo**
   em vez de importar de `features/transactions` — a regra de
   dependência entre features proíbe o import; duplicar 2 linhas é
   mais barato e mais simples do que criar uma abstração compartilhada
   pra tão pouca lógica (mesma decisão já tomada entre
   `TransactionList`/`TransactionDetailDialog` na FEAT-23).
4. **`useTransactionsQuery`/`TransactionFilters` ganham parâmetro/prop
   opcionais pra filtro inicial**, 100% compatíveis com o uso atual
   (default vazio) — evita duplicar toda a lógica de fetch/formulário
   só pra suportar o link "Ver todas" vindo do dashboard.
5. **`HomePage.tsx` renomeado para `DashboardPage.tsx`** — o
   componente deixa de ser um placeholder genérico e passa a ter
   identidade própria (mesmo racional da FEAT-23 renomear
   `ExpensesListPage` → `TransactionsListPage`).
6. **Barra de progresso usa `.je-track`/`.je-fill`**, classes que só
   existem no `<style>` inline do `.dc.html` (não no bundle base do
   design system) — vendorizadas do mesmo jeito que os tokens
   `--color-positive*` foram na FEAT-22.
7. **`navConfig.ts`: item `home` passa de `status: 'placeholder'` para
   `'active'`** — busquei no código e esse campo não afeta nenhuma
   renderização hoje (só documentação), mas fica desatualizado se não
   ajustar agora que a tela é real.

## Recursos AWS

Nenhum. Esta feature só consome `GET /summary` (já em produção, backend
FEAT-23) e reaproveita `POST /transactions`/`GET /categories` já
publicados — nenhuma infraestrutura nova.

## Mapeamento de erros

| Origem | Condição | Exceção lançada | Mensagem exibida |
|---|---|---|---|
| `GET /summary` | `401` | `SessionExpiredError` (nova, mesma mensagem padrão) | "Sua sessão expirou. Faça login novamente." — limpa a sessão |
| `GET /summary` | falha de rede | `NetworkError` (nova, mesma mensagem padrão) | "Não foi possível conectar à API. Verifique sua conexão." |
| `GET /summary` | outro status (`400`/`500`, não esperado em uso normal) | `UnknownSummaryError` (nova) | "Ocorreu um erro inesperado ao carregar o resumo. Tente novamente." |

`POST /transactions` (via `TransactionFormDialog` reaproveitado): sem
mudança, mesmo mapeamento já documentado nas FEAT-23/24.

## Pontos a confirmar antes do `/tasks`

1. **"Ver todas" de últimos lançamentos exige tocar
   `features/transactions`** (`useTransactionsQuery`,
   `TransactionFilters`, `TransactionsListPage`) além de
   `features/summary` — não ficou explícito na spec que a decisão 3
   ("navega filtrado pelo mês") teria esse custo técnico. A versão
   completa (painel "Filtros avançados" já aberto, mostrando o mês
   aplicado, editável) é a proposta acima; uma alternativa mais barata
   seria só aplicar o filtro no fetch sem sincronizar visualmente o
   formulário (o painel continuaria fechado por padrão, sem mostrar
   que um filtro de mês já está ativo) — confirmar qual das duas.
2. **`navConfig.ts`: mudar `status` de `home` para `'active'`** — como
   esse campo não afeta nada hoje, é uma mudança de zero risco, mas
   sinalizando por ser um arquivo fora do escopo "óbvio" da feature.
