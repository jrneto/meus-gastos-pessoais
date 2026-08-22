# Plano técnico — FEAT-18: Migração para o design system Modernist (Popup de Editar Despesa)

## Camadas afetadas

Só frontend, dentro de `frontend/app/src/`. Nenhuma camada do backend é
tocada, nenhum contrato de API muda.

| Arquivo | O que muda |
| --- | --- |
| `features/expenses/hooks/useExpense.ts` | Passa a pular a busca quando `id` é vazio (`isLoading: false`, sem chamar a API) — necessário porque o popup unificado chama este hook mesmo no modo cadastro, onde não há id |
| `features/expenses/components/ExpenseForm.tsx` | Generalizado para os dois modos: `mode?: 'create' \| 'edit'`, `expenseId?: string`, `initialValues?: ExpenseFormInput`; usa `useRegisterExpense` ou `useUpdateExpense` conforme o modo; rótulo do botão muda conforme o modo; `NotFoundError` no modo edição aciona `onSuccess` (fecha silenciosamente, sem exibir erro) |
| `features/expenses/components/NewExpenseDialog.tsx` → renomeado para `features/expenses/components/ExpenseFormDialog.tsx` | Ganha `expenseId?: string` (presente = modo edição); busca a despesa via `useExpense` antes de renderizar `ExpenseForm`; título e estado de carregamento variam conforme o modo; 404 ao carregar fecha o popup e atualiza a listagem |
| `features/expenses/components/ExpenseList.tsx` | Ícone de editar deixa de ser um `<Link to=".../edit">`; vira um `<button onClick={() => onEdit(item)}>` — nova prop `onEdit: (item: ExpenseQueryItem) => void` |
| `routes/ExpensesListPage.tsx` | Estado local unificado (`dialogTarget: { mode: 'create' } \| { mode: 'edit'; id: string } \| null`, mesmo princípio já usado em `ExpenseDeleteDialog`/`deleteTarget`); passa `onEdit` para `ExpenseList`; usa `ExpenseFormDialog` (renomeado) com `key` por alvo, para reiniciar o formulário a cada abertura |
| `routes/ExpenseDetailPage.tsx` | Link "Editar" passa a apontar para `/expenses` em vez de `/expenses/:id/edit` (a rota removida) |
| `app/router.tsx` | Remove a rota `expenses/:id/edit` e o import de `EditExpensePage` |
| `routes/EditExpensePage.tsx` + `routes/EditExpensePage.test.tsx` | **Removidos** |
| `features/expenses/components/EditExpenseForm.tsx` + `.test.tsx` | **Removidos** |
| `features/expenses/components/ExpenseFormFields.tsx` | **Removido** — sem consumidores após a remoção de `EditExpenseForm` |

Fora desta tabela — **não tocados**: `ExpenseDeleteDialog`,
`expenseSchema`, `useRegisterExpense`, `useUpdateExpense`,
`expensesApi`, `ExpenseNotFound` (continua usado por
`ExpenseDetailPage` para o 404 de acessar `/expenses/:id` direto),
`CategoryBadge`, qualquer outra rota do app.

## Decisão técnica: `useExpense` ganha um "modo desligado"

```ts
useEffect(() => {
  if (!id) {
    setIsLoading(false)
    return
  }
  // ...fetch atual, inalterado
}, [id, token])
```

Necessário porque `ExpenseFormDialog` agora chama `useExpense(expenseId
?? '')` incondicionalmente (regra dos hooks), mesmo no modo cadastro
(sem id) — sem essa guarda, o hook chamaria `GET /expenses/` a cada
abertura do popup de cadastro. `ExpenseDetailPage`/`EditExpensePage`
(este último removido) sempre passam um id não vazio, então esse
comportamento não muda para eles.

## Decisão técnica: `ExpenseForm` ganha `mode`/`expenseId`/`initialValues`

```ts
interface ExpenseFormProps {
  mode?: 'create' | 'edit' // default 'create'
  expenseId?: string // obrigatório no modo 'edit'
  initialValues?: ExpenseFormInput
  onSuccess?: () => void
  onCancel?: () => void
}
```

