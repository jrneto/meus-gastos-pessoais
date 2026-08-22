# Plano técnico — FEAT-19: Migração para o design system Modernist (CRUD de Categorias)

## Camadas afetadas

Só frontend, dentro de `frontend/app/src/`. Nenhuma camada do backend é
tocada, nenhum contrato de API muda.

| Arquivo | O que muda |
| --- | --- |
| `features/categories/hooks/useRegisterCategory.ts` | Passa a expor `data: CategoryItem \| null` (o retorno de `createCategory`, hoje descartado) — necessário para `CategoriesPage` inserir a categoria criada na lista local sem um novo fetch |
| `features/categories/hooks/useUpdateCategory.ts` | Mesma mudança: expõe `data: CategoryItem \| null` |
| `features/categories/components/CategoryForm.tsx` (novo, une `NewCategoryForm` + `EditCategoryForm`) | `mode?: 'create' \| 'edit'`, `categoryId?: string`, `initialValues?: CategoryFormInput`, `onSaved: (category: CategoryItem) => void`, `onNotFound?: () => void`, `onCancel: () => void` — Modernist, campos próprios (não `CategoryFormFields`, removido) |
| `features/categories/components/IconPicker.tsx` | Reescrito com tiles Modernist (`.icon-tile`), no lugar das classes Tailwind atuais — mesma interface (`value`/`onChange`/`error`) |
| `features/categories/components/CategoryList.tsx` | Reescrito no Modernist; cada linha renderiza ícone/cor/nome localmente (sem `CategoryBadge`, compartilhado com `ExpenseDetailPage`, fora do escopo); botão "Editar" expande a própria linha num `CategoryForm` inline; botão excluir abre `CategoryDeleteDialog` |
| `features/categories/components/CategoryDeleteDialog.tsx` | Reescrito com `.dialog-backdrop`/`.dialog` (`role="alertdialog"`), mesmo padrão de `ExpenseDeleteDialog`, no lugar do `AlertDialog` do shadcn/ui |
| `routes/CategoriesPage.tsx` | Estado único `formTarget: {mode:'create'} \| {mode:'edit', id} \| null` (mesmo princípio de `ExpensesListPage`/`dialogTarget`); "+ Nova categoria" alterna `formTarget`; formulário inline de cadastro renderizado condicionalmente; atualiza `items` local ao salvar (create/edit) via `onSaved` |
| `features/expenses/components/ExpenseForm.tsx` | Só o destino do link "Criar categoria" (estado sem categorias) muda de `/categories/new` para `/categories` — fora do escopo visual, ajuste mecânico |
| `app/router.tsx` | Remove as rotas `categories/new` e `categories/:id/edit`, e os imports de `NewCategoryPage`/`EditCategoryPage` |
| `routes/NewCategoryPage.tsx`, `routes/EditCategoryPage.tsx` + testes | **Removidos** |
| `features/categories/components/NewCategoryForm.tsx`, `EditCategoryForm.tsx`, `CategoryFormFields.tsx`, `CategoryNotFound.tsx` + testes | **Removidos** — sem consumidores após a unificação em `CategoryForm` |

Fora desta tabela — **não tocados**: `categoriesWriteApi`,
`categoriesReadApi`, `categorySchema`, `categoryErrors`,
`CATEGORY_ICONS`, `CategoryBadge` (continua usado por
`ExpenseDetailPage`, fora do escopo), `useCategories`, qualquer outra
rota do app.

## Decisão técnica: hooks de escrita passam a expor a categoria retornada

```ts
interface UseRegisterCategoryResult {
  registerCategory: (data: CategoryFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
  data: CategoryItem | null
}
```

(mesma mudança em `useUpdateCategory`). `categoriesWriteApi.createCategory`/
`updateCategory` já retornam `CategoryItem` — só o hook descartava esse
valor. Necessário porque `CategoriesPage` não tem um "refetch" de lista
(diferente de `useExpensesQuery`, que já expõe `refetch` desde a
FEAT-17) — em vez de introduzir um, a categoria criada/editada é
inserida/atualizada diretamente no array local `items` já mantido por
`CategoriesPage` hoje.

## Decisão técnica: `CategoryForm` único (create/edit), inline

Mesmo espírito da unificação já feita para despesas (FEAT-17/18), mas
sem popup — o formulário é montado diretamente no lugar onde é
mostrado (acima da lista para criar, dentro da linha para editar):

