# Plano técnico — FEAT-16: Migração para o design system Modernist (Transações)

## Camadas afetadas

Só frontend, dentro de `frontend/app/src/`. Nenhuma camada do backend é
tocada, nenhum contrato de API muda.

| Arquivo | O que muda |
| --- | --- |
| `routes/ExpensesListPage.tsx` | Reescrito: `.ds-modernist` no wrapper raiz, título "Transações", monta filtros (chips + painel avançado) + tabela + paginação |
| `features/expenses/components/ExpenseFilters.tsx` | Reescrito: chips de categoria (`.tag`) + painel "Filtros avançados" colapsável (`.field`/`.input`), mesma validação Zod |
| `features/expenses/components/ExpenseList.tsx` | Reescrito como tabela (`.table`), linha navegável, ações de editar/excluir por linha, estado vazio/erro no Modernist |
| `features/expenses/components/ExpenseDeleteDialog.tsx` | Reescrito com `.dialog-backdrop`/`.dialog` do Modernist no lugar de `AlertDialog` do shadcn/ui |
| `styles/modernist/modernist.css` | Estende o arquivo vendorizado com `.tag`/`.tag-neutral`/`.tag-accent` e `.table`, escopados sob `.ds-modernist`; completa a escala de tokens `--color-neutral-100/300/400/600/800` e `--color-accent-800` (hoje só parcialmente portada) |
| `components/nav/navConfig.ts` | Só o `label` do item `id: 'expenses'` muda de `'Despesas'` para `'Transações'` — `to`, `icon`, `status`, `mobilePrimary` inalterados |
| `components/nav/navConfig.test.ts` | Ajusta a asserção do rótulo esperado para "Transações" |

Nenhum outro arquivo de `components/nav/` muda (a casca de navegação já
foi migrada na FEAT-15; esta feature só toca o texto de um `label`).

Fora desta tabela — **não tocados**: `RegisterExpensePage`,
`EditExpensePage`, `ExpenseDetailPage`, `ExpenseFormFields`,
`CategoryBadge` (`lib/categories/`), `useExpensesQuery`,
`useDeleteExpense`, `expenseFilterSchema`, `expensesApi` — toda a lógica
de dados e validação já existente é reaproveitada como está.

## Decisão técnica: `CategoryBadge` não é tocado

`CategoryBadge` (`lib/categories/CategoryBadge.tsx`) é um componente
**compartilhado** (usado por `ExpensesListPage` e por telas fora do
escopo desta feature, como `CategoriesPage`/`ExpenseDetailPage`, que
continuam shadcn/ui). Migrá-lo para o Modernist vazaria o novo visual
para telas que a FEAT-16 explicitamente não migra.

Em vez disso, a coluna "Categoria" da nova tabela renderiza um `.tag`
próprio, montado localmente em `ExpenseList.tsx` (nome da categoria +
`style={{ color: category.cor }}` inline, mesmo dado hoje consumido de
`useCategories()`), sem importar `CategoryBadge`. O mesmo vale para os
chips de filtro em `ExpenseFilters.tsx`.

## Decisão técnica: chips de filtro por categoria

Chip = `<button type="button" class="tag ...">`, uma por categoria
(via `useCategories()`), com dois estados visuais:

- **Selecionada** (`categoryId` do form === a categoria do chip):
  `background: var(--color-accent-100)`, `color:
  var(--color-accent-700)`, `border: 1px solid var(--color-accent)`
- **Não selecionada**: `background: var(--color-neutral-100)`,
  `color: var(--color-neutral-800)`, `border: 1px solid
  var(--color-divider)`

Clique em um chip não selecionado chama `setValue('categoryId',
category.id)` + `handleSubmit(onApply)` (aplica na hora, sem esperar
outro submit); clique no chip já selecionado limpa (`setValue(
'categoryId', '')` + reaplica). Continua um `Controller`/campo único do
mesmo `expenseFilterSchema` — troca é só de controle visual (chips no
lugar do `Select` do shadcn/ui), o valor e a validação são os mesmos.

