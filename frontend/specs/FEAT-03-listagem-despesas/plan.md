# Plan — FEAT-03: Listagem de despesas com filtros

Referência: [`spec.md`](./spec.md). Segue o mesmo padrão arquitetural
estabelecido em `frontend/specs/FEAT-02-cadastro-despesa/plan.md` e
`frontend/docs/constitution.md` (feature-based, token em memória, testes
via MSW no nível de rede). Reaproveita o que já existe em
`features/expenses/` (constants, errors, utils) em vez de duplicar.

## Camadas afetadas (feature-based)

Cresce a feature `features/expenses/` já criada na FEAT-02, e acrescenta
uma rota nova + um componente compartilhado de navegação:

```
frontend/app/src/
├── features/
│   └── expenses/
│       ├── api/
│       │   └── expensesApi.ts        # + getExpenses(token, params)
│       ├── components/
│       │   ├── ExpenseForm.tsx       # inalterado (FEAT-02)
│       │   ├── ExpenseFilters.tsx    # NOVO — RHF + zodResolver
│       │   └── ExpenseList.tsx       # NOVO — lista + "Carregar mais" + estado vazio
│       ├── hooks/
│       │   ├── useRegisterExpense.ts # inalterado (FEAT-02)
│       │   └── useExpensesQuery.ts   # NOVO
│       ├── schemas/
│       │   ├── expenseSchema.ts      # inalterado (FEAT-02)
│       │   └── expenseFilterSchema.ts # NOVO
│       ├── constants/
│       │   └── expenseCategories.ts  # inalterado — reaproveitado no filtro
│       ├── utils/
│       │   └── currency.ts           # + formatCentsToCurrency (novo export)
│       └── errors/
│           └── expenseErrors.ts      # + InvalidFilterError, UnknownExpenseQueryError
├── routes/
│   ├── RegisterExpensePage.tsx       # pequeno ajuste — link para /expenses
│   └── ExpensesListPage.tsx          # NOVO — header próprio, mesmo padrão
└── app/
    └── router.tsx                    # + rota '/expenses'
```

**`features/expenses/` continua sem `store/`.** O estado de
filtros/itens/cursor (`useExpensesQuery`) é local à tela de listagem —
nada fora dela precisa ler esse estado. Mesmo raciocínio já registrado
no plan da FEAT-02: só sobe pra Zustand quando o estado precisa ser
compartilhado fora do componente que o gera.

**Sem componente de navegação compartilhado nesta feature — decisão
explícita.** Cogitamos extrair um `AppHeader`/`AppLayout` comum, mas o
usuário confirmou que uma feature futura (próxima, "FEAT-04 — menu e
home page") vai introduzir a navegação real do app (menu com todas as
telas, não só um link pra "a outra tela"). Construir agora uma
abstração de navegação que a FEAT-04 provavelmente substitui seria
trabalho descartado. Solução desta feature: `ExpensesListPage.tsx`
ganha seu próprio header inline, copiando o padrão que já existe em
`RegisterExpensePage.tsx` (título + botão "Sair"); `RegisterExpensePage.tsx`
recebe só o ajuste mínimo de acrescentar um link para `/expenses` no
header que já tem. A duplicação entre os dois headers é aceita
propositalmente como temporária — será resolvida quando a FEAT-04
introduzir a navegação definitiva.

## Contratos técnicos

Caminhos relativos a `frontend/app/src/`.

### `features/expenses/utils/currency.ts` (acréscimo)
```ts
export function formatCentsToCurrency(cents: number): string {
  return (cents / 100).toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
}
```
Inverso de `parseCurrencyToCents` (já existente, FEAT-02) — usado para
exibir `amountInCents` de cada item da lista como `R$ 45,90`.