```ts
interface CategoryFormProps {
  mode?: 'create' | 'edit'
  categoryId?: string
  initialValues?: CategoryFormInput
  onSaved: (category: CategoryItem) => void
  onNotFound?: () => void // só relevante no modo edição
  onCancel: () => void
}
```

- Chama os dois hooks incondicionalmente (`useRegisterCategory()` e
  `useUpdateCategory(categoryId ?? '')`), usa o resultado de um ou
  outro conforme `mode` — mesmo padrão do `ExpenseForm`
- Ao suceder (`success && data`): `mode === 'create'` reseta os campos
  (permite cadastrar em sequência sem fechar, já que aqui não há popup
  fechando sozinho); em ambos os modos chama `onSaved(data)` — quem
  decide o que fazer depois (recolher o formulário, atualizar a lista)
  é `CategoriesPage`/`CategoryList`
- `NameConflictError`: mesmo tratamento de hoje, `setError('nome', {
  message })`, sem recolher o formulário
- No modo `edit`, se `error` for `NotFoundError` (categoria excluída
  por outra sessão), chama `onNotFound?.()` em vez de `onSaved` — o
  chamador remove a categoria da lista local e recolhe a linha, sem
  exibir erro (diferente de `ExpenseForm`, que reaproveitava
  `onSuccess` para os dois casos; aqui os dois desfechos exigem ações
  diferentes no pai — inserir/atualizar vs. remover — por isso duas
  callbacks em vez de uma sobrecarregada)
- Botão "Cancelar" sempre presente (diferente de `ExpenseForm`, onde é
  opcional) — aqui não existe um popup com botão de fechar
  equivalente; cancelar é a única forma de recolher sem salvar

## Decisão técnica: `CategoryList` não usa `CategoryBadge`

Mesmo motivo já identificado na FEAT-16/17: `CategoryBadge`
(`lib/categories/`) é compartilhado com `ExpenseDetailPage`, fora do
escopo desta feature. `CategoryList` passa a renderizar ícone (via
`findCategoryIcon`) + cor + nome diretamente, sem importar
`CategoryBadge`, para não vazar o Modernist a uma tela não migrada.

## Decisão técnica: `IconPicker` — tiles Modernist

Sem equivalente no design de referência (o seletor de ícone é uma
funcionalidade própria do app, introduzida na FEAT-13, sem página
correspondente em `jrnexpenses-web.dc.html`). Recriado com uma nova
classe `.icon-tile` em `modernist.css` (grade de botões quadrados,
borda `--color-divider`, estado selecionado com borda/fundo de acento
— mesmo padrão visual já usado para os chips de filtro `.tag` da
FEAT-16), mantendo a interface atual (`value`/`onChange`/`error`) e o
catálogo `CATEGORY_ICONS` inalterado.

## Decisão técnica: campo Cor

Mantém o `<input type="color">` nativo (sem equivalente Modernist —
é um widget do navegador, não decorado por CSS de design system),
envolto em `.field`, com o valor hex exibido ao lado como texto
(`color-mix`/opacity do Modernist), mesmo comportamento de hoje.

## Decisão técnica: `CategoriesPage` — estado único para os dois modos

```ts
type CategoryFormTarget = { mode: 'create' } | { mode: 'edit'; id: string } | null

const [items, setItems] = useState<CategoryItem[]>([])
const [formTarget, setFormTarget] = useState<CategoryFormTarget>(null)

function handleSaved(category: CategoryItem) {
  setItems((prev) => {
    const exists = prev.some((c) => c.id === category.id)
    return exists ? prev.map((c) => (c.id === category.id ? category : c)) : [...prev, category]
  })
  setFormTarget(null)
}

function handleNotFound(id: string) {
  setItems((prev) => prev.filter((c) => c.id !== id))
  setFormTarget(null)
}
```

- "+ Nova categoria": alterna `formTarget` entre `{mode:'create'}` e
  `null` — clicar de novo com o formulário já aberto fecha (mesmo
  princípio de alternância usado no botão "Filtros avançados" da
  FEAT-16)
- `CategoryList` recebe `editingId={formTarget?.mode === 'edit' ?
  formTarget.id : null}` e `onEditToggle={(id) => setFormTarget((cur)
  => cur?.mode === 'edit' && cur.id === id ? null : { mode: 'edit', id
  })}` — clicar "Editar" na linha já aberta fecha, clicar em outra
  linha troca qual está aberta
