# Plano técnico — FEAT-20: Detalhe da Despesa e Acertos Finos de Transações

## Camadas afetadas

Só frontend, dentro de `frontend/app/src/`. Nenhuma camada do backend é
tocada, nenhum contrato de API muda.

| Arquivo | O que muda |
| --- | --- |
| `lib/categories/CategoryLetterTile.tsx` (novo) | Extrai o tile decorativo (24×24, borda, inicial do nome, sem cor) hoje inline em `CategoryList` — reaproveitado também pelo novo popup de detalhe |
| `features/categories/components/CategoryList.tsx` | Passa a usar `CategoryLetterTile` em vez do markup inline equivalente (comportamento visual idêntico) |
| `features/expenses/components/ExpenseList.tsx` | Remove a cor customizada do tag de categoria; remove a coluna "Ações" e os ícones de editar/excluir; remove o `ExpenseDeleteDialog`/estado `deleteTarget` internos (a exclusão passa a ser acionada só a partir do popup de detalhe, orquestrado pelo pai); linha inteira vira clicável via nova prop `onRowClick` |
| `features/expenses/components/ExpenseDetailDialog.tsx` (novo) | Popup Modernist "Detalhe da despesa" (valor, data, categoria com `CategoryLetterTile`, descrição), botões Excluir/Editar/Fechar — sem chamada à API (usa o item já carregado na listagem) |
| `routes/ExpensesListPage.tsx` | Novo estado `detailTarget`/`deleteTarget` (além do `dialogTarget` já existente para criar/editar); orquestra a transição detalhe → editar / detalhe → excluir; aplica `max-width: 920px` ao conteúdo |
| `routes/CategoriesPage.tsx` | Aplica a mesma restrição de largura (`max-width: 920px`) |
| `app/router.tsx` | Remove a rota `expenses/:id` e o import de `ExpenseDetailPage` |
| `routes/ExpenseDetailPage.tsx` + teste | **Removidos** |
| `features/expenses/components/ExpenseNotFound.tsx` | **Removido** — sem consumidores após a remoção de `ExpenseDetailPage` |
| `lib/categories/CategoryBadge.tsx` + teste | **Removidos** — sem consumidores após a remoção de `ExpenseDetailPage` (era o único lugar que ainda o usava) |

Fora desta tabela — **não tocados**: `ExpenseFormDialog`, `ExpenseForm`,
`ExpenseDeleteDialog`, `ExpenseFilters`, `useExpensesQuery`,
`useExpense`, `expensesApi`, qualquer rota fora de `/expenses` e
`/categories`.

## Decisão técnica: `CategoryLetterTile` — extraído para `lib/categories/`

Hoje `CategoryList` (FEAT-19) tem o markup do tile inline (span 24×24,
borda `--color-divider`, inicial do nome, sem cor). Como o popup de
detalhe de despesa (`ExpenseDetailDialog`, novo) precisa do mesmo
elemento visual, ele é extraído para um componente pequeno e
compartilhado:

```ts
interface CategoryLetterTileProps {
  name: string
}
```

Diferente de `CategoryBadge` (que está sendo removido por vazar
`cor`/`icone` reais e por só ter um consumidor fora do Modernist),
`CategoryLetterTile` é seguro de compartilhar: não depende de dado de
cor/ícone (só a primeira letra do nome), e os dois consumidores
(`CategoryList`, `ExpenseDetailDialog`) já são telas 100% Modernist —
não há risco de vazar o design system para uma tela shadcn/ui ainda
não migrada.

## Decisão técnica: `ExpenseList` perde a coluna de ações e os popups internos

```ts
interface ExpenseListProps {
  items: ExpenseQueryItem[]
  isLoading: boolean
  isLoadingMore: boolean
  error: Error | null
  hasMore: boolean
  onLoadMore: () => void
  onRowClick: (item: ExpenseQueryItem) => void
}
```

- Tabela volta a ter só 4 colunas (Categoria/Descrição/Data/Valor),
  igual ao design de referência — sem "Ações"
