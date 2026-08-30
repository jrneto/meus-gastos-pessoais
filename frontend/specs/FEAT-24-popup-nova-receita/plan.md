# Plan — FEAT-24: Popup de nova receita

## Camadas afetadas

Só `frontend/app/src/features/transactions/` e o consumidor
`routes/TransactionsListPage.tsx` — nenhum arquivo fora dessa árvore.

| Arquivo | O que muda |
|---|---|
| `hooks/useRegisterTransaction.ts` | Passa a receber `tipo: 'despesa' \| 'receita'` como argumento do hook (em vez de hardcoded); usa esse valor no payload |
| `hooks/useUpdateTransaction.ts` | Mesma mudança: `tipo` vira segundo argumento do hook, ao lado de `id` |
| `components/TransactionForm.tsx` | Ganha prop obrigatória `tipo: 'despesa' \| 'receita'`; filtra categorias por esse `tipo` (em vez do `'despesa'` fixo); textos (mensagem de "sem categoria", rótulo do botão de criar) interpolam `tipo` |
| `components/TransactionFormDialog.tsx` | Ganha prop opcional `tipo` (usada só ao criar); título/rótulos derivam de `tipo` (criar) ou de `data.tipo` (editar, já carregado pela API) |
| `components/TransactionDetailDialog.tsx` | Título ("Detalhe da despesa"/"Detalhe da receita"), cor e sinal do valor (`+`/`-`) passam a depender de `transaction.tipo` |
| `components/TransactionDeleteDialog.tsx` | Título ("Excluir despesa"/"Excluir receita") passa a depender de `transaction.tipo` |
| `routes/TransactionsListPage.tsx` | Novo botão "+ Nova receita" (secundário, antes do "+ Nova despesa" primário — ordem do `.dc.html`); `TransactionFormTarget` ganha `tipo` no branch `create`; passa `tipo` pro `TransactionFormDialog` |
| Testes de cada arquivo acima | Acompanham as mudanças + cobrem os cenários novos da spec (US1-US10) |

Não muda: `api/transactionsApi.ts`, `errors/transactionErrors.ts`,
`schemas/transactionSchema.ts` (`tipo` continua fora do schema Zod —
nunca foi e continua não sendo um campo do formulário),
`schemas/transactionFilterSchema.ts`, `components/TransactionList.tsx`
(sinal/cor por tipo já implementado na FEAT-23),
`components/TransactionFilters.tsx`, `hooks/useTransactionsQuery.ts`,
`hooks/useTransaction.ts`, `hooks/useDeleteTransaction.ts`,
`lib/categories/*` (nenhum novo uso de `CategoryLetterTile` com prop
`tipo` — ver decisão técnica 4).

## Contratos técnicos

### `hooks/useRegisterTransaction.ts`

```ts
export function useRegisterTransaction(
  tipo: 'despesa' | 'receita',
): UseRegisterTransactionResult {
  // ...
  async function registerTransaction(data: TransactionFormOutput): Promise<void> {
    // ...
    await transactionsApi.registerTransaction(token ?? '', {
      description: data.description,
      amountInCents: data.amount,
      categoryId: data.categoryId,
      tipo,
      date: data.date,
    })
    // ...
  }
  // ...
}
```

### `hooks/useUpdateTransaction.ts`

```ts
export function useUpdateTransaction(
  id: string,
  tipo: 'despesa' | 'receita',
): UseUpdateTransactionResult {
  // ...
  await transactionsApi.updateTransaction(token ?? '', id, {
    description: data.description,
    amountInCents: data.amount,
    categoryId: data.categoryId,
    tipo,
    date: data.date,
  })
  // ...
}
```

### `components/TransactionForm.tsx`

```tsx
interface TransactionFormProps {
  mode?: 'create' | 'edit'
  tipo: 'despesa' | 'receita'
  transactionId?: string
  initialValues?: TransactionFormInput
  onSuccess?: () => void
  onCancel?: () => void
}

export function TransactionForm({ mode = 'create', tipo, transactionId, ... }: TransactionFormProps) {
  const registerHook = useRegisterTransaction(tipo)
  const updateHook = useUpdateTransaction(transactionId ?? '', tipo)
  // ...
  const categoriesForTipo = categories.filter((category) => category.tipo === tipo)
  // ...
  // estado vazio:
  <p>Você ainda não tem nenhuma categoria de {tipo} cadastrada.</p>
  // ...
  // botão de submit:
  {isLoading ? 'Salvando...' : mode === 'edit' ? 'Salvar alterações' : `Registrar ${tipo}`}
}
```