- Como `formTarget` é uma única variável, abrir o cadastro fecha
  qualquer edição em andamento e vice-versa — cumpre o requisito de
  "só um formulário aberto por vez" por construção, sem lógica extra

## Recursos AWS

**Nenhum.** Só frontend (React/CSS), sem novo endpoint, sem
infraestrutura.

## Mapeamento de erros

Sem mudança nos erros em si — só onde/como aparecem:

| Erro | Onde aparece | Tratamento |
| --- | --- | --- |
| `NameConflictError` | Campo Nome, inline | Inalterado |
| Outro erro de validação (`ValidationError`) | Dentro do formulário inline | Mensagem de erro, dados preservados — inalterado |
| `NotFoundError` ao salvar edição | — | `CategoryForm` chama `onNotFound()`; `CategoriesPage` remove a categoria da lista e recolhe a linha, sem exibir erro (novo, específico do formulário inline — antes não existia essa situação para categorias) |
| `CategoryInUseError`/outro erro ao excluir | Dentro do popup de exclusão | Inalterado (`CategoryDeleteDialog`) |
| `SessionExpiredError` | — | Limpa a sessão (já tratado nos hooks), sem mudança |

## Testes afetados

- `useRegisterCategory.test.ts`/`useUpdateCategory.test.ts`: novo caso
  — `data` reflete a categoria retornada pela API em caso de sucesso
- `CategoryForm.test.tsx` (novo, substitui
  `NewCategoryForm.test.tsx`/`EditCategoryForm.test.tsx`): casos de
  `create` (cria e chama `onSaved`, nome duplicado, cancelar) e `edit`
  (pré-preenchido, atualiza e chama `onSaved`, 404 chama `onNotFound`
  sem exibir erro, cancelar)
- `IconPicker.test.tsx` (se existir): ajustar para o novo markup,
  mantendo a mesma cobertura de seleção/erro
- `CategoryList.test.tsx`: reescrito — "Editar" expande a linha com
  `CategoryForm` pré-preenchido; exclusão continua cobrindo o novo
  `CategoryDeleteDialog`; estado vazio sem o link removido
- `CategoryDeleteDialog.test.tsx`: ajustado para o novo markup,
  mantendo os mesmos casos (sucesso, `NotFoundError`, outro erro,
  cancelar)
- `CategoriesPage.test.tsx`: "+ Nova categoria" expande o formulário
  inline em vez de navegar; salvar insere na lista; editar uma linha
  atualiza a lista
- `ExpenseForm.test.tsx`: ajustar o `href` esperado do link "Criar
  categoria" para `/categories`
- Remover `NewCategoryPage.test.tsx`, `EditCategoryPage.test.tsx`,
  `NewCategoryForm.test.tsx`, `EditCategoryForm.test.tsx`

## Resumo das decisões

1. `CategoryForm` único (create/edit) e inline — sem popup, fiel ao
   design de referência; usa duas callbacks (`onSaved`/`onNotFound`)
   em vez de uma só, porque os dois desfechos exigem ações diferentes
   no pai (inserir/atualizar vs. remover da lista)
2. `useRegisterCategory`/`useUpdateCategory` passam a expor a
   categoria retornada pela API, evitando introduzir um endpoint/hook
   de refetch só para atualizar a lista local
3. `CategoryList` não usa `CategoryBadge` (compartilhado com
   `ExpenseDetailPage`, fora do escopo) — ícone/cor/nome renderizados
   localmente
4. `IconPicker` recriado com uma nova classe `.icon-tile`, sem
   equivalente no design de referência (funcionalidade própria do
   app, FEAT-13)
5. `CategoriesPage` usa um único estado (`formTarget`) para cadastro e
   edição, garantindo por construção que só um formulário fica aberto
   por vez
6. `/categories/new`, `/categories/:id/edit` e todos os componentes
   sem consumidor após a unificação são removidos; o link "Criar
   categoria" em `ExpenseForm` (fora do escopo) é redirecionado para
   `/categories`

## Pontos confirmados pelo usuário

- Sem rota de fallback para `/categories/new`/`/categories/:id/edit`
  (mesma decisão já tomada nas FEAT-17/18) — **ok**
- Botão "Cancelar" do `CategoryForm` sempre visível (diferente de
  `ExpenseForm`, onde é opcional), já que aqui não existe popup com
  fechamento por Esc/backdrop — **ok**
