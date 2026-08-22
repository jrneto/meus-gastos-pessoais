# FEAT-16: Migração para o design system Modernist — Transações

## Objetivo

Continuar a migração visual iniciada na FEAT-14 (Login) e na FEAT-15
(menu de navegação), agora sobre a **tela de listagem de despesas**
(`ExpensesListPage`): recriá-la com a linguagem visual do design system
**Modernist**, adotando o layout de tabela + filtros do design de
referência, e renomeá-la (página e item de menu) para **"Transações"**,
como no design.

## Contexto

A FEAT-14 migrou a tela de Login e a FEAT-15 migrou a casca de
navegação (sidebar, barra inferior, painel "Mais"), ambas deixando
explícito que o conteúdo das demais páginas migra "uma tela de cada
vez, em specs futuras" (ver `frontend/specs/
FEAT-15-design-system-modernist-menu/spec.md`, seção "Fora do
escopo"). Esta feature é essa próxima tela.

O design de referência (`frontend/design-system/jrnexpenses-web.dc.html`,
bloco `isTx`) mostra a página "Transações": título + botão "+ Nova
despesa", uma linha de chips de filtro rápido por categoria, um botão
"Filtros avançados" que abre um painel colapsável (De/Até/valor
mín./máx.), e uma tabela (Categoria/Descrição/Data/Valor) — sem
paginação por scroll infinito (o protótipo usa dado estático), mas o
app real mantém paginação por cursor (`hasMore`/"Carregar mais"), que
esta feature preserva.

O item de menu hoje rotulado "Despesas" (`navConfig.ts`, id `expenses`,
recriado na FEAT-15) passa a se chamar "Transações", coerente com o
design de referência (sidebar do protótipo usa esse rótulo para a
mesma área). A rota (`/expenses`) **não muda** — só o rótulo visível.

Escopo desta feature: **só a tela de listagem** e seus componentes
diretos (filtros, lista/tabela, diálogo de exclusão, paginação). As
rotas de cadastro (`/expenses/new`), edição (`/expenses/:id/edit`) e
detalhe (`/expenses/:id`) continuam em shadcn/ui + Tailwind, migradas
em specs futuras — mesmo princípio da FEAT-14/FEAT-15.

## Requisitos de negócio

- Nenhuma regra de negócio, validação ou contrato de API muda nesta
  feature: os mesmos filtros (`yearMonth`, `categoryId`, `dateFrom`,
  `dateTo`, `minAmount`/`maxAmount`), a mesma paginação por cursor
  (`hasMore`/"carregar mais"), a mesma exclusão de despesa
  (`useDeleteExpense`) e os mesmos erros tipados
  (`features/expenses/errors/`) continuam funcionando exatamente como
  hoje — só a camada visual muda
- `ExpensesListPage` passa a usar tokens/classes do Modernist
  (`frontend/app/src/styles/modernist/modernist.css`, mesmo escopo
  `.ds-modernist` vendorizado na FEAT-14/15), sem nenhuma classe
  Tailwind/shadcn remanescente nela nem nos componentes que ela
  compõe diretamente (`ExpenseFilters`, `ExpenseList`,
  `ExpenseDeleteDialog`)
- Título da página muda de "Minhas despesas" para "Transações"
- O item de menu hoje rotulado "Despesas" (`navConfig.ts`, `NAV_TREE`,
  id `expenses`) passa a ter `label: 'Transações'`; a rota (`to:
  '/expenses'`), o ícone, `status` e `mobilePrimary` não mudam
- Filtro por categoria vira uma linha de chips (`.tag`, um por
  categoria existente, via `useCategories`) em vez do `Select` atual:
  clicar em um chip seleciona aquela categoria como filtro (equivalente
  a escolher no `Select` de hoje); clicar de novo no mesmo chip
  limpa o filtro de categoria (equivalente a "Todas"); continua sendo
  um único `categoryId` selecionado por vez, nunca múltiplos
- Os demais filtros (mês, De, Até, valor mín., valor máx.) ficam dentro
  de um painel "Filtros avançados" colapsável (fechado por padrão),
  com um indicador visual (ponto de destaque) quando algum desses
  filtros está ativo — mesma validação Zod de hoje
  (`expenseFilterSchema`), incluindo as mensagens de erro inline
  (data inicial após final, valor mínimo maior que o máximo)
- A lista de despesas vira uma tabela (`.table`) com colunas Categoria
  (badge/tag), Descrição, Data e Valor; cada linha continua navegável
  para o detalhe (`/expenses/:id`) e mantém as ações de editar
  (`/expenses/:id/edit`) e excluir já existentes
- Paginação: o botão/estado "Carregar mais" (`hasMore`,
  `isLoadingMore`) é preservado, recriado com `.btn`/`.btn-secondary`
  do Modernist
- Estado vazio ("nenhuma despesa encontrada para os filtros
  selecionados") e estado de erro de busca continuam existindo,
  recriados com a tipografia/cor do Modernist (sem os componentes
  `Alert` do shadcn/ui)
- Diálogo de confirmação de exclusão (`ExpenseDeleteDialog`) é
  recriado como painel próprio do Modernist
  (`.dialog-backdrop`/`.dialog`, mesmo padrão introduzido na FEAT-15
  para o painel "Mais"), sem `AlertDialog` do shadcn/ui, preservando
  estado de carregamento, erro de exclusão e o tratamento especial de
  `NotFoundError` (remove o item da lista silenciosamente) já
  implementados
- O escopo visual `.ds-modernist` desta página não pode vazar para o
  `AppShell`/menu (que já é Modernist, tratado na FEAT-15) nem para as
  rotas de cadastro/edição/detalhe de despesa, que continuam
  shadcn/ui + Tailwind inalteradas
- Nenhuma mudança de contrato com o backend, nenhum novo endpoint,
  nenhum novo campo de filtro, nenhum recurso AWS

## User stories

### Acessar a listagem migrada

- **Given** um usuário autenticado navega para "Transações" no menu
  (sidebar, barra inferior ou painel "Mais")
- **When** a página `/expenses` carrega
- **Then** vê o título "Transações", o botão "+ Nova despesa", a linha
  de chips de categoria, o botão "Filtros avançados" e a tabela de
  despesas, tudo com a linguagem visual do Modernist

### Filtrar por categoria via chip

- **Given** a listagem carregada com despesas de mais de uma categoria
- **When** o usuário clica em um chip de categoria
- **Then** a tabela é refeita mostrando só despesas daquela categoria
  (mesmo comportamento do `Select` atual); clicar no mesmo chip de novo
  volta a mostrar todas as categorias

### Aplicar filtros avançados

- **Given** o usuário abre o painel "Filtros avançados"
- **When** preenche De/Até e/ou valor mín./máx. válidos e aplica
- **Then** a tabela é refeita respeitando os filtros combinados
  (categoria via chip + avançados), idêntico ao comportamento atual de
  `applyFilters`

### Validação dos filtros avançados

- **Given** o painel "Filtros avançados" aberto
- **When** o usuário informa data inicial depois da final, ou valor
  mínimo maior que o máximo
- **Then** vê a mensagem de erro inline correspondente, com a
  tipografia/cor de erro do Modernist, sem chamar a API

### Paginação

- **Given** a listagem tem mais despesas do que a primeira página
  retornada
- **When** o usuário clica em "Carregar mais"
- **Then** as despesas seguintes são anexadas à tabela, com o rótulo
  de carregamento igual ao atual ("Carregando...")

### Excluir uma despesa

- **Given** o usuário clica no ícone de excluir em uma linha da tabela
- **When** confirma no diálogo Modernist de exclusão
- **Then** a despesa é removida da API e da tabela, com o mesmo
  tratamento de erro (inclusive despesa já excluída) de hoje

### Menu renomeado

- **Given** qualquer tela do app com o menu visível
- **When** o usuário olha para a sidebar/barra inferior/painel "Mais"
- **Then** vê o item "Transações" (não mais "Despesas") apontando para
  a mesma rota `/expenses`

### Páginas fora do escopo não migram

- **Given** o usuário acessa cadastro, edição ou detalhe de uma despesa
- **When** essas telas carregam
- **Then** continuam com a aparência shadcn/ui + Tailwind de hoje,
  inalteradas

## Fora do escopo

- Migrar `RegisterExpensePage`, `EditExpensePage`, `ExpenseFormFields`
  ou `ExpenseDetailPage` para o Modernist — cada uma migra em spec
  futura própria
- Migrar `CategoriesPage`, `HomePage` ou `SettingsPage` — fora do
  escopo desta feature
- Qualquer mudança nas regras de negócio de filtro, paginação ou
  exclusão de despesas
- Seleção múltipla de categorias no filtro (o design e o app hoje só
  suportam uma categoria por vez)
- Qualquer alteração em `backend/`
- Provisionamento ou alteração de infraestrutura AWS

## Critérios de aceite

- [x] `ExpensesListPage` renderiza com título "Transações" e classes/
      tokens do Modernist, sem nenhuma classe Tailwind/shadcn
      remanescente nela, em `ExpenseFilters`, `ExpenseList` ou
      `ExpenseDeleteDialog`
- [x] Item de menu "Despesas" renomeado para "Transações" em
      `navConfig.ts` (`NAV_TREE`), rota `/expenses` inalterada
- [x] Filtro de categoria funciona via chips (seleciona/limpa),
      equivalente ao `Select` atual
- [x] Painel "Filtros avançados" colapsável com mês, De, Até, valor
      mín./máx., mesma validação e mensagens de erro de hoje
- [x] Listagem migra para tabela (Categoria/Descrição/Data/Valor),
      preservando navegação para detalhe, edição e exclusão por linha
- [x] Paginação "Carregar mais" preservada com o mesmo comportamento
      (`hasMore`/`isLoadingMore`)
- [x] Estados vazio e de erro de busca recriados no Modernist
- [x] Diálogo de exclusão recriado como `.dialog-backdrop`/`.dialog`
      do Modernist, preservando todo o comportamento atual
      (carregamento, erro, `NotFoundError`)
- [x] Nenhuma página de cadastro/edição/detalhe de despesa, nem
      qualquer outra tela do app, muda visualmente por causa desta
      feature
- [x] 100% dos testes (unitários/componente) de
      `features/expenses/` e `routes/ExpensesListPage` passando após
      a migração (226/226, `tsc -b`, `oxlint` e `npm run build`
      limpos)