- `<tr onClick={() => onRowClick(item)}>` — como não há mais nenhum
  elemento interativo dentro da linha (os ícones saíram), não é
  preciso `stopPropagation()` em lugar nenhum
- `ExpenseDeleteDialog` e o estado `deleteTarget` saem de
  `ExpenseList` — a exclusão só é acionada a partir do popup de
  detalhe agora, então esse estado sobe para `ExpensesListPage`
  (mesmo lugar que já guarda `dialogTarget` para criar/editar)
- Categoria: `<span className="tag tag-neutral">{category.nome}</span>`
  — sem `style={{ color: category.cor }}`

## Decisão técnica: `ExpenseDetailDialog` (novo)

```ts
interface ExpenseDetailDialogProps {
  expense: ExpenseQueryItem | null
  onOpenChange: (open: boolean) => void
  onEdit: (expense: ExpenseQueryItem) => void
  onDelete: (expense: ExpenseQueryItem) => void
}
```

- Mesmo padrão de painel próprio (`.dialog-backdrop`/`.dialog`,
  `role="dialog"`, fecha em Esc/backdrop/"Fechar") já usado em
  `ExpenseFormDialog`/`ExpenseDeleteDialog`
- Usa `useCategories()` (já um hook compartilhado, sem custo extra) só
  para resolver `categoryId` → nome/tile, igual a `ExpenseList`
- Sem chamada à API — os dados vêm do item já carregado na listagem
  (`ExpenseQueryItem`), coerente com o design (`t.open` não recarrega
  a despesa, só abre o popup com os dados que já tinha na tela)
- Botão "Excluir" (`.btn.btn-ghost`, cor de acento, igual ao design):
  chama `onDelete(expense)` e fecha (`onOpenChange(false)`)
- Botão "Editar" (`.btn.btn-secondary`): chama `onEdit(expense)` e
  fecha
- Botão "Fechar" (`.btn.btn-primary`): só fecha

## Decisão técnica: `ExpensesListPage` orquestra a troca entre popups

```ts
type ExpenseFormTarget = { mode: 'create' } | { mode: 'edit'; id: string } | null

const [formTarget, setFormTarget] = useState<ExpenseFormTarget>(null)
const [detailTarget, setDetailTarget] = useState<ExpenseQueryItem | null>(null)
const [deleteTarget, setDeleteTarget] = useState<ExpenseQueryItem | null>(null)

function handleEditFromDetail(item: ExpenseQueryItem) {
  setDetailTarget(null)
  setFormTarget({ mode: 'edit', id: item.id })
}

function handleDeleteFromDetail(item: ExpenseQueryItem) {
  setDetailTarget(null)
  setDeleteTarget(item)
}
```

- `ExpenseList` chama `onRowClick={setDetailTarget}`
- `ExpenseDetailDialog` recebe `detailTarget`, `onEdit=
  {handleEditFromDetail}`, `onDelete={handleDeleteFromDetail}`
- `ExpenseDeleteDialog` (que já existia, hoje dentro de `ExpenseList`)
  sobe para cá, com o mesmo padrão de `key` por alvo já usado em
  outras telas (`key={deleteTarget?.id ?? 'closed'}`), `onDeleted`
  chama `query.removeItem` e limpa `deleteTarget`
- Como as trocas (`setDetailTarget(null)` + `setFormTarget(...)` ou
  `setDeleteTarget(...)`) acontecem dentro do mesmo handler de evento,
  React agrupa as duas atualizações num único re-render — não há
  instante visual em que dois popups apareçam sobrepostos

## Decisão técnica: restrição de largura do conteúdo

Mesmos valores do design de referência
(`frontend/design-system/jrnexpenses-web.dc.html`, wrapper do
conteúdo principal):

```ts
style={{ maxWidth: '920px', margin: '0 auto', padding: '40px 40px 60px', boxSizing: 'border-box' }}
```

Aplicado ao `style` do `<div className="ds-modernist">` raiz de
`ExpensesListPage` e `CategoriesPage` (mesclado com o `display:flex;
flexDirection:column; gap:...` que já existe) — não em `AppShell`
(que envolve todas as rotas, migradas ou não), para não afetar
páginas ainda em shadcn/ui.

