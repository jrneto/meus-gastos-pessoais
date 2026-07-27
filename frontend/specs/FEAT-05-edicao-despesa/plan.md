# Plan — FEAT-05: Edição de despesa

Referência: [`spec.md`](./spec.md). Segue o mesmo padrão arquitetural
das features anteriores (`FEAT-02-cadastro-despesa/plan.md`,
`FEAT-03-listagem-despesas/plan.md`) e `frontend/docs/constitution.md`.
Reaproveita tudo que já existe em `features/expenses/` (schema,
constants, currency, padrão de erros) em vez de duplicar.

## Camadas afetadas

```
frontend/app/src/
├── features/
│   └── expenses/
│       ├── api/
│       │   └── expensesApi.ts          # + getExpenseById, updateExpense
│       ├── components/
│       │   ├── ExpenseForm.tsx          # refatorado — usa ExpenseFormFields
│       │   ├── ExpenseFormFields.tsx    # NOVO — campos compartilhados (dumb component)
│       │   ├── EditExpenseForm.tsx      # NOVO
│       │   ├── ExpenseNotFound.tsx      # NOVO — view compartilhada de "não encontrada"
│       │   └── ExpenseList.tsx          # + ação de editar por item
│       ├── errors/
│       │   └── expenseErrors.ts         # + NotFoundError, UpdateValidationError
│       ├── hooks/
│       │   ├── useExpense.ts            # NOVO — GET /expenses/{id}
│       │   └── useUpdateExpense.ts      # NOVO — PUT /expenses/{id}
│       └── utils/
│           └── currency.ts              # + centsToAmountInput
├── routes/
│   └── EditExpensePage.tsx              # NOVO
└── app/
    └── router.tsx                        # + rota 'expenses/:id/edit'
```

## Decisões técnicas confirmadas

- **Extrair `ExpenseFormFields` em vez de duplicar os 4 campos.**
  `ExpenseForm` (cadastro, FEAT-02) e `EditExpenseForm` (esta feature)
  usam exatamente os mesmos campos/validações (`expenseSchema`, já
  existente). Em vez de copiar os ~80 linhas de markup dos inputs, um
  componente puramente apresentacional (`register`/`control`/`errors`
  como props, sem hook de API) é compartilhado pelos dois. `ExpenseForm`
  é refatorado para usá-lo, mas seu comportamento e DOM final não
  mudam — `ExpenseForm.test.tsx` (FEAT-02) continua passando sem
  alteração.
- **`ExpenseForm` e `EditExpenseForm` continuam sendo dois componentes
  separados**, não um único componente com `mode="create"|"edit"`. Os
  fluxos de sucesso são fundamentalmente diferentes (cadastro limpa o
  formulário e permanece na tela; edição navega de volta à listagem) e
  as origens de erro/hook são diferentes (`useRegisterExpense` vs.
  `useUpdateExpense`) — unificar os dois exigiria um componente com
  ramificação condicional em vários pontos, mais complexo do que dois
  componentes pequenos e diretos compondo o mesmo `ExpenseFormFields`.
- **`ExpenseNotFound` é compartilhado entre o carregamento inicial e o
  salvamento.** Se `GET /expenses/{id}` retornar 404 (tela recém-
  aberta) ou se `PUT /expenses/{id}` retornar 404 (despesa removida
  entre o carregamento e o envio), o mesmo componente substitui o
  formulário — mesma mensagem, mesmo caminho de volta à listagem,
  conforme pedido na spec ("mesma mensagem clara" nos dois casos).
- **`useExpense`/`useUpdateExpense` não navegam sozinhos.** Mesmo
  padrão reativo já estabelecido em `useRegisterExpense`/
  `useExpensesQuery`: os hooks só expõem estado (`data`/`success`/
  `error`); é o componente que reage via `useEffect` (`EditExpenseForm`
  chama `navigate('/expenses')` quando `success` vira `true`) ou
  delega ao fluxo já existente (`SessionExpiredError` →
  `useAuthStore.getState().clearSession()` dentro do próprio hook,
  igual às features anteriores — o redirect para `/login` continua
  vindo de `ProtectedRoute` reagindo à store).
