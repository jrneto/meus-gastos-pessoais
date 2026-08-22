# Plano técnico — FEAT-17: Migração para o design system Modernist (Popup de Nova Despesa)

## Camadas afetadas

Só frontend, dentro de `frontend/app/src/`. Nenhuma camada do backend é
tocada, nenhum contrato de API muda.

| Arquivo | O que muda |
| --- | --- |
| `features/expenses/components/NewExpenseDialog.tsx` (novo) | Popup Modernist (`.dialog-backdrop`/`.dialog`) que hospeda `ExpenseForm`; controla abrir/fechar e aciona o refresh da listagem ao criar com sucesso |
| `features/expenses/components/ExpenseForm.tsx` | Reescrito com tokens/classes do Modernist; ganha `onSuccess`/`onCancel` (opcionais); some o alerta "Despesa registrada" e o reset pós-sucesso com formulário ainda aberto — agora quem decide o que acontece após o sucesso é o chamador (`NewExpenseDialog`, que fecha o popup). Os campos passam a ser markup próprio (não `ExpenseFormFields`) |
| `features/expenses/components/ExpenseFormFields.tsx` | **Não tocado** — descoberto durante a implementação que é compartilhado com `EditExpenseForm` (fora do escopo, continua shadcn/ui); `ExpenseForm` deixa de usá-lo e passa a ter seus próprios campos Modernist inline, para não vazar a migração para a edição de despesa |
| `features/expenses/hooks/useExpensesQuery.ts` | Ganha `refetch(): void`, que re-executa a busca com os filtros/página atuais (primeira página) — usado para atualizar a listagem depois de criar uma despesa |
| `routes/ExpensesListPage.tsx` | Botão "+ Nova despesa" deixa de ser `<Link>` e passa a abrir `NewExpenseDialog` (estado local `isAddOpen`); passa `query.refetch` como callback de sucesso |
| `app/router.tsx` | Remove a rota `expenses/new` e o import de `RegisterExpensePage` |
| `routes/RegisterExpensePage.tsx` | **Removido** (arquivo deletado) — sem outro consumidor além do router |

Fora desta tabela — **não tocados**: `EditExpensePage`,
`ExpenseDetailPage`, `expenseSchema`, `useRegisterExpense`,
`expensesApi`, `CategoryBadge`, qualquer outra rota do app.

## Decisão técnica: `NewExpenseDialog` — novo componente, não uma rota

Mesmo padrão já usado em `ExpenseDeleteDialog` (FEAT-16) e
`NavMoreSheet` (FEAT-15): painel próprio, sem lib de dialog, retorna
`null` quando fechado.

```ts
interface NewExpenseDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated: () => void
}
```

- Fecha em Esc (listener de `keydown`, igual aos dois componentes
  acima), clique no backdrop, ou botão "Cancelar" — todos chamam
  `onOpenChange(false)`, sem chamar a API
- `role="dialog"` `aria-modal="true"` (não `alertdialog` — não é uma
  confirmação destrutiva, é o mesmo padrão do `NavMoreSheet`)
- Ao receber `onSuccess` de `ExpenseForm` (submissão concluída): chama
  `onCreated()` (o pai re-executa a busca da listagem) e
  `onOpenChange(false)` (fecha o popup) — nessa ordem, para a
  listagem já estar atualizando quando o popup some
- Renderiza `ExpenseForm` passando `onSuccess`/`onCancel`; o botão
  "Cancelar" fica dentro de `.dialog-actions`, ao lado do submit de
  `ExpenseForm`

## Decisão técnica: `ExpenseForm` perde o alerta de sucesso "in-place"

Hoje `ExpenseForm` reseta o formulário e mostra "Despesa registrada"
mantendo-se aberto, pensado para cadastro em sequência numa página
dedicada. Como o usuário optou por seguir o design de referência
(fechar o popup ao salvar — ver spec.md), esse comportamento deixa de
fazer sentido: não há mais tempo de tela para o usuário ler a
confirmação antes de tudo sumir.

`ExpenseForm` passa a:
- Continuar resetando os campos após sucesso (`reset()`, inofensivo
  mesmo que o componente vá desmontar em seguida)
- Chamar `onSuccess?.()` no lugar de manter um estado de alerta local
  — quem decide o que fazer depois (fechar popup, atualizar lista) é
  quem usa o componente (`NewExpenseDialog`), não `ExpenseForm`
- O alerta de **erro** (`error`, ex.: 400 da API) continua exatamente
  como hoje — só o estado de **sucesso** muda
- Ganha um botão "Cancelar" (`.btn.btn-secondary`) ao lado do submit,
  renderizado só quando `onCancel` é passado — para não quebrar o uso
  standalone do componente em teste

## Decisão técnica: campo Categoria vira `<select>` nativo

O design de referência não documenta um componente `.select` (usa uma
lista com busca, fora do escopo desta feature — ver spec.md). Em vez
de recriar o `Select` (Radix) do shadcn/ui com decoração incompatível
com o Modernist (raio zero, sem sombra), o campo Categoria vira um
`<select class="input">` nativo:

```tsx
<select id="categoryId" className="input" {...register('categoryId')}>
  <option value="">Selecione uma categoria</option>
  {categories.map((c) => <option key={c.id} value={c.id}>{c.nome}</option>)}
</select>
```

