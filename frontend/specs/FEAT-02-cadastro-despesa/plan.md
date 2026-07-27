# Plan — FEAT-02: Cadastro de despesas

Referência: [`spec.md`](./spec.md). Segue o mesmo padrão arquitetural
estabelecido em `frontend/specs/FEAT-01-setup-login/plan.md` e
`frontend/docs/constitution.md` (feature-based, token em memória, testes
via MSW no nível de rede).

## Camadas afetadas (feature-based)

Nova feature `features/expenses/`, seguindo exatamente a mesma estrutura
interna já usada por `features/auth/`:

```
frontend/app/src/
├── features/
│   └── expenses/
│       ├── api/
│       │   └── expensesApi.ts        # registerExpense(token, payload)
│       ├── components/
│       │   └── ExpenseForm.tsx       # RHF + zodResolver + useRegisterExpense
│       ├── hooks/
│       │   └── useRegisterExpense.ts
│       ├── schemas/
│       │   └── expenseSchema.ts      # Zod: description, amount, category, expenseDate
│       ├── constants/
│       │   └── expenseCategories.ts  # enum backend → label pt-BR
│       ├── utils/
│       │   └── currency.ts           # parseCurrencyToCents
│       └── errors/
│           └── expenseErrors.ts      # ValidationError, SessionExpiredError, NetworkError, UnknownExpenseError
├── routes/
│   ├── RegisterExpensePage.tsx       # substitui HomePage.tsx (removido)
│   └── LoginPage.tsx                 # inalterado
└── app/
    └── router.tsx                    # index route: HomePage → RegisterExpensePage
```

**`features/expenses/` não tem `store/`.** Diferente de `auth` (onde a
sessão precisa ser compartilhada por `ProtectedRoute`, `LoginPage` e
qualquer página futura), o estado de cadastro de despesa
(`isLoading`/`error`/`success`) é local ao formulário — não há nada
aqui que outra parte da árvore precise ler. Usar Zustand seria estado
global sem necessidade. Precedente a seguir: só criar `store/` numa
feature quando o estado precisar ser lido fora do componente que o
gera.

`HomePage.tsx` (placeholder da FEAT-01) é removido — a spec já
determina que esta tela **substitui** o placeholder, não convive com
ele.

## Contratos técnicos

Caminhos relativos a `frontend/app/src/`.

### `features/expenses/constants/expenseCategories.ts`
```ts
export const EXPENSE_CATEGORIES = [
  { value: 'Alimentacao', label: 'Alimentação' },
  { value: 'Transporte', label: 'Transporte' },
  { value: 'Moradia', label: 'Moradia' },
  { value: 'Saude', label: 'Saúde' },
  { value: 'Educacao', label: 'Educação' },
  { value: 'Lazer', label: 'Lazer' },
  { value: 'ComprasEServicos', label: 'Compras e Serviços' },
  { value: 'Outros', label: 'Outros' },
] as const

export type ExpenseCategory = (typeof EXPENSE_CATEGORIES)[number]['value']
```
Fonte única do enum (usado pelo schema Zod e pelo `<Select>` do
formulário) — espelha exatamente os valores aceitos por
`POST /expenses` no backend.

### `features/expenses/utils/currency.ts`
```ts
export function parseCurrencyToCents(value: string): number {
  const normalized = value.trim().replace(/\./g, '').replace(',', '.')
  return Math.round(Number(normalized) * 100)
}
```
Converte o valor digitado em formato monetário pt-BR (ex.: `"45,90"`,
`"1.234,56"`) para centavos (`long` esperado pela API). Usado dentro do
`.transform()` do schema Zod.