- **Ação de editar na listagem é um ícone-link (`Pencil`, já disponível
  via `lucide-react`), não um novo prop de callback em `ExpenseList`.**
  Como a navegação é auto-contida (`<Link to={.../edit} />`),
  `ExpenseList` não precisa de uma nova prop — mantém sua assinatura
  atual, só ganha um elemento a mais por item.
- **Rota de edição não entra em `navConfig.ts` (FEAT-04).** É uma rota
  de detalhe/ação (`/expenses/:id/edit`), não um destino de navegação
  de primeiro nível — não faz sentido como item de menu. Consequência
  aceita: nem a sidebar nem o bottom nav destacam "Listagem/Filtros"
  enquanto o usuário está na tela de edição (o matching de item ativo
  em `NavItemRow`, FEAT-04, é por igualdade exata de rota) — fora do
  escopo desta feature alterar esse comportamento.
- **`centsToAmountInput` é uma função nova em `currency.ts`**, distinta
  de `formatCentsToCurrency` (que inclui o símbolo `R$` e não é o
  formato que o campo de valor do formulário aceita de volta via
  `parseCurrencyToCents`). Produz o mesmo formato que o usuário digitaria
  (`"1.234,56"`), usado como `defaultValue` do campo ao pré-preencher.

## Contratos técnicos

Caminhos relativos a `frontend/app/src/`.

### `features/expenses/utils/currency.ts` (acréscimo)
```ts
export function centsToAmountInput(cents: number): string {
  return (cents / 100).toLocaleString('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}
```

### `features/expenses/errors/expenseErrors.ts` (acréscimos)
```ts
export class NotFoundError extends Error {
  constructor() {
    super('Despesa não encontrada.')
    this.name = 'NotFoundError'
  }
}

export class UpdateValidationError extends Error {
  constructor() {
    super('Não foi possível salvar as alterações. Verifique os dados informados.')
    this.name = 'UpdateValidationError'
  }
}
```
`SessionExpiredError`, `NetworkError` e `UnknownExpenseError` já
existentes (FEAT-02) são reaproveitados sem alteração.

### `features/expenses/api/expensesApi.ts` (acréscimos)
```ts
interface UpdateExpensePayload {
  description: string
  amountInCents: number
  category: string
  expenseDate: string
}

interface ExpenseDetail {
  id: string
  description: string
  amountInCents: number
  category: string
  expenseDate: string
  createdAt: string
}

function assertDetailOk(response: Response): void {
  if (response.status === 404) throw new NotFoundError()
  if (response.status === 401) throw new SessionExpiredError()
  if (!response.ok) throw new UnknownExpenseError()
}

function assertUpdateOk(response: Response): void {
  if (response.status === 400) throw new UpdateValidationError()
  if (response.status === 404) throw new NotFoundError()
  if (response.status === 401) throw new SessionExpiredError()
  if (!response.ok) throw new UnknownExpenseError()
}

async function getExpenseById(token: string, id: string): Promise<ExpenseDetail> {
  const response = await safeFetch(() =>
    httpClient.get(`/expenses/${id}`, { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertDetailOk(response)
  return response.json() as Promise<ExpenseDetail>
}

async function updateExpense(
  token: string,
  id: string,
  payload: UpdateExpensePayload,
): Promise<ExpenseDetail> {
  const response = await safeFetch(() =>
    httpClient.put(`/expenses/${id}`, payload, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertUpdateOk(response)
  return response.json() as Promise<ExpenseDetail>
}

export const expensesApi = { registerExpense, getExpenses, getExpenseById, updateExpense }
```
`safeFetch` já existente é reaproveitado sem mudança. `httpClient` hoje
só expõe `get`/`post` (`lib/httpClient.ts`) — precisa de um método
`put` novo, mesmo padrão dos existentes (`method: 'PUT'`, corpo
serializado).