`categoriesForTipo` substitui `expenseCategories` em todo o arquivo
(dropdown + checagem de estado vazio). Como `tipo` já é literalmente
`'despesa'`/`'receita'`, a interpolação direta em português funciona
sem tabela de tradução (`Registrar despesa`/`Registrar receita`,
`categoria de despesa`/`categoria de receita`).

### `components/TransactionFormDialog.tsx`

```tsx
interface TransactionFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: () => void
  transactionId?: string
  tipo?: 'despesa' | 'receita' // usado só ao criar; ao editar, usa data.tipo
}

export function TransactionFormDialog({ open, onOpenChange, onSaved, transactionId, tipo }: TransactionFormDialogProps) {
  const isEdit = !!transactionId
  const { data, isLoading, error } = useTransaction(transactionId ?? '')
  const effectiveTipo = isEdit ? (data?.tipo ?? 'despesa') : (tipo ?? 'despesa')
  const title = isEdit
    ? (effectiveTipo === 'receita' ? 'Editar receita' : 'Editar despesa')
    : (effectiveTipo === 'receita' ? 'Nova receita' : 'Nova despesa')
  // ...
  {isEdit && !isLoading && data && (
    <TransactionForm mode="edit" tipo={data.tipo} transactionId={data.id} initialValues={{ ... }} ... />
  )}
  {!isEdit && <TransactionForm mode="create" tipo={effectiveTipo} onSuccess={handleSuccess} onCancel={...} />}
}
```

`effectiveTipo` só existe pra dar um valor de fallback (`'despesa'`)
durante os poucos milissegundos de `isLoading` no modo edição, antes de
`data.tipo` chegar — nesse intervalo o título pode mostrar "Editar
despesa" mesmo que a transação seja receita, corrigindo-se sozinho
assim que os dados chegam (ver "Pontos a confirmar" item 2). Uma vez
`data` carregado, `TransactionForm` sempre recebe `tipo={data.tipo}`
(nunca o fallback).

### `components/TransactionDetailDialog.tsx`

```tsx
const isIncome = transaction.tipo === 'receita'
const amountColor = isIncome ? 'var(--color-positive-700)' : 'var(--color-accent-700)'
const amountSign = isIncome ? '+ ' : '- '
const title = isIncome ? 'Detalhe da receita' : 'Detalhe da despesa'
```
Aplicado no `id="transaction-detail-title"` e na célula do valor
(`{amountSign}{formatCentsToCurrency(transaction.amountInCents)}`,
`color: amountColor`) — mesmo padrão já usado em `TransactionList.tsx`
desde a FEAT-23. `CategoryLetterTile` continua sem a prop `tipo`
(tile neutro), e nenhum outro elemento do popup muda — ver decisão
técnica 4 e "Pontos a confirmar" item 1.

### `components/TransactionDeleteDialog.tsx`

```tsx
const title = transaction?.tipo === 'receita' ? 'Excluir receita' : 'Excluir despesa'
```
Aplicado no `id="delete-transaction-title"`. Resto do popup (texto de
confirmação, botões) sem mudança.

### `routes/TransactionsListPage.tsx`

```tsx
type TransactionFormTarget =
  | { mode: 'create'; tipo: 'despesa' | 'receita' }
  | { mode: 'edit'; id: string }
  | null

// cabeçalho:
<div style={{ display: 'flex', gap: 'var(--space-2)' }}>
  <button type="button" className="btn btn-secondary" onClick={() => setFormTarget({ mode: 'create', tipo: 'receita' })}>
    + Nova receita
  </button>
  <button type="button" className="btn btn-primary" onClick={() => setFormTarget({ mode: 'create', tipo: 'despesa' })}>
    + Nova despesa
  </button>
</div>

// dialog:
<TransactionFormDialog
  key={formTarget ? (formTarget.mode === 'edit' ? formTarget.id : `create-${formTarget.tipo}`) : 'closed'}
  open={formTarget !== null}
  transactionId={formTarget?.mode === 'edit' ? formTarget.id : undefined}
  tipo={formTarget?.mode === 'create' ? formTarget.tipo : undefined}
  onOpenChange={(open) => !open && setFormTarget(null)}
  onSaved={query.refetch}
/>
```