### `features/expenses/schemas/expenseFilterSchema.ts`
```ts
export const expenseFilterSchema = z
  .object({
    yearMonth: z.string().optional(),
    category: z.string().optional(), // '' = "todas"
    dateFrom: z.string().optional(),
    dateTo: z.string().optional(),
    minAmount: z.string().optional(), // formato "45,90", mesmo input do cadastro
    maxAmount: z.string().optional(),
  })
  .transform((data) => ({
    yearMonth: data.yearMonth || undefined,
    category: data.category || undefined,
    dateFrom: data.dateFrom || undefined,
    dateTo: data.dateTo || undefined,
    minAmountInCents: data.minAmount ? parseCurrencyToCents(data.minAmount) : undefined,
    maxAmountInCents: data.maxAmount ? parseCurrencyToCents(data.maxAmount) : undefined,
  }))
  .refine(
    (f) => !f.dateFrom || !f.dateTo || f.dateFrom <= f.dateTo,
    { message: 'Data inicial não pode ser depois da data final.', path: ['dateTo'] },
  )
  .refine(
    (f) => f.minAmountInCents === undefined || f.maxAmountInCents === undefined || f.minAmountInCents <= f.maxAmountInCents,
    { message: 'Valor mínimo não pode ser maior que o máximo.', path: ['maxAmount'] },
  )

export type ExpenseFilterOutput = z.output<typeof expenseFilterSchema>
```
Todos os campos opcionais — formulário de filtro nunca bloqueia por
campo obrigatório, só pelas duas combinações inconsistentes que o
backend também rejeitaria (`dateFrom > dateTo`,
`minAmountInCents > maxAmountInCents`), evitando o round-trip de 400
para esses dois casos. Reaproveita `parseCurrencyToCents` já existente
(FEAT-02) — mesmo formato de valor do formulário de cadastro.

### `features/expenses/errors/expenseErrors.ts` (acréscimos)
```ts
export class InvalidFilterError extends Error {
  constructor() {
    super('Um ou mais filtros são inválidos.')
    this.name = 'InvalidFilterError'
  }
}

export class UnknownExpenseQueryError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado ao buscar as despesas. Tente novamente.')
    this.name = 'UnknownExpenseQueryError'
  }
}
```
`SessionExpiredError` e `NetworkError` já existentes (FEAT-02) são
reaproveitados sem alteração — mesma semântica (sessão expirou / falha
de rede), independente de ser cadastro ou consulta.

### `features/expenses/api/expensesApi.ts` (acréscimo)
```ts
interface GetExpensesParams {
  yearMonth?: string
  category?: string
  dateFrom?: string
  dateTo?: string
  minAmountInCents?: number
  maxAmountInCents?: number
  cursor?: string
}

interface ExpenseQueryItem {
  id: string
  description: string
  amountInCents: number
  category: string
  expenseDate: string
  createdAt: string
}

interface GetExpensesResponse {
  items: ExpenseQueryItem[]
  nextCursor: string | null
}

function toQueryString(params: GetExpensesParams): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== '')
  const search = new URLSearchParams(entries as [string, string][])
  const query = search.toString()
  return query ? `?${query}` : ''
}

async function getExpenses(token: string, params: GetExpensesParams): Promise<GetExpensesResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/expenses${toQueryString(params)}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertQueryOk(response)
  return response.json() as Promise<GetExpensesResponse>
}

export const expensesApi = { registerExpense, getExpenses }
```
`assertQueryOk` é uma pequena variação local de `assertOk` (já
existente) que lança `InvalidFilterError`/`UnknownExpenseQueryError` em
vez de `ValidationError`/`UnknownExpenseError` — mesma estrutura,
mensagens corretas para o contexto de consulta. `safeFetch` (já
existente) é reaproveitado sem mudança.

### `features/expenses/hooks/useExpensesQuery.ts`
```ts
interface UseExpensesQueryResult {
  items: ExpenseQueryItem[]
  isLoading: boolean       // primeira página (novos filtros)
  isLoadingMore: boolean   // páginas seguintes
  error: Error | null
  hasMore: boolean
  applyFilters: (filters: ExpenseFilterOutput) => void
  loadMore: () => void
}
```
Estado interno: `items`, `cursor` (`string | null`), `filters`
(últimos filtros aplicados). Busca a primeira página (sem filtros) em
um `useEffect` de montagem — cobre a US "Listar despesas sem filtros".
`applyFilters` substitui `filters`, zera `items`/`cursor` e busca a
página 1 (cobre "Trocar de filtro reinicia a listagem"). `loadMore`
busca com o `cursor` atual e `filters` atuais, anexando ao final de
`items` (cobre "Carregar mais resultados"); não faz nada se
`cursor === null`. Em `SessionExpiredError`, chama
`useAuthStore.getState().clearSession()` — mesmo padrão reativo já
usado em `useRegisterExpense.ts`: o redirect para `/login` acontece via
`ProtectedRoute` reagindo à mudança da store, sem `navigate()` explícito
no hook.