- Chama **os dois** hooks (`useRegisterExpense()` e
  `useUpdateExpense(expenseId ?? '')`) incondicionalmente — regra dos
  hooks — e usa o resultado de um ou outro conforme `mode`; no modo
  `create`, `useUpdateExpense('')` nunca tem sua função de submit
  chamada, sem efeito colateral
- `useForm({ defaultValues: initialValues ?? { description: '',
  amount: '', categoryId: '', expenseDate: '' } })` — como
  `ExpenseFormDialog` só renderiza `ExpenseForm` depois que os dados
  editados já carregaram (nunca antes), não há problema do
  `defaultValues` do React Hook Form ser capturado só no mount
- Rótulo do botão: `'Salvar alterações'` (edição) vs `'Registrar
  despesa'` (cadastro), com `isLoading` sobrepondo para `'Salvando...'`
  nos dois casos
- `reset()` pós-sucesso só ocorre no modo `create` (no modo edição o
  popup fecha imediatamente de qualquer forma — resetar não teria
  efeito visível e evita um estado intermediário desnecessário)
- No modo `edit`, se `error` for `NotFoundError` (a despesa foi
  excluída por outra sessão entre abrir o popup e salvar), o
  formulário chama `onSuccess?.()` como se tivesse tido sucesso (fecha
  o popup + atualiza a listagem), **sem** mostrar o bloco de erro —
  mesmo espírito do tratamento silencioso já usado em
  `ExpenseDeleteDialog`

## Decisão técnica: `NewExpenseDialog` → `ExpenseFormDialog`

```ts
interface ExpenseFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: () => void
  expenseId?: string // presente = modo edição
}
```

- `isEdit = !!expenseId`
- Chama `useExpense(expenseId ?? '')` sempre (ver decisão acima);
  título do `.dialog-title` é `'Editar despesa'` quando `isEdit`,
  senão `'Nova despesa'`
- No modo edição, enquanto `isLoading`, mostra "Carregando..." (mesmo
  texto hoje usado em `EditExpensePage`) no lugar do formulário
- Se `isEdit && error instanceof NotFoundError` (404 ao **carregar**),
  fecha o popup e chama `onSaved()` — mesmo tratamento silencioso do
  404 ao **salvar** (tratado dentro de `ExpenseForm`, ver acima)
- Ao renderizar `ExpenseForm`, passa `mode`, `expenseId`/
  `initialValues` (calculados a partir de `data`, usando
  `centsToAmountInput` já existente em `utils/currency.ts`) só no modo
  edição
- Fecha em Esc/backdrop/"Cancelar" — comportamento inalterado da
  FEAT-17
- `onCreated` (nome da prop na FEAT-17) é renomeado para `onSaved`,
  mais correto agora que serve tanto para criar quanto para editar

## Decisão técnica: `ExpensesListPage` — estado único para os dois modos

```ts
type ExpenseDialogTarget = { mode: 'create' } | { mode: 'edit'; id: string } | null

const [dialogTarget, setDialogTarget] = useState<ExpenseDialogTarget>(null)

// "+ Nova despesa": setDialogTarget({ mode: 'create' })
// onEdit (de ExpenseList): (item) => setDialogTarget({ mode: 'edit', id: item.id })

<ExpenseFormDialog
  key={dialogTarget ? (dialogTarget.mode === 'edit' ? dialogTarget.id : 'create') : 'closed'}
  open={dialogTarget !== null}
  expenseId={dialogTarget?.mode === 'edit' ? dialogTarget.id : undefined}
  onOpenChange={(open) => !open && setDialogTarget(null)}
  onSaved={query.refetch}
/>
```

O `key` força o React a remontar `ExpenseFormDialog` (e o `useForm`
dentro de `ExpenseForm`) a cada nova abertura — mesmo truque já usado
em `ExpenseDeleteDialog` (`key={deleteTarget?.id ?? 'closed'}`),
evitando que dados de uma edição anterior vazem para a próxima
abertura.

## Decisão técnica: `ExpenseList` delega a abertura da edição ao pai

O ícone de lápis deixa de navegar (`<Link>`) e vira um `<button
onClick={() => onEdit(item)}>`, mesmo padrão já usado para o ícone de
excluir (`onClick={() => setDeleteTarget(item)}`). Nova prop
obrigatória `onEdit: (item: ExpenseQueryItem) => void`.

