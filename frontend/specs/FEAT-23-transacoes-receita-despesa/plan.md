# Plan — FEAT-23: Transações — generalizar despesa para receita/despesa

## Camadas afetadas

Toda a feature `expenses` é renomeada para `transactions`
(`frontend/app/src/features/expenses/` →
`frontend/app/src/features/transactions/`, arquivo por arquivo, mesmo
padrão de subpastas). Efeito colateral fora dessa pasta: rota do
router, item de menu, e comentários em `categories` que citam os nomes
antigos dos componentes.

| Camada/arquivo | O que muda |
|---|---|
| `features/transactions/api/transactionsApi.ts` (era `expensesApi.ts`) | Todas as chamadas usam `/transactions`; `expenseDate`→`date`; `tipo`, `createdByUserId`, `createdByLabel` adicionados aos tipos de leitura; `GetTransactionsParams` ganha `tipo?` (decisão 6 da spec, sem uso por UI) |
| `features/transactions/errors/transactionErrors.ts` (era `expenseErrors.ts`) | `UnknownExpenseError`→`UnknownTransactionError`, `UnknownExpenseQueryError`→`UnknownTransactionQueryError`; mensagem de `NotFoundError` generalizada para "Transação não encontrada." (a listagem já pode mostrar receita); demais classes mantidas como estão |
| `features/transactions/schemas/transactionSchema.ts` (era `expenseSchema.ts`) | Campo `expenseDate`→`date`; sem campo `tipo` (permanece implícito, ver "Decisões técnicas" item 1) |
| `features/transactions/schemas/transactionFilterSchema.ts` (era `expenseFilterSchema.ts`) | Sem mudança de campos — `tipo` não é exposto no filtro nesta feature (decisão 6 da spec) |
| `features/transactions/components/TransactionForm.tsx` (era `ExpenseForm.tsx`) | Dropdown de categoria filtra `tipo === 'despesa'` antes de listar; campo de data usa `date` no lugar de `expenseDate`; texto/label seguem "despesa" (criação continua restrita a esse tipo) |
| `features/transactions/components/TransactionFormDialog.tsx` (era `ExpenseFormDialog.tsx`) | Só renome + ids/imports; título continua fixo "Nova despesa"/"Editar despesa" |
| `features/transactions/components/TransactionList.tsx` (era `ExpenseList.tsx`) | Cada linha calcula sinal (`-`/`+`) e cor (`--color-accent-700`/`--color-positive-700`) a partir de `item.tipo`; coluna de data usa `item.date` |
| `features/transactions/components/TransactionFilters.tsx` (era `ExpenseFilters.tsx`) | Só renome + imports — sem mudança de campos |
| `features/transactions/components/TransactionDetailDialog.tsx` (era `ExpenseDetailDialog.tsx`) | Ganha bloco "Lançado por" (`transaction.createdByLabel`); título "Detalhe da despesa" e cor do valor continuam fixos (decisão 4 da spec) |
| `features/transactions/components/TransactionDeleteDialog.tsx` (era `ExpenseDeleteDialog.tsx`) | Só renome + imports |
| `features/transactions/hooks/*` (5 hooks renomeados: `useTransactionsQuery`, `useTransaction`, `useRegisterTransaction`, `useUpdateTransaction`, `useDeleteTransaction`) | `useRegisterTransaction`/`useUpdateTransaction` passam a montar o payload com `tipo: 'despesa'` fixo (não vem do formulário) antes de chamar `transactionsApi` |
| `routes/TransactionsListPage.tsx` (era `ExpensesListPage.tsx`) | Só renome + imports; mantém um único botão "+ Nova despesa" (ver "Pontos a confirmar" item 1 sobre o botão "+ Nova receita") |
| `app/router.tsx` | Path `expenses` → `transactions`; import de `TransactionsListPage` |
| `components/nav/navConfig.ts` | `id: 'expenses'` → `'transactions'`, `to: '/expenses'` → `'/transactions'`; `label: 'Transações'` já está correto, sem mudança |
| `features/categories/components/CategoryDeleteDialog.tsx`, `lib/categories/CategoryLetterTile.tsx` | Só o **comentário** que cita `ExpenseDeleteDialog`/`ExpenseDetailDialog` é atualizado para `TransactionDeleteDialog`/`TransactionDetailDialog` — nenhuma mudança de código |
| Testes (`*.test.ts`/`*.test.tsx` de cada arquivo acima, + `navConfig.test.ts`, `DesktopSidebar.test.tsx`, `MobileBottomNav.test.tsx`, `AppShell.test.tsx`) | Acompanham o rename (`/expenses`→`/transactions` nas rotas/mocks MSW) e ganham os novos cenários da spec (dropdown filtrado por tipo, sinal/cor por tipo na lista, "Lançado por" no detalhe) |