### `features/expenses/schemas/expenseSchema.ts`
```ts
const CURRENCY_REGEX = /^\d+(\.\d{3})*(,\d{2})?$/

export const expenseSchema = z.object({
  description: z.string().trim().min(1, 'Informe a descrição.').max(200),
  amount: z
    .string()
    .min(1, 'Informe o valor.')
    .regex(CURRENCY_REGEX, 'Use o formato 0,00.')
    .transform(parseCurrencyToCents)
    .refine((cents) => cents > 0, 'O valor deve ser maior que zero.'),
  category: z.enum(EXPENSE_CATEGORIES.map((c) => c.value) as [string, ...string[]]),
  expenseDate: z.string().min(1, 'Informe a data.'),
})

export type ExpenseFormInput = z.input<typeof expenseSchema>   // amount como string (o que o form manipula)
export type ExpenseFormOutput = z.output<typeof expenseSchema> // amount já em centavos (o que vai pra API)
```
`expenseDate` usa `<input type="date">` nativo do navegador, que já
entrega a string no formato `YYYY-MM-DD` — mesmo formato ISO 8601 que a
API espera, sem necessidade de parsing adicional.

### `features/expenses/errors/expenseErrors.ts`
```ts
export class ValidationError extends Error {}       // 400 (edge case — client já validou antes)
export class SessionExpiredError extends Error {}   // 401
export class NetworkError extends Error {}
export class UnknownExpenseError extends Error {}
```
Mensagens amigáveis embutidas no `message` de cada classe, mesmo padrão
de `features/auth/errors/authErrors.ts`. `SessionExpiredError` é
semanticamente diferente do `InvalidCredentialsError` da feature auth
(aqui 401 significa "sessão expirou no meio do uso", não "credenciais
erradas") — por isso não é reaproveitada entre features.

### `features/expenses/api/expensesApi.ts`
```ts
async function registerExpense(
  token: string,
  payload: { description: string; amountInCents: number; category: string; expenseDate: string },
): Promise<{ id: string; description: string; amountInCents: number; category: string; expenseDate: string; createdAt: string }>
```
`POST {VITE_API_BASE_URL}/expenses` via `lib/httpClient`, com header
`Authorization: Bearer <token>`. Mapeia status HTTP → erros de
`expenseErrors.ts` (400 → `ValidationError`, 401 → `SessionExpiredError`,
falha de rede → `NetworkError`, outro status → `UnknownExpenseError`).
Mesmo padrão de `authApi.ts`: função simples, sem interface/abstração
de repositório.

### `features/expenses/hooks/useRegisterExpense.ts`
```ts
interface UseRegisterExpenseResult {
  registerExpense: (data: ExpenseFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}
```
Lê o token via `useAuthStore((state) => state.token)`. Em caso de
sucesso, seta `success = true` (o `ExpenseForm` reage a isso pra
resetar o formulário — via `useEffect`, mesmo padrão reativo já usado
em `LoginPage`, evitando depender do valor de retorno de uma Promise
capturado por closure). Em caso de `SessionExpiredError`, além de
setar `error`, chama `useAuthStore.getState().clearSession()` —
**isso é suficiente para redirecionar ao login**: como `ProtectedRoute`
já está inscrito na `authStore` via `useAuthSession`, limpar a sessão
dispara um novo render do `ProtectedRoute` que já existe montado,
resultando no redirect automático para `/login`, sem o hook precisar
chamar `navigate()` diretamente.

### `features/expenses/components/ExpenseForm.tsx`
RHF + `zodResolver(expenseSchema)`, campos: `Input` (descrição),
`Input` (valor, `inputMode="decimal"`, texto livre — sem lib de
máscara adicional, a validação via regex cobre o suficiente pro MVP),
`Select` (categoria, shadcn — **novo componente, ainda não instalado**,
ver "Novas dependências" abaixo), `Input type="date"` (data). Exibe:
- Erros inline por campo (`formState.errors`)
- `Alert` (variant `destructive`) quando `error` do hook está setado
- `Alert` (variant `default`) de confirmação quando `success === true`
- `useEffect` que chama `reset()` do RHF quando `success` vira `true`

### `routes/RegisterExpensePage.tsx`
Substitui `routes/HomePage.tsx`. Mantém a ação de logout que existia no
placeholder (chama `authStore.clearSession()` e navega para `/login`),
já que a spec não previu outro lugar pra essa ação — só move pra um
cabeçalho simples acima do `ExpenseForm`.

### `app/router.tsx`
Troca o `element` da rota índice de `<HomePage />` para
`<RegisterExpensePage />`. Nenhuma outra rota muda.

## Novas dependências

- **shadcn `select`**: `npx shadcn add select` — necessário pro campo
  de categoria. Único componente shadcn novo desta feature (os demais
  já existem: `Input`, `Label`, `Button`, `Alert`).
- Nenhuma lib nova de terceiros (sem lib de máscara de input monetário,
  sem date picker — `<input type="date">` nativo é suficiente pro MVP).

## Recursos AWS
**Nenhum recurso novo.** Consome `POST /expenses`, já implementado e
provisionado (mesma API Gateway/Lambda da FEAT-01/FEAT-10 do backend).

## Mapeamento de erros

| Cenário | Origem | Erro tipado | UI |
|---|---|---|---|
| Campo obrigatório vazio/inválido | Validação Zod (client) | — (erro de formulário do RHF) | Mensagem inline por campo |
| Valor com formato inválido ou ≤ 0 | Validação Zod (client) | — | Mensagem inline no campo valor |
| 400 da API (edge case) | `POST /expenses` 400 | `ValidationError` | Alerta genérico, formulário mantém os dados preenchidos |
| Sessão expirada | `POST /expenses` 401 | `SessionExpiredError` | Alerta de sessão expirada + `clearSession()` → redirect automático via `ProtectedRoute` |
| Falha de rede/timeout | `fetch` reject | `NetworkError` | Alerta genérico de conectividade |
| Erro inesperado (5xx) | API | `UnknownExpenseError` | Alerta genérico de erro |

## Testes (Vitest + Testing Library + MSW)
- `features/expenses/utils/currency.test.ts` — conversão de formatos
  (`"45,90"` → `4590`, `"1.234,56"` → `123456`, etc.)
- `features/expenses/schemas/expenseSchema.test.ts` — validação de
  cada campo, incluindo o `.transform()` do valor
- `features/expenses/hooks/useRegisterExpense.test.ts` — sucesso
  (`success = true`), 400 (`ValidationError`), 401 (`SessionExpiredError`
  + confirma que `authStore.clearSession()` foi chamado, via
  `useAuthStore.getState().token` voltando a `null`), erro de rede
- `features/expenses/components/ExpenseForm.test.tsx` — validação
  inline, submit com sucesso (formulário limpa, mostra confirmação),
  erro 400 (mensagem genérica, dados preenchidos permanecem no form) —
  via MSW mockando `POST /expenses`

Não há teste dedicado para `RegisterExpensePage.tsx`: o comportamento
de redirecionamento sem sessão já está coberto por
`components/ProtectedRoute.test.tsx` (FEAT-01), e o comportamento do
formulário em si é coberto por `ExpenseForm.test.tsx` — testar a página
de novo seria redundante.

## Decisões técnicas confirmadas
- **Sem `store/` nesta feature** — estado local ao hook/formulário,
  já que nada fora do `ExpenseForm` precisa ler `isLoading`/`error`/
  `success`.
- **`SessionExpiredError` própria da feature**, não reaproveita
  `InvalidCredentialsError` de `auth` — semântica diferente (sessão
  expirou vs. credenciais erradas).
- **Sem lib de máscara de input monetário** — regex + `.transform()`
  no Zod é suficiente pro volume de complexidade do MVP.
- **`<input type="date">` nativo** em vez de um date picker de
  terceiros — já entrega o formato ISO 8601 que a API espera.
- **Redirect em sessão expirada é reativo** (via `ProtectedRoute` já
  montado reagindo à mudança da `authStore`), não uma chamada explícita
  de `navigate()` dentro do hook — mesmo padrão já usado em
  `LoginPage.tsx` na FEAT-01.
- **Ação de logout**: confirmado pelo usuário — continua existindo,
  movida para um cabeçalho simples acima do `ExpenseForm` em
  `RegisterExpensePage.tsx`.

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente — todas as decisões técnicas deste plano foram
confirmadas.