## Decisão técnica: painel "Filtros avançados"

Estado local `advancedOpen` (`useState(false)`) em `ExpenseFilters`,
sem lib nova. Contém os campos que hoje já existem no form
(`yearMonth`, `dateFrom`, `dateTo`, `minAmount`, `maxAmount`) dentro de
um container com `border: 1px solid var(--color-divider)` (reaproveita
o padrão inline já usado no design de referência — não introduz uma
classe `.card` nova só para isso, já que o design usa borda simples
aqui, diferente de `.card`/`elev-*`).

Indicador de "algum filtro avançado ativo": ponto (`•`) em
`var(--color-accent)` no botão "Filtros avançados" quando qualquer um
desses 5 campos tem valor não vazio no form atual (`watch()` do React
Hook Form), replicando o `hasActiveAdvancedFilters` do design de
referência.

Botão "Filtrar" atual (`<Button type="submit">Filtrar</Button>`) é
preservado dentro do painel (ou logo abaixo dele) como
`.btn.btn-primary` — sem ele, aplicar filtros avançados exigiria
submeter por outro caminho, o que mudaria comportamento.

## Decisão técnica: tabela em `ExpenseList`

Estrutura `<table class="table">` com `<thead>` (Categoria / Descrição
/ Data / Valor) e `<tbody>` de `<tr>` clicável (`onClick` navega para
`/expenses/:id`, mesmo destino do `<Link>` de hoje — usa
`useNavigate()` em vez de aninhar `<Link>` dentro de `<tr>`, já que
`<tr>` não aceita `<a>` como filho direto de forma válida/acessível).
Ações de editar/excluir continuam por linha, como ícone
(`Pencil`/`Trash2`, já dependência do projeto) com `stopPropagation()`
no clique para não disparar a navegação da linha — preserva o
comportamento atual onde clicar no texto navega e clicar no ícone não.

Paginação: botão "Carregar mais" recriado como
`.btn.btn-secondary`, mesma condição `hasMore`/rótulo
`isLoadingMore ? 'Carregando...' : 'Carregar mais'` de hoje.