## Remoção de `ExpenseDetailPage`/`ExpenseNotFound`/`CategoryBadge`

- `app/router.tsx`: remove a entrada `{ path: 'expenses/:id', element:
  <ExpenseDetailPage /> }` e o import correspondente — acessar a URL
  diretamente fica sem destino, mesma decisão já tomada nas FEAT-17/18
  para as rotas de cadastro/edição
- `ExpenseDetailPage.tsx` (+ teste): deletado
- `ExpenseNotFound.tsx` (`features/expenses/`): deletado — seu único
  consumidor era `ExpenseDetailPage`
- `CategoryBadge.tsx` (+ teste, `lib/categories/`): deletado — seu
  único consumidor restante era `ExpenseDetailPage` (o `CategoryList`
  já tinha parado de usá-lo na FEAT-19)

## Recursos AWS

**Nenhum.** Só frontend (React/CSS), sem novo endpoint, sem
infraestrutura.

## Mapeamento de erros

Sem mudança — nenhum erro novo é introduzido. `ExpenseDetailDialog`
não chama API, então não tem estado de erro próprio; os erros de
editar/excluir continuam tratados exatamente como hoje dentro de
`ExpenseFormDialog`/`ExpenseDeleteDialog`, só reabertos a partir de um
gatilho diferente (o popup de detalhe, em vez do ícone da linha).

## Testes afetados

- `CategoryLetterTile.test.tsx` (novo, pequeno): renderiza a inicial
  do nome em maiúscula
- `CategoryList.test.tsx`: sem mudança de comportamento esperado (só
  troca de markup inline por `CategoryLetterTile`) — ajustar só se
  algum teste depender da estrutura DOM exata do tile
- `ExpenseList.test.tsx`: remove os testes de link/botão de
  editar/excluir por linha (saíram do componente); adiciona teste de
  `onRowClick` chamado ao clicar na linha; teste de categoria sem
  `style` de cor
- `ExpenseDetailDialog.test.tsx` (novo): renderiza valor/data/categoria/
  descrição; "Editar" chama `onEdit` e fecha; "Excluir" chama
  `onDelete` e fecha; "Fechar"/Esc/backdrop só fecham
- `ExpensesListPage.test.tsx`: novo caso — clicar numa linha abre o
  popup de detalhe; "Editar" no detalhe abre o popup de edição;
  "Excluir" no detalhe abre a confirmação de exclusão
- `CategoriesPage.test.tsx`: sem mudança de comportamento esperado
  (só CSS de largura, não testado via RTL)
- Remover `ExpenseDetailPage.test.tsx`, `CategoryBadge.test.tsx`

## Resumo das decisões

1. Ícones de editar/excluir saem da tabela; a linha inteira abre um
   novo popup de detalhe (`ExpenseDetailDialog`), que só orquestra os
   popups de editar/excluir já existentes — não duplica lógica de
   formulário nem de exclusão
2. `ExpenseDeleteDialog` sobe de `ExpenseList` para `ExpensesListPage`,
   já que a exclusão só é acionada a partir do detalhe agora
3. `CategoryLetterTile` extraído para `lib/categories/` e
   compartilhado entre `CategoryList` e `ExpenseDetailDialog` — seguro
   por não depender de `cor`/`icone` reais e por só ser usado em telas
   já 100% Modernist
4. `/expenses/:id`, `ExpenseDetailPage`, `ExpenseNotFound` e
   `CategoryBadge` são removidos (cadeia de órfãos)
5. `ExpensesListPage` e `CategoriesPage` ganham `max-width: 920px` +
   padding, mesmos valores do design de referência

## Pontos confirmados pelo usuário

- Sem rota de fallback para `/expenses/:id` (mesma decisão já tomada
  nas FEAT-17/18) — **ok**
- Ordem/estilo dos botões do popup de detalhe: "Excluir"
  (`btn-ghost`, cor de acento), "Editar" (`btn-secondary`), "Fechar"
  (`btn-primary`), nessa ordem da esquerda para a direita, igual ao
  design de referência — **ok**
