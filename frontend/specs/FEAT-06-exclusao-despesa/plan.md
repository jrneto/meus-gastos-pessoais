# Plan — FEAT-06: Exclusão de despesa

Referência: [`spec.md`](./spec.md). Segue o mesmo padrão arquitetural
das features anteriores (`FEAT-03-listagem-despesas/plan.md`,
`FEAT-05-edicao-despesa/plan.md`) e `frontend/docs/constitution.md`.
Reaproveita `NotFoundError`/`SessionExpiredError`/`UnknownExpenseError`
já existentes (FEAT-05) em vez de duplicar.

## Camadas afetadas

```
frontend/app/src/
├── components/
│   └── ui/
│       └── alert-dialog.tsx          # NOVO — shadcn/ui
├── features/
│   └── expenses/
│       ├── api/
│       │   └── expensesApi.ts        # + deleteExpense(token, id)
│       ├── components/
│       │   ├── ExpenseDeleteDialog.tsx # NOVO
│       │   └── ExpenseList.tsx        # + botão excluir por item, dialog, prop onDeleted
│       └── hooks/
│           ├── useDeleteExpense.ts    # NOVO
│           └── useExpensesQuery.ts    # + removeItem(id)
├── routes/
│   └── ExpensesListPage.tsx           # + passa query.removeItem para ExpenseList
└── lib/
    └── httpClient.ts                  # + método delete
```

## Decisões técnicas confirmadas

- **Popup de confirmação é o shadcn `alert-dialog`** (já disponível no
  registry do projeto, compatível com base-ui, mesma família dos
  componentes já instalados — `sheet`, `select`). Único dependency
  registry nova desta feature; sua única dependência interna
  (`button`) já existe.
- **`ExpenseList` continua props-driven, ganha só uma prop nova:
  `onDeleted: (id: string) => void`.** Quem "de fato" remove o item da
  lista é `useExpensesQuery` (dono do estado `items`) — `ExpenseList`
  não muta dado, só reporta que um `id` foi excluído, mesmo raciocínio
  já usado para `applyFilters`/`onLoadMore` (FEAT-03).
- **Qual item está com o popup aberto é estado local de UI dentro de
  `ExpenseList`** (`useState<ExpenseQueryItem | null>`), não sobe para
  `useExpensesQuery` — é puramente "o que a tela está mostrando agora",
  mesmo raciocínio do estado de colapso da sidebar (FEAT-04).
- **Um único `ExpenseDeleteDialog` é reaproveitado para qualquer item**
  (não um por item da lista) — evita montar N dialogs escondidos.
  Renderizado com `key={deleteTarget?.id ?? 'closed'}`: forçar
  remount ao trocar de item (ou reabrir) garante que o estado interno
  do `useDeleteExpense` (`isLoading`/`error`/`success`) começa limpo a
  cada exclusão, sem vazar erro de uma tentativa anterior para a
  próxima.
- **`useDeleteExpense` não recebe `id` fixo no hook** (diferente de
  `useUpdateExpense`, que é sempre para uma despesa da tela de edição)
  — `deleteExpense(id)` recebe o `id` na chamada, porque o mesmo
  `ExpenseDeleteDialog`/hook atende qualquer item da lista.
- **404 ao confirmar também dispara `onDeleted`.** Se a despesa já não
  existe mais (removida em outra aba, por exemplo), do ponto de vista
  do usuário o resultado é o mesmo — ela não deveria estar na lista.
  `ExpenseDeleteDialog` trata `NotFoundError` como "sucesso funcional"
  para fins de remoção local, mas mantém uma mensagem própria (não a
  mesma UI de sucesso silencioso) antes de fechar.
- **Erros inesperados (5xx/rede) mantêm o dialog aberto**, com um
  `Alert` inline acima do rodapé de ações — usuário pode tentar
  novamente ou cancelar, item não é removido da lista (a exclusão não
  aconteceu).
- **Sessão expirada segue o padrão reativo já estabelecido**:
  `useDeleteExpense` chama `useAuthStore.getState().clearSession()` em
  `SessionExpiredError`; o redirect para `/login` vem de
  `ProtectedRoute` reagindo à store, sem `navigate()` explícito no
  hook nem no dialog.

## Contratos técnicos

Caminhos relativos a `frontend/app/src/`.

### `lib/httpClient.ts` (acréscimo)
```ts
delete: (path: string, init?: RequestInit) =>
  request(path, { ...init, method: 'DELETE' }),
```
Sem corpo — mesmo padrão de `get`, único método novo necessário no
client HTTP compartilhado.

### `features/expenses/api/expensesApi.ts` (acréscimo)
```ts
function assertDeleteOk(response: Response): void {
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownExpenseError()
  }
}

async function deleteExpense(token: string, id: string): Promise<void> {
  const response = await safeFetch(() =>
    httpClient.delete(`/expenses/${id}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertDeleteOk(response)
}

export const expensesApi = {
  registerExpense,
  getExpenses,
  getExpenseById,
  updateExpense,
  deleteExpense,
}
```
`NotFoundError`, `SessionExpiredError`, `UnknownExpenseError` já
existentes (FEAT-05) — nenhum erro tipado novo necessário nesta
feature. Resposta 204 não tem corpo, por isso `deleteExpense` retorna
`Promise<void>` (sem `response.json()`).

### `features/expenses/hooks/useDeleteExpense.ts`
```ts
interface UseDeleteExpenseResult {
  deleteExpense: (id: string) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useDeleteExpense(): UseDeleteExpenseResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function deleteExpense(id: string): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await expensesApi.deleteExpense(token ?? '', id)
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