Estado vazio e erro: parágrafo simples com `opacity: .55` (padrão do
design de referência para vazio) no lugar de `<Alert>` do shadcn/ui
para erro — mantém o texto atual ("Não foi possível buscar as
despesas" / mensagem do erro).

## Decisão técnica: `ExpenseDeleteDialog` sem `AlertDialog`

Mesmo padrão já estabelecido na FEAT-15 para `NavMoreSheet`: painel
próprio com `.dialog-backdrop`/`.dialog`/`.dialog-title`/
`.dialog-actions`, `role="alertdialog"` `aria-modal="true"` (mais
específico que `role="dialog"` do `NavMoreSheet`, por se tratar de uma
confirmação destrutiva), fecha em Esc/clique no backdrop/cancelar.
Mantém intactos: `open={expense !== null}`, os dois `useEffect`
(`success`→`onDeleted`, `NotFoundError`→`onDeleted` silencioso), o
estado de carregamento (`disabled` + rótulo "Excluindo...") e a
renderização do `otherError`.

## Decisão técnica: `navConfig.ts` — só o rótulo muda

```ts
{ id: 'expenses', label: 'Transações', icon: ListFilter, to: '/expenses', status: 'active', mobilePrimary: true },
```

Nenhuma outra propriedade do item muda; `DesktopSidebar`,
`MobileBottomNav`, `NavItemRow`, `NavMoreSheet` não são tocados (já
renderizam `item.label` dinamicamente, recriados na FEAT-15).

## Extensão de `modernist.css`

Classes novas a portar de `frontend/design-system/_ds/
modernist-a01587a5-394c-4dcb-a692-c51267a2ceac/styles.css` (só o que é
usado, mesma regra das FEAT-14/15):

- `.tag`, `.tag-neutral`, `.tag-accent` (chips de filtro + coluna
  Categoria da tabela)
- `.table`, `.table th`, `.table td`, `.table tbody tr:hover`

Tokens a completar (hoje só parcialmente vendorizados):
`--color-neutral-100/300/400/600/800`, `--color-accent-800` — todos já
existem na origem, só faltam nesta cópia vendorizada.

## Recursos AWS

**Nenhum.** Só frontend (React/CSS), sem novo endpoint, sem
infraestrutura.

## Mapeamento de erros

Sem mudança — os mesmos erros tipados já existentes continuam
mapeados igual, só a camada visual que os exibe muda:

| Erro | Onde aparece | Tratamento (inalterado) |
| --- | --- | --- |
| Erro de rede/API na busca | `ExpensesListPage`/`ExpenseList` | Parágrafo de erro no Modernist, sem mudar mensagem |
| `NotFoundError` na exclusão | `ExpenseDeleteDialog` | Remove item da lista silenciosamente (sem exibir erro) |
| Outro erro na exclusão | `ExpenseDeleteDialog` | Mensagem de erro dentro do `.dialog`, sem mudar texto |
| Validação Zod (`expenseFilterSchema`) | Painel de filtros avançados | Mensagens inline, mesmas regras (data/valor) |

## Testes afetados

- `ExpenseFilters.test.tsx`: reescrito para chips (clique
  seleciona/desseleciona categoria) + painel avançado (abrir/fechar,
  validações existentes)
- `ExpenseList.test.tsx`: reescrito para a marcação de tabela; mantém
  cobertura de navegação por linha, editar, excluir, estado vazio/erro,
  paginação
- `ExpenseDeleteDialog.test.tsx`: ajustado para o novo markup
  (`.dialog`), mantendo os mesmos casos (sucesso, `NotFoundError`,
  outro erro, cancelar)
- `ExpensesListPage.test.tsx`: ajustado para o título "Transações" e
  a nova composição; mantém o caso do link "+ Nova despesa"
- `navConfig.test.ts`: ajusta a asserção do item `expenses` para
  `label: 'Transações'`
- `DesktopSidebar.test.tsx`/`MobileBottomNav.test.tsx`: só se algum
  teste hoje asserir o texto literal "Despesas" — ajustar para
  "Transações" (nenhuma mudança de estrutura/markup esperada)

## Resumo das decisões

1. Escopo contido em `ExpensesListPage` + `ExpenseFilters` +
   `ExpenseList` + `ExpenseDeleteDialog`; nenhuma outra rota de despesa
   muda
2. `CategoryBadge` (compartilhado) não é tocado — tag de categoria é
   renderizada localmente nesta feature para não vazar o Modernist a
   telas fora do escopo
3. Filtro de categoria: chips (`.tag-neutral`/`.tag-accent`) substituem
   o `Select`, mesmo campo/validação do `expenseFilterSchema`
4. Demais filtros ficam num painel "Filtros avançados" colapsável,
   com indicador de filtro ativo, preservando toda a validação Zod
5. Listagem migra para `.table`; paginação por cursor (`hasMore`/
   "Carregar mais") preservada sem mudança de comportamento
6. Exclusão migra para `.dialog-backdrop`/`.dialog`
   (`role="alertdialog"`), mesmo padrão da FEAT-15, preservando todos
   os estados (carregamento, erro, `NotFoundError`)
7. `navConfig.ts`: só o `label` do item `expenses` muda para
   "Transações" — rota, ícone e demais propriedades inalterados
8. `modernist.css` ganha `.tag`/`.table` (+ tokens que faltavam),
   seguindo a mesma regra de portar só o que é usado

## Pontos confirmados pelo usuário

- Chip de categoria aplica a busca imediatamente ao clicar (diferente
  do painel avançado, que continua exigindo o botão "Filtrar") — **ok**
- Indicador de filtro avançado ativo é só o ponto visual `•`, sem
  texto adicional — **ok**
- Diálogo de exclusão usa `role="alertdialog"` (confirmação
  destrutiva), diferente do `role="dialog"` do `NavMoreSheet` (FEAT-15,
  que é navegação) — **ok**