### `features/expenses/hooks/useExpense.ts`
```ts
interface UseExpenseResult {
  data: ExpenseDetail | null
  isLoading: boolean
  error: Error | null
}

export function useExpense(id: string): UseExpenseResult {
  const [data, setData] = useState<ExpenseDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    expensesApi
      .getExpenseById(token ?? '', id)
      .then((result) => {
        if (!cancelled) setData(result)
      })
      .catch((err) => {
        if (cancelled) return
        if (err instanceof SessionExpiredError) {
          useAuthStore.getState().clearSession()
        }
        setError(err as Error)
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [id, token])

  return { data, isLoading, error }
}
```
`cancelled` evita `setState` após desmontagem (ex.: usuário navega para
fora da tela de edição antes da resposta chegar).

### `features/expenses/hooks/useUpdateExpense.ts`
```ts
interface UseUpdateExpenseResult {
  updateExpense: (data: ExpenseFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useUpdateExpense(id: string): UseUpdateExpenseResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function updateExpense(data: ExpenseFormOutput): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await expensesApi.updateExpense(token ?? '', id, {
        description: data.description,
        amountInCents: data.amount,
        category: data.category,
        expenseDate: data.expenseDate,
      })
      setSuccess(true)
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        useAuthStore.getState().clearSession()
      }
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { updateExpense, isLoading, error, success }
}
```
Mesmo formato de `useRegisterExpense` (FEAT-02), parametrizado pelo
`id` da despesa.

### `features/expenses/components/ExpenseFormFields.tsx`
```ts
interface ExpenseFormFieldsProps {
  register: UseFormRegister<ExpenseFormInput>
  control: Control<ExpenseFormInput>
  errors: FieldErrors<ExpenseFormInput>
}
```
Os 4 blocos de campo (`description`, `amount`, `category` via
`Controller`/`Select`, `expenseDate`) extraídos tal qual existem hoje
em `ExpenseForm.tsx` — sem `<form>`, sem alertas, sem botão de submit
(isso continua em cada componente que a usa).

### `features/expenses/components/ExpenseForm.tsx` (refatorado)
Mesmo `useForm`/`useRegisterExpense`/alertas/`useEffect` de reset já
existentes; o bloco dos 4 campos vira
`<ExpenseFormFields register={register} control={control} errors={errors} />`.
Comportamento e saída em DOM inalterados.

### `features/expenses/components/ExpenseNotFound.tsx`
```tsx
export function ExpenseNotFound() {
  return (
    <div className="flex w-full max-w-sm flex-col items-center gap-4 py-8 text-center">
      <p className="text-sm text-muted-foreground">Despesa não encontrada.</p>
      <Button render={<Link to="/expenses">Voltar à listagem</Link>} />
    </div>
  )
}
```

### `features/expenses/components/EditExpenseForm.tsx`
```ts
interface EditExpenseFormProps {
  expense: ExpenseDetail
}
```
`useForm<ExpenseFormInput, unknown, ExpenseFormOutput>` com
`defaultValues` calculados de `expense` (`amount:
centsToAmountInput(expense.amountInCents)`, demais campos diretos).
`useUpdateExpense(expense.id)`. `useEffect` chama
`navigate('/expenses')` quando `success` vira `true` (mesmo padrão
reativo do `reset()` em `ExpenseForm`). Se `error instanceof
NotFoundError`, renderiza `<ExpenseNotFound />` no lugar do formulário;
qualquer outro erro (`UpdateValidationError`, `SessionExpiredError`,
`NetworkError`, `UnknownExpenseError`) vira um `Alert` acima dos
campos, dados preenchidos preservados. Botões: "Salvar" (submit) e
"Cancelar" (`Link` para `/expenses`, sem chamar a API).

### `features/expenses/components/ExpenseList.tsx` (ajuste)
Cada item ganha um `Link` com ícone `Pencil` (`lucide-react`) para
`/expenses/${item.id}/edit`, ao lado do valor formatado. Assinatura de
props inalterada.

### `routes/EditExpensePage.tsx`
```tsx
export function EditExpensePage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, error } = useExpense(id!)

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Editar despesa</h1>
      {isLoading && <p className="text-sm text-muted-foreground">Carregando...</p>}
      {!isLoading && error instanceof NotFoundError && <ExpenseNotFound />}
      {!isLoading && error && !(error instanceof NotFoundError) && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível carregar a despesa</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}
      {!isLoading && data && <EditExpenseForm expense={data} />}
    </div>
  )
}
```