Não muda: `features/categories/*` (além dos dois comentários acima),
`lib/currency.ts`, `lib/httpClient`, `features/auth/*`, `routes/
HomePage.tsx`/`ReportsComingSoonPage.tsx`/`SettingsPage.tsx`,
`ProtectedRoute`. Nenhuma rota de redirecionamento hardcoded para
`/expenses` existe fora de `router.tsx`/`navConfig.ts` (confirmado por
busca — o texto "expenses" em `LoginPage.tsx` é a tagline de marca
"jrn.expenses", sem relação com a rota, fora do escopo).

## Contratos técnicos

### `features/transactions/api/transactionsApi.ts`

```ts
interface RegisterTransactionPayload {
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
}

interface RegisterTransactionResponse {
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

export interface GetTransactionsParams {
  tipo?: 'despesa' | 'receita'
  yearMonth?: string
  categoryId?: string
  dateFrom?: string
  dateTo?: string
  minAmountInCents?: number
  maxAmountInCents?: number
  cursor?: string
}

export interface TransactionQueryItem {
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

export interface GetTransactionsResponse {
  items: TransactionQueryItem[]
  nextCursor: string | null
}

interface UpdateTransactionPayload {
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
}

export interface TransactionDetail {
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
```

Funções (`registerTransaction`, `getTransactions`, `getTransactionById`,
`updateTransaction`, `deleteTransaction`) mantêm a mesma assinatura e
lógica de hoje (`safeFetch`/`assert*Ok`), só trocando o path base
`/expenses` → `/transactions` e os nomes dos tipos. `assertOk`/
`assertQueryOk`/`assertDetailOk`/`assertUpdateOk`/`assertDeleteOk`
continuam checando exatamente os mesmos status hoje mapeados (`400`,
`401`, `404`) — `403` não ganha checagem dedicada nesta feature
(decisão 5 da spec: cai no `else` genérico de cada função, hoje
`Unknown*Error`).

### `features/transactions/errors/transactionErrors.ts`

```ts
export class ValidationError extends Error { /* mensagem inalterada */ }
export class SessionExpiredError extends Error { /* mensagem inalterada */ }
export class NetworkError extends Error { /* mensagem inalterada */ }
export class UnknownTransactionError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownTransactionError'
  }
}
export class InvalidFilterError extends Error { /* mensagem inalterada */ }
export class UnknownTransactionQueryError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado ao buscar as transações. Tente novamente.')
    this.name = 'UnknownTransactionQueryError'
  }
}
export class NotFoundError extends Error {
  constructor() {
    super('Transação não encontrada.')
    this.name = 'NotFoundError'
  }
}
export class UpdateValidationError extends Error { /* mensagem inalterada */ }
```

### `features/transactions/schemas/transactionSchema.ts`

```ts
export const transactionSchema = z.object({
  description: z.string().trim().min(1, 'Informe a descrição.').max(200, '...'),
  amount: z.string().min(1, 'Informe o valor.').regex(CURRENCY_REGEX, 'Use o formato 0,00.')
    .transform(parseCurrencyToCents).refine((cents) => cents > 0, 'O valor deve ser maior que zero.'),
  categoryId: z.string().min(1, 'Selecione uma categoria.'),
  date: z.string().min(1, 'Informe a data.'),
})

export type TransactionFormInput = z.input<typeof transactionSchema>
export type TransactionFormOutput = z.output<typeof transactionSchema>
```