## `ExpenseDetailPage` — ajuste mecânico do link "Editar"

```tsx
<Link to="/expenses" className={cn(buttonVariants({}))}>
  Editar
</Link>
```

Sem mudança de layout/estilo — só o destino, já que
`/expenses/:id/edit` deixa de existir. `ExpenseDetailPage` continua
fora do escopo visual desta feature.

## Recursos AWS

**Nenhum.** Só frontend (React/CSS), sem novo endpoint, sem
infraestrutura.

## Mapeamento de erros

Sem mudança nos erros em si — só onde/como aparecem:

| Erro | Onde aparece | Tratamento |
| --- | --- | --- |
| Validação Zod (`expenseSchema`) | Dentro do popup, inline por campo | Inalterado |
| Erro 400 da API (`ValidationError`/`UpdateValidationError`) | Dentro do popup | Mensagem de erro, dados preservados, popup não fecha — inalterado |
| `NotFoundError` ao **carregar** (`GET /expenses/{id}`) | — | `ExpenseFormDialog` fecha o popup e chama `onSaved()`, sem mostrar erro (novo, específico do popup) |
| `NotFoundError` ao **salvar** (`PUT /expenses/{id}`) | — | `ExpenseForm` chama `onSuccess()` como sucesso silencioso, sem mostrar erro (novo, específico do popup — antes virava a tela `ExpenseNotFound`) |
| `SessionExpiredError` | — | Limpa a sessão (já tratado em `useExpense`/`useUpdateExpense`), sem mudança |

## Testes afetados

- `useExpense.test.ts`: novo caso — `id` vazio não chama a API e
  `isLoading` fica `false`
- `ExpenseForm.test.tsx`: novos casos para `mode="edit"` — renderiza
  com `initialValues` preenchidos, rótulo "Salvar alterações", chama
  `useUpdateExpense`/`PUT`, sucesso chama `onSuccess`, 404 ao salvar
  chama `onSuccess` sem exibir erro; casos existentes de `create`
  continuam cobertos
- `NewExpenseDialog.test.tsx` → renomeado para
  `ExpenseFormDialog.test.tsx`: novos casos para `expenseId` — mostra
  "Editar despesa"/"Carregando...", pré-preenche o formulário, 404 ao
  carregar fecha o popup e chama `onSaved`; casos existentes (create)
  continuam cobertos com `onCreated` renomeado para `onSaved`
- `ExpenseList.test.tsx`: teste do link de editar substituído por um
  teste que clica no ícone e verifica que `onEdit` é chamado com o
  item certo
- `ExpensesListPage.test.tsx`: novo caso — clicar no ícone de editar
  de uma linha abre o popup com `role="dialog"` e os campos
  pré-preenchidos
- `ExpenseDetailPage.test.tsx` (se existir teste do link "Editar"):
  ajustar o `href` esperado para `/expenses`
- Remover `EditExpensePage.test.tsx` e `EditExpenseForm.test.tsx`

## Resumo das decisões

1. `NewExpenseDialog`/`ExpenseForm` (FEAT-17) são generalizados para
   também editar, em vez de duplicar dialog/formulário — um só popup,
   modo controlado por `mode`/`expenseId` (decisão do usuário)
2. `useExpense` ganha uma guarda para `id` vazio, necessária para o
   hook poder ser chamado incondicionalmente dentro do popup unificado
3. 404 (despesa não encontrada), tanto ao carregar quanto ao salvar,
   fecha o popup silenciosamente e atualiza a listagem — sem mostrar
   erro (decisão do usuário)
4. `/expenses/:id/edit`, `EditExpensePage`, `EditExpenseForm` e
   `ExpenseFormFields` são removidos; `ExpenseDetailPage` tem só o
   destino do link "Editar" ajustado para `/expenses`
5. `ExpenseList` passa a delegar a abertura da edição ao pai via
   `onEdit`, mesmo padrão já usado para excluir

## Pontos confirmados pelo usuário

- Nome final do componente unificado: `ExpenseFormDialog` — **ok**
- Sem rota de fallback para `/expenses/:id/edit` (mesma decisão já
  tomada na FEAT-17 para `/expenses/new`) — **ok**