Reaproveita a classe `.input` já vendorizada (borda/padding/fundo
consistentes com os demais campos) sem precisar de nenhuma classe
`.select` nova. `register('categoryId')` funciona diretamente (campo
nativo, sem necessidade de `Controller`) — simplifica
`ExpenseFormFields` (remove a dependência de `control`/`Controller`
usada só para o `Select` do shadcn/ui).

## Decisão técnica: `useExpensesQuery.refetch()`

```ts
interface UseExpensesQueryResult {
  // ...campos já existentes
  refetch: () => void
}
```

Implementação: reexecuta `fetchPage(filters, null, false)` usando o
`filters` já guardado no estado do hook (os últimos aplicados via
`applyFilters`) — mesma função interna já usada por
`applyFilters`/`loadMore`, só chamada sem mudar `filters` nem cursor
(sempre volta para a primeira página, igual a uma nova aplicação dos
mesmos filtros). Não é preciso nenhuma dependência nova nem cache —
o padrão de fetch já existente é suficiente.

## Decisão técnica: remoção da rota `/expenses/new`

`router.tsx` perde a entrada `{ path: 'expenses/new', element:
<RegisterExpensePage /> }` e o import correspondente.
`RegisterExpensePage.tsx` é deletado (não tem teste próprio e não é
usado em nenhum outro lugar). Acessar `/expenses/new` diretamente cai
no comportamento padrão do roteador para rota inexistente (idêntico
ao de qualquer URL não mapeada hoje — esta feature não introduz
nenhuma tela de "404", pois isso já é um comportamento existente do
app, fora do escopo desta mudança).

## `ExpensesListPage` — abrir o popup

```tsx
const [isAddOpen, setIsAddOpen] = useState(false)
// ...
<button type="button" className="btn btn-primary" onClick={() => setIsAddOpen(true)}>
  + Nova despesa
</button>
<NewExpenseDialog
  open={isAddOpen}
  onOpenChange={setIsAddOpen}
  onCreated={query.refetch}
/>
```

## Recursos AWS

**Nenhum.** Só frontend (React/CSS), sem novo endpoint, sem
infraestrutura.

## Mapeamento de erros

Sem mudança — os mesmos erros tipados já existentes continuam
mapeados igual, só a camada visual que os exibe muda:

| Erro | Onde aparece | Tratamento (inalterado) |
| --- | --- | --- |
| Validação Zod (`expenseSchema`) | Dentro do popup, inline por campo | Mesmas mensagens de hoje |
| Erro 400 da API (`ValidationError`) | Dentro do popup | Mensagem "Não foi possível registrar", dados preservados, popup não fecha |
| `SessionExpiredError` | — | Limpa a sessão (já tratado em `useRegisterExpense`), sem mudança |

## Testes afetados

- `ExpenseForm.test.tsx`: ajustar o teste de sucesso — em vez de
  esperar o alerta "Despesa registrada" com o formulário ainda
  aberto, verificar que `onSuccess` é chamado (passando um spy) após
  o submit válido; demais casos (categoria vazia, erro de validação,
  erro 400) continuam cobertos com o novo markup
- `NewExpenseDialog.test.tsx` (novo): abre/fecha via
  `open`/`onOpenChange`; fecha ao pressionar Esc; fecha ao clicar no
  backdrop; fecha ao clicar em "Cancelar" sem chamar a API; ao
  cadastrar com sucesso, chama `onCreated` e fecha o popup
  (`onOpenChange(false)`)
- `ExpenseFormFields.tsx` não tem teste próprio hoje (coberto via
  `ExpenseForm.test.tsx`) — sem mudança nessa cobertura
- `useExpensesQuery.test.ts`: novo caso para `refetch()` — reexecuta a
  busca com os filtros atuais, primeira página
- `ExpensesListPage.test.tsx`: o teste existente do link "+ Nova
  despesa" (`href="/expenses/new"`) é substituído por um teste que
  clica no botão e verifica que o popup abre (`role="dialog"` visível
  com os campos do formulário)
- Remover/ajustar qualquer teste que dependesse da rota `expenses/new`
  ou de `RegisterExpensePage`

## Resumo das decisões

1. `/expenses/new` é removida; `NewExpenseDialog` (novo) abre por cima
   de `ExpensesListPage`, mesmo padrão de painel próprio das FEAT-15/16
2. Ao salvar com sucesso, o popup fecha imediatamente (decisão do
   usuário, segue o design de referência) — `ExpenseForm` perde seu
   alerta de sucesso "in-place" e passa a delegar isso a quem o usa
   via `onSuccess`
3. `useExpensesQuery` ganha `refetch()` para atualizar a listagem
   depois de criar uma despesa, reaproveitando o fetch já existente
4. Campo Categoria vira `<select class="input">` nativo, sem
   `Controller`/Radix — mais simples que recriar o `Select` do
   shadcn/ui, e a interação continua a mesma (uma categoria por vez)
5. `RegisterExpensePage.tsx` é deletado; nenhuma tela de fallback é
   criada para a rota removida

## Pontos confirmados pelo usuário

- Sem rota de fallback/compatibilidade para `/expenses/new` — a rota
  simplesmente deixa de existir — **ok**
- Texto/rótulo "+ Nova despesa" inalterado, só muda de `<Link>` para
  `<button>` — **ok**