Idêntico ao `expenseSchema` de hoje, só o campo `expenseDate` renomeado
para `date` — sem campo `tipo` (ver "Decisões técnicas" item 1).

### `features/transactions/hooks/useRegisterTransaction.ts` / `useUpdateTransaction.ts`

```ts
await transactionsApi.registerTransaction(token ?? '', {
  description: data.description,
  amountInCents: data.amount,
  categoryId: data.categoryId,
  tipo: 'despesa', // fixo nesta feature — sem campo correspondente no formulário
  date: data.date,
})
```

Mesmo padrão em `useUpdateTransaction` (`updateTransaction`). Nenhuma
outra mudança de lógica (loading/error/success inalterados).

### `TransactionForm.tsx` — filtro de categoria por tipo

```tsx
const { items: categories, isLoading: categoriesLoading } = useCategories()
const expenseCategories = categories.filter((category) => category.tipo === 'despesa')
```

`expenseCategories` substitui `categories` no `<select>` e na checagem
de "nenhuma categoria cadastrada" (estado vazio já existente passa a
checar `expenseCategories.length === 0`, não `categories.length`, para
não bloquear o formulário quando só existirem categorias de receita).

### `TransactionList.tsx` — sinal e cor por tipo

```tsx
const amountColor = item.tipo === 'receita' ? 'var(--color-positive-700)' : 'var(--color-accent-700)'
const amountSign = item.tipo === 'receita' ? '+ ' : '- '
```

Aplicado na célula de valor (`{amountSign}{formatCentsToCurrency(item.amountInCents)}`),
reaproveitando os tokens `--color-positive-700`/`--color-accent-700` já
existentes em `modernist.css` (introduzidos na FEAT-22 de categorias —
nenhum token novo necessário aqui).

### `TransactionDetailDialog.tsx` — bloco "Lançado por"

```tsx
<div>
  <div style={{ fontSize: '12px', opacity: 0.6, marginBottom: '4px' }}>Lançado por</div>
  <div style={{ fontSize: '14px' }}>{transaction.createdByLabel}</div>
</div>
```

Inserido entre o bloco de categoria e o bloco de descrição (mesma
posição do `.dc.html`, linha 549-552). Título do dialog
(`"Detalhe da despesa"`) e cor do valor continuam fixos, sem checar
`transaction.tipo` (decisão 4 da spec — generalização completa é
FEAT-25).

## Decisões técnicas

1. **`tipo` não é campo do formulário nesta feature.** Fica hardcoded
   como `'despesa'` no ponto em que o hook monta o payload
   (`useRegisterTransaction`/`useUpdateTransaction`), não no schema Zod
   nem no componente de formulário — mantém `TransactionForm` livre de
   um campo escondido/fantasma, e evita qualquer `defaultValues` com
   valor fixo que teria que ser filtrado do `TransactionFormOutput`.
   Quando a FEAT-24 adicionar o seletor, o `tipo` passa a vir do
   formulário e essa linha hardcoded sai.
2. **Renomear `expenseDate` para `date`** no schema/formulário (não só
   na API) — evita uma camada de tradução (`date: data.expenseDate`)
   sem propósito depois que o próprio contrato mudou de nome; mesmo
   racional já seguido pela FEAT-22 quando o backend trocou nomes de
   campo.
3. **Duplicação `TransactionQueryItem`/`TransactionDetail` mantida**
   (dois `interface` idênticos) — mesmo padrão já existente hoje
   (`ExpenseQueryItem`/`ExpenseDetail`); não é escopo desta feature
   unificar.
4. **Reaproveitar os tokens de cor `--color-positive-700`/
   `--color-accent-700`**, já introduzidos em `modernist.css` pela
   FEAT-22 (categorias) — nenhum token novo, nenhuma mudança em
   `modernist.css` nesta feature.
5. **Filtro de categoria em `TransactionForm` feito com `.filter()` no
   client** (mesmo padrão já usado por `CategoryList` pra separar
   categorias por tipo) — sem introduzir `GET /categories?tipo=` (que
   a FEAT-22 deixou disponível, mas não usado ainda).