### `features/expenses/components/ExpenseFilters.tsx`
RHF + `zodResolver(expenseFilterSchema)`, campos: `Input type="month"`
nativo (`yearMonth` — mesmo espírito do `<input type="date">` já usado
no `ExpenseForm`, zero dependência nova), `Select` (categoria, com
opção "Todas" + `EXPENSE_CATEGORIES`
já existente), `Input type="date"` × 2 (`dateFrom`/`dateTo`), `Input`
texto × 2 (`minAmount`/`maxAmount`, mesmo padrão do campo valor do
`ExpenseForm`). Submit chama `onApply(data)` (prop, recebida de
`ExpensesListPage`) — o componente não conhece `useExpensesQuery`
diretamente, mantendo-o testável isoladamente com MSW/mock de props.
Erros de `refine` (`dateTo`, `maxAmount`) exibidos inline, mesmo padrão
de `ExpenseForm`.

### `features/expenses/components/ExpenseList.tsx`
Recebe `items`, `isLoading`, `isLoadingMore`, `error`, `hasMore`,
`onLoadMore` via props (mesma separação: não chama a API diretamente).
Renderiza:
- Lista semântica (não uma `Table` shadcn — nenhum componente de tabela
  foi instalado ainda e o volume de colunas é pequeno; uma lista de
  linhas com Tailwind resolve sem dependência nova, mesmo raciocínio de
  "sem lib nova sem necessidade clara" já usado na FEAT-02)
- Cada item: descrição, `formatCentsToCurrency(amountInCents)`,
  label da categoria (via `EXPENSE_CATEGORIES`), `expenseDate`
  formatada
- Estado vazio (`items.length === 0 && !isLoading`): mensagem clara,
  sem parecer erro
- `Alert` (variant `destructive`) quando `error` está setado
- Botão "Carregar mais" (via `Button`, shadcn já existente),
  visível só quando `hasMore === true`; desabilitado/mostrando loading
  quando `isLoadingMore === true`

### `routes/ExpensesListPage.tsx`
Header inline, mesmo padrão de `RegisterExpensePage.tsx` (título +
`Button variant="outline"` de logout com `clearSession()` +
`navigate('/login', { replace: true })`), acrescido de um `Link`
(react-router-dom) para `/` com o texto "Nova despesa".
```tsx
export function ExpensesListPage() {
  const clearSession = useAuthStore((state) => state.clearSession)
  const navigate = useNavigate()
  const query = useExpensesQuery()

  function handleLogout() {
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <main className="flex min-h-svh flex-col items-center gap-6 p-4">
      <header className="flex w-full max-w-sm items-center justify-between pt-4">
        <h1 className="text-2xl font-semibold">Minhas despesas</h1>
        <div className="flex gap-2">
          <Button variant="ghost" asChild><Link to="/">Nova despesa</Link></Button>
          <Button variant="outline" onClick={handleLogout}>Sair</Button>
        </div>
      </header>
      <ExpenseFilters onApply={query.applyFilters} />
      <ExpenseList {...query} onLoadMore={query.loadMore} />
    </main>
  )
}
```

### `routes/RegisterExpensePage.tsx` (ajuste mínimo)
Header existente inalterado (título, `handleLogout`, botão "Sair") —
só acrescenta, ao lado do botão "Sair", um `Link` para `/expenses` com
o texto "Ver despesas". Nenhuma extração, nenhum componente novo.

### `app/router.tsx` (ajuste)
```tsx
{
  path: '/',
  element: <ProtectedRoute />,
  children: [
    { index: true, element: <RegisterExpensePage /> },
    { path: 'expenses', element: <ExpensesListPage /> },
  ],
}
```

## Novas dependências
Nenhuma. Nenhum componente shadcn novo (reaproveita `Select`, `Input`,
`Button`, `Alert`, `Label` já instalados pela FEAT-02). Nenhuma lib de
terceiros nova.

## Recursos AWS
**Nenhum recurso novo.** Consome `GET /expenses`, já implementado e
provisionado (FEAT-06/FEAT-10 do backend).

## Mapeamento de erros