  return { deleteExpense, isLoading, error, success }
}
```
Mesmo formato de `useUpdateExpense` (FEAT-05), sem `id` fixo — recebido
por chamada.

### `features/expenses/hooks/useExpensesQuery.ts` (acréscimo)
```ts
function removeItem(id: string): void {
  setItems((prev) => prev.filter((item) => item.id !== id))
}
```
Mutação puramente local — não chama a API (quem chamou `DELETE
/expenses/{id}` foi `useDeleteExpense`, em outro componente). Exposto
no retorno do hook: `{ ..., removeItem }`.

### `features/expenses/components/ExpenseDeleteDialog.tsx`
```ts
interface ExpenseDeleteDialogProps {
  expense: ExpenseQueryItem | null
  onOpenChange: (open: boolean) => void
  onDeleted: (id: string) => void
}
```
`AlertDialog` (shadcn) controlado por `open={expense !== null}`.
`AlertDialogDescription` cita `expense?.description` e deixa explícito
que a ação não pode ser desfeita. `useDeleteExpense()` interno;
`useEffect` observa `success` → chama `onDeleted(expense.id)`;
`useEffect` observa `error instanceof NotFoundError` → também chama
`onDeleted(expense.id)` (despesa já não existe, remove da lista mesmo
assim), com uma `AlertDialogDescription` diferente informando que ela
já não existia. Qualquer outro erro vira `Alert` (variant
`destructive`) dentro do conteúdo do dialog, que permanece aberto.
Botão de ação usa `variant="destructive"` (já existente em
`buttonVariants`, `components/ui/button.tsx`), desabilitado durante
`isLoading`.

### `features/expenses/components/ExpenseList.tsx` (ajuste)
```ts
interface ExpenseListProps {
  // ...props já existentes
  onDeleted: (id: string) => void
}
```
Estado local `const [deleteTarget, setDeleteTarget] = useState<ExpenseQueryItem | null>(null)`.
Cada item ganha um segundo `Button` ícone (`Trash2`, `lucide-react`),
`aria-label="Excluir despesa"`, `onClick={() => setDeleteTarget(item)}`,
ao lado do botão de editar (FEAT-05) já existente. Renderiza
`<ExpenseDeleteDialog key={deleteTarget?.id ?? 'closed'} expense={deleteTarget} onOpenChange={(open) => !open && setDeleteTarget(null)} onDeleted={(id) => { onDeleted(id); setDeleteTarget(null) }} />`
uma única vez, fora do `<ul>`.

### `routes/ExpensesListPage.tsx` (ajuste)
Passa `onDeleted={query.removeItem}` para `<ExpenseList />`, junto das
props já existentes.

## Novas dependências
- **shadcn `alert-dialog`**: `npx shadcn add alert-dialog` — usado
  pelo popup de confirmação. Única dependência interna
  (`button`) já instalada.

## Recursos AWS
**Nenhum recurso novo.** Consome `DELETE /expenses/{id}`, já
implementado e provisionado (FEAT-07/FEAT-10 do backend). O `GSI2`
necessário para a exclusão já existe desde a FEAT-07 do backend.

## Mapeamento de erros

| Cenário | Origem | Erro tipado | UI |
|---|---|---|---|
| Despesa já não existe / de outro usuário | `DELETE /expenses/{id}` 404 | `NotFoundError` (reaproveitado) | Dialog informa que já não existia, remove da lista, fecha |
| Sessão expirada | `DELETE /expenses/{id}` 401 | `SessionExpiredError` (reaproveitado) | `clearSession()` → redirect automático via `ProtectedRoute` |
| Erro inesperado (5xx) | API | `UnknownExpenseError` (reaproveitado) | Alerta genérico dentro do dialog, que permanece aberto; item não é removido |
| Falha de rede/timeout | `fetch` reject | `NetworkError` (reaproveitado) | Alerta genérico dentro do dialog, que permanece aberto |

## Testes (Vitest + Testing Library + MSW)
- `features/expenses/hooks/useDeleteExpense.test.ts` — sucesso
  (`success = true`), 404 (`NotFoundError`), 401 (`SessionExpiredError`
  + verificação de `clearSession`), erro de rede (`NetworkError`) — via
  MSW mockando `DELETE /expenses/{id}`
- `features/expenses/hooks/useExpensesQuery.test.ts` — acrescenta caso
  de `removeItem` (remove só o item do `id` informado, mantém os
  demais)
- `features/expenses/components/ExpenseDeleteDialog.test.tsx` —
  fechado quando `expense` é `null`; aberto exibe descrição da despesa;
  cancelar chama `onOpenChange(false)` sem chamar a API; confirmar com
  sucesso chama a API e `onDeleted`; confirmar com 404 chama `onDeleted`
  com mensagem de "já não existia"; confirmar com 5xx mantém o dialog
  aberto com alerta, sem chamar `onDeleted` — via MSW
- `features/expenses/components/ExpenseList.test.tsx` — acrescenta:
  clicar no botão de excluir de um item abre o dialog mostrando sua
  descrição; confirmar a exclusão (via MSW) remove o item da lista
  renderizada e chama `onDeleted` com o `id` correto

Não há teste dedicado para o acréscimo em `ExpensesListPage.tsx` — é
só passar `query.removeItem` como prop, mesmo raciocínio já registrado
nos plans anteriores para composição de página.

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente.