6. **Rename mecânico de diretório** (`features/expenses/` →
   `features/transactions/`) feito arquivo por arquivo, preservando a
   estrutura interna (`api/`, `components/`, `errors/`, `hooks/`,
   `schemas/`) — sem introduzir nem remover nenhuma subpasta.
7. **Sem tratamento dedicado de `403`** (decisão 5 da spec) — as
   funções `assertUpdateOk`/`assertDeleteOk`/`assertOk` continuam sem
   um `if (response.status === 403)`, caindo no `else` genérico
   (`Unknown*Error`), igual ao comportamento de hoje.

## Recursos AWS

Nenhum. Esta feature só consome `/transactions`
(`GET`/`POST`/`PUT`/`DELETE`), já publicado e em produção pelo backend
(FEAT-22) — nenhuma infraestrutura nova.

## Mapeamento de erros

Sem mudança de comportamento em relação a hoje — só renome de classe
onde indicado:

| Origem | Condição | Exceção lançada | Mensagem exibida |
|---|---|---|---|
| `POST /transactions` | `400` | `ValidationError` (mantida) | "Não foi possível registrar a despesa. Verifique os dados informados." |
| `POST /transactions` | `401` | `SessionExpiredError` (mantida) | limpa sessão, mesmo fluxo atual |
| `POST /transactions` | `403` (papel `Leitura`) | `UnknownTransactionError` (fallback genérico, sem checagem dedicada) | "Ocorreu um erro inesperado. Tente novamente." |
| `POST /transactions` | outros | `UnknownTransactionError` (era `UnknownExpenseError`) | mesma mensagem genérica |
| `GET /transactions` | `400` | `InvalidFilterError` (mantida) | "Um ou mais filtros são inválidos." |
| `GET /transactions` | outros | `UnknownTransactionQueryError` (era `UnknownExpenseQueryError`) | mensagem genérica atualizada para "transações" |
| `GET /transactions/{id}` | `404` | `NotFoundError` (mensagem generalizada) | "Transação não encontrada." |
| `PUT /transactions/{id}` | `400` | `UpdateValidationError` (mantida) | "Não foi possível salvar as alterações. Verifique os dados informados." |
| `PUT`/`DELETE /transactions/{id}` | `403` (papel `Lancar` em transação de outro membro) | `UnknownTransactionError` (fallback genérico) | "Ocorreu um erro inesperado. Tente novamente." |
| `PUT`/`DELETE /transactions/{id}` | `404` | `NotFoundError` | fecha o popup silenciosamente (transação removida por outra sessão) — mesmo fluxo já existente |

## Pontos a confirmar antes do `/tasks`

1. **Botão "+ Nova receita" não é renderizado nesta feature** (nem
   desabilitado) — só o "+ Nova despesa" aparece em
   `TransactionsListPage`, igual à tela atual. O `.dc.html` mostra os
   dois lado a lado, mas como a decisão 1 da spec deixa o fluxo de
   receita inteiramente para a FEAT-24, entendo que renderizar um
   botão sem ação (ou com um placeholder) seria pior do que só
   acrescentá-lo quando funcionar de fato. Confirmar que essa leitura
   está certa antes de virar tarefa.
2. **Mensagem de `NotFoundError` generalizada de "Despesa não
   encontrada." para "Transação não encontrada."** — mudança de texto
   não pedida explicitamente pelos critérios de aceite da spec, mas
   necessária porque a listagem agora pode mostrar receita (evita
   mostrar "despesa" ao excluir/reabrir uma receita já removida).
   Confirmar que a mudança de texto é aceitável.
3. **`TransactionForm` passa a checar `expenseCategories.length === 0`
   (só despesa) para o estado "nenhuma categoria cadastrada"**, em vez
   de `categories.length === 0` — uma conta com só categorias de
   receita veria esse estado (com o link "Criar categoria") mesmo tendo
   categorias cadastradas. Comportamento coerente com o formulário ser
   só de despesa nesta feature, mas quis confirmar antes de virar
   tarefa, já que muda uma condição hoje simples.