| Cenário | Origem | Erro tipado | UI |
|---|---|---|---|
| `dateFrom` > `dateTo` ou `minAmount` > `maxAmount` | Validação Zod (client) | — (erro de formulário do RHF) | Mensagem inline no campo correspondente, não chama a API |
| 400 da API (edge case) | `GET /expenses` 400 | `InvalidFilterError` | Alerta genérico em `ExpenseList` |
| Sessão expirada | `GET /expenses` 401 | `SessionExpiredError` (reaproveitado) | Alerta de sessão expirada + `clearSession()` → redirect automático via `ProtectedRoute` |
| Falha de rede/timeout | `fetch` reject | `NetworkError` (reaproveitado) | Alerta genérico de conectividade |
| Erro inesperado (5xx) | API | `UnknownExpenseQueryError` | Alerta genérico de erro |
| Filtros não retornam nada | `GET /expenses` 200, `items: []` | — | Estado vazio (não é erro) |

## Testes (Vitest + Testing Library + MSW)
- `features/expenses/utils/currency.test.ts` — acrescenta casos de
  `formatCentsToCurrency` (`4590` → `"R$ 45,90"`)
- `features/expenses/schemas/expenseFilterSchema.test.ts` — cada campo
  opcional isolado, transform de valor/data, os dois `refine`
  (`dateFrom > dateTo`, `minAmount > maxAmount`)
- `features/expenses/hooks/useExpensesQuery.test.ts` — carga inicial
  sem filtro, `applyFilters` reinicia (`items`/`cursor` zerados antes da
  nova busca), `loadMore` anexa itens usando `nextCursor`, 400
  (`InvalidFilterError`), 401 (`SessionExpiredError` + confirma
  `clearSession()` chamado), lista vazia
- `features/expenses/components/ExpenseFilters.test.tsx` — validação
  inline dos dois `refine`, submit chama `onApply` com dados
  transformados — via mock de prop, sem rede
- `features/expenses/components/ExpenseList.test.tsx` — renderização
  dos itens, estado vazio, botão "Carregar mais" some quando
  `hasMore === false`, alerta de erro — via mock de props, sem rede

Não há teste dedicado para `ExpensesListPage.tsx`/
`RegisterExpensePage.tsx` como integração ponta a ponta: o
redirecionamento sem sessão já está coberto por
`ProtectedRoute.test.tsx` (FEAT-01), e o comportamento de cada peça
(filtros, lista) é coberto isoladamente — mesmo raciocínio já
registrado no plan da FEAT-02. O link de navegação adicionado em cada
página (`Link to="/expenses"` / `Link to="/"`) é simples o suficiente
pra não precisar de teste dedicado — sem lógica condicional, apenas
`href`.

## Decisões técnicas confirmadas
- **Sem `store/` nesta feature** — estado de filtros/itens/cursor é
  local à tela de listagem.
- **Sem componente/abstração de navegação compartilhada nesta
  feature** — header duplicado propositalmente entre
  `RegisterExpensePage` e `ExpensesListPage` (mesmo padrão simples de
  título + logout, cada um com seu link pra outra tela). A navegação
  real (menu, home page) é escopo de uma feature futura confirmada
  pelo usuário ("FEAT-04 — menu e home page"); extrair uma abstração
  agora seria trabalho descartado quando essa feature chegar.
- **`<input type="month">` nativo** para o filtro de mês — mesmo
  raciocínio do `<input type="date">` já usado no `ExpenseForm`
  (FEAT-02): zero dependência nova, formato `YYYY-MM` nativo do
  navegador já compatível com o que a API espera.
- **Sem componente de tabela (shadcn)** — lista semântica com Tailwind
  é suficiente para o volume de colunas do MVP; evita instalar
  dependência nova sem necessidade clara comprovada.
- **`ExpenseFilters`/`ExpenseList` recebem dados via props**, não
  chamam a API/hook diretamente — mantém os dois componentes
  testáveis isoladamente (mock de props), com `useExpensesQuery`
  como único ponto de integração com a API dentro de
  `ExpensesListPage`.
- **Erros tipados novos ficam em `expenseErrors.ts` existente**
  (`InvalidFilterError`, `UnknownExpenseQueryError`), reaproveitando
  `SessionExpiredError`/`NetworkError` já existentes — mesma
  semântica, evita duplicar classes equivalentes.
- **Rota nova em `/expenses`** (inglês, consistente com o nome do
  recurso na API) — `/login` e `/` (raiz) são as únicas rotas
  existentes hoje, nenhuma convenção de idioma de rota foi
  estabelecida antes; `/expenses` segue o nome do recurso HTTP.

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente — campo de mês (`<input type="month">` nativo) e
estratégia de navegação (header duplicado, sem abstração
compartilhada, aguardando a futura feature de menu) já confirmados
pelo usuário.