## Decisões técnicas

1. **`tipo` entra como argumento de construção dos hooks**
   (`useRegisterTransaction(tipo)`, `useUpdateTransaction(id, tipo)`),
   não como campo do `transactionSchema`/formulário — mesmo padrão já
   usado por `id` em `useUpdateTransaction`. Mantém `tipo` como um
   valor controlado inteiramente pela UI (qual botão abriu o popup, ou
   o tipo já existente da transação), nunca como entrada livre.
2. **`TransactionForm` interpola `tipo` diretamente nos textos**
   (`Registrar ${tipo}`, `categoria de ${tipo}`) em vez de uma tabela
   de rótulos — `tipo` já é literalmente a palavra em português
   (`'despesa'`/`'receita'`), sem necessidade de mapeamento.
3. **Fallback `'despesa'` em `TransactionFormDialog` durante o
   carregamento da edição** (antes de `data.tipo` chegar) — cosmético,
   autocorrige em milissegundos; ver "Pontos a confirmar" item 2.
4. **`CategoryLetterTile` não ganha a prop `tipo` em
   `TransactionDetailDialog`** nesta feature — colorir o tile é
   "ajuste fino" reservado à FEAT-25 (decisão 2 da spec: só título e
   cor do valor mudam aqui).
5. **Sinal (`+`/`-`) adicionado ao valor do popup de detalhe**, não só
   a cor — mesmo padrão já usado em `TransactionList` (FEAT-23); vai
   um pouco além da letra da decisão 2 da spec ("título e cor"), mas
   evita a listagem mostrar sinal e o detalhe da mesma transação não
   mostrar; ver "Pontos a confirmar" item 1.
6. **Ordem dos botões replicada do `.dc.html`**: "+ Nova receita"
   (secundário) antes de "+ Nova despesa" (primário), não o contrário.
7. **`key` do `TransactionFormDialog` inclui o `tipo`** no modo criar
   (`create-despesa`/`create-receita`) — garante remount (e reset do
   `useForm` interno) se o usuário abrir "+ Nova despesa" logo após
   fechar "+ Nova receita" (ou vice-versa) rápido o suficiente pra
   reaproveitar o componente; custo zero, mesmo racional do `key`
   já usado no modo editar.

## Recursos AWS

Nenhum. Esta feature só exercita `tipo: "receita"` nas mesmas chamadas
`POST`/`PUT`/`DELETE /transactions` já publicadas e usadas desde a
FEAT-23 — nenhuma infraestrutura nova.

## Mapeamento de erros

Sem mudança em relação ao já documentado no `plan.md` da FEAT-23 —
`ValidationError`, `SessionExpiredError`, `UpdateValidationError`,
`NotFoundError`, `UnknownTransactionError` continuam cobrindo os
mesmos casos, agora também exercitados com `tipo: "receita"`. Nenhum
erro novo: como `tipo` nunca vem de input livre do usuário (decisão 1
da spec), não existe caminho pelo client para mandar um `tipo`
inválido que precise de tratamento dedicado.

## Pontos a confirmar antes do `/tasks`

1. **Sinal (`+`/`-`) no valor do popup de detalhe**, além da cor —
   não estava explícito na decisão 2 da spec ("título e cor"), mas é
   uma extensão pequena e natural (mesmo padrão já usado na listagem).
   Confirmar que entra junto, ou se deve ficar só a cor por ora.
2. **Durante o carregamento da edição** (`isLoading` em
   `TransactionFormDialog`, antes de `data.tipo` chegar), o título
   mostra "Editar despesa" por padrão, mesmo que a transação seja uma
   receita — corrige sozinho assim que os dados chegam (tipicamente
   menos de um segundo, mesma latência de hoje). Confirmar que esse
   flash cosmético é aceitável, em vez de, por exemplo, mostrar um
   título neutro ("Editar transação") enquanto carrega.