### `lib/httpClient.ts` (acréscimo)
```ts
put: (path: string, body?: unknown, init?: RequestInit) =>
  request(path, {
    ...init,
    method: 'PUT',
    body: body !== undefined ? JSON.stringify(body) : undefined,
  }),
```
Mesmo padrão de `post`, único método novo necessário no client HTTP
compartilhado.

### `app/router.tsx` (ajuste)
```tsx
{ path: 'expenses/:id/edit', element: <EditExpensePage /> },
```
Acrescentado às filhas de `AppShell`, junto das rotas já existentes.

## Novas dependências
Nenhuma. `Pencil` já vem com `lucide-react` (instalado desde FEAT-04).
Nenhum componente shadcn novo.

## Recursos AWS
**Nenhum recurso novo.** Consome `GET /expenses/{id}` e
`PUT /expenses/{id}`, já implementados e provisionados
(FEAT-07/FEAT-08/FEAT-10 do backend).

## Mapeamento de erros

| Cenário | Origem | Erro tipado | UI |
|---|---|---|---|
| Campo obrigatório vazio/inválido | Validação Zod (client) | — (erro de formulário do RHF) | Mensagem inline no campo, não chama a API |
| 400 da API ao salvar (edge case) | `PUT /expenses/{id}` 400 | `UpdateValidationError` | Alerta genérico, dados preenchidos preservados |
| Despesa não encontrada / de outro usuário, ao carregar | `GET /expenses/{id}` 404 | `NotFoundError` | `ExpenseNotFound` no lugar do formulário |
| Despesa não encontrada / de outro usuário, ao salvar | `PUT /expenses/{id}` 404 | `NotFoundError` | `ExpenseNotFound` no lugar do formulário |
| Sessão expirada (carregar ou salvar) | 401 em qualquer uma das chamadas | `SessionExpiredError` (reaproveitado) | Alerta de sessão expirada + `clearSession()` → redirect automático via `ProtectedRoute` |
| Falha de rede/timeout | `fetch` reject | `NetworkError` (reaproveitado) | Alerta genérico de conectividade |
| Erro inesperado (5xx) | API | `UnknownExpenseError` (reaproveitado) | Alerta genérico de erro |

## Testes (Vitest + Testing Library + MSW)
- `features/expenses/utils/currency.test.ts` — acrescenta casos de
  `centsToAmountInput` (`4590` → `"45,90"`, `123456` → `"1.234,56"`)
- `features/expenses/hooks/useExpense.test.ts` — carregamento com
  sucesso, 404 (`NotFoundError`), 401 (`SessionExpiredError` +
  `clearSession()`), erro de rede
- `features/expenses/hooks/useUpdateExpense.test.ts` — sucesso
  (`success = true`), 400 (`UpdateValidationError`), 404
  (`NotFoundError`), 401 (`SessionExpiredError` + `clearSession()`)
- `features/expenses/components/EditExpenseForm.test.tsx` — formulário
  pré-preenchido a partir de `expense`, validação inline, submit com
  sucesso navega para `/expenses` (via `MemoryRouter`), 400 mantém
  dados preenchidos com alerta genérico, 404 (ao salvar) troca o
  formulário por `ExpenseNotFound`, "Cancelar" navega para `/expenses`
  sem chamar a API — via MSW mockando `PUT /expenses/{id}`
- `routes/EditExpensePage.test.tsx` — estado de carregamento, 404 ao
  carregar renderiza `ExpenseNotFound`, sucesso renderiza
  `EditExpenseForm` pré-preenchido — via MSW mockando
  `GET /expenses/{id}`
- `features/expenses/components/ExpenseList.test.tsx` — acrescenta
  verificação de que cada item tem um link de editar com `href`
  apontando para `/expenses/{id}/edit`

Não há teste dedicado para `ExpenseFormFields.tsx` isoladamente — é
totalmente coberto pelos testes de `ExpenseForm.test.tsx` (já
existente, inalterado) e `EditExpenseForm.test.tsx` (novo), mesmo
raciocínio já registrado nos plans anteriores para componentes-página.

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente.
