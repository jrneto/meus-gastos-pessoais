# FEAT-15: Migração para o design system Modernist — Menu

## Objetivo

Continuar a migração visual iniciada na FEAT-14 (Login), agora sobre a
**navegação principal do app** (sidebar desktop, barra inferior mobile e
o painel "Mais"): recriar todos os itens de menu hoje existentes usando
a linguagem visual do design system **Modernist**, e transformar o único
item de menu que hoje aponta para uma funcionalidade inexistente
("Relatórios", hoje desabilitado/sem rota) em um item clicável que leva
a uma página fake, em vez de ficar cinza e sem ação.

## Contexto

A FEAT-14 introduziu o Modernist isolado à tela de Login, convivendo com
shadcn/ui + Tailwind no restante do app (ver `frontend/docs/
constitution.md`, seção "Stack", e
`frontend/specs/FEAT-14-design-system-modernist-login/plan.md` para o
racional do escopo `.ds-modernist`). Esta feature estende essa migração
para a **casca de navegação** (`components/nav/`: `AppShell`,
`DesktopSidebar`, `MobileBottomNav`, `NavMoreSheet`, `NavItemRow`,
`navConfig`) — não para o conteúdo das páginas que o menu abre
(`HomePage`, `ExpensesListPage`, `CategoriesPage`, `SettingsPage`
continuam em shadcn/ui + Tailwind, migradas em specs futuras, uma tela
de cada vez, mesmo princípio da FEAT-14).

O design de referência (`frontend/design-system/jrnexpenses-web.dc.html`,
seção `isApp`) mostra uma sidebar com um item por área de negócio e um
rodapé de conta/logout; o protótipo mobile
(`design_handoff_jrnexpenses_prototype/`) mostra uma barra de abas
inferior de 4 itens. Nenhum dos dois documenta um menu com subitens
(grupo pai + filhos) — no design, "Despesas" é um único item de
navegação, e criar uma despesa é uma ação (botão) dentro da própria tela
de listagem, não um item de menu à parte.

O menu real do app hoje (`navConfig.ts`, `NAV_TREE`) tem 4 itens de topo
— Início, Despesas (grupo com 2 subitens: Nova despesa e Listagem),
Relatórios, Categorias, Configurações — na verdade 5 contando
Configurações. Esta feature:
1. Funde o grupo "Despesas" em um único item de menu apontando para
   `/expenses` (a listagem); a ação de criar despesa passa a ser um
   botão "+ Nova despesa" dentro de `ExpensesListPage` linkando para
   `/expenses/new` (rota já existente, sem mudança de contrato) — ajuste
   mínimo e pontual nessa página, só para preservar o acesso à
   funcionalidade real que já existe; **não é uma migração visual completa
   dessa tela** (o botão usa o kit de UI atual do projeto, shadcn/ui,
   já que o restante da página continua nesse sistema até sua própria
   spec de migração)
2. "Relatórios" deixa de ser um item desabilitado/sem rota e passa a
   apontar para uma rota real que exibe uma página de placeholder
   ("em breve"), seguindo o mesmo padrão já usado em
   `/cadastro-em-breve` (FEAT-14) para funcionalidades ainda não
   implementadas
3. Todo o restante do menu (Início, Categorias, Configurações) continua
   apontando para as mesmas rotas reais de hoje, só com o visual
   recriado no Modernist

## Requisitos de negócio

- `navConfig.ts` (`NAV_TREE`) passa a ter 5 itens de topo, todos sem
  filhos: Início (`/`), Despesas (`/expenses`), Relatórios (`/reports`),
  Categorias (`/categories`), Configurações (`/settings`) — nenhum item
  fica com `status: 'disabled'` nem sem `to`; todo item de menu navega
  para algo
- `DesktopSidebar`, `MobileBottomNav` e `NavMoreSheet` são recriados com
  as classes/tokens do Modernist (mesmo escopo `.ds-modernist` vendorizado
  na FEAT-14 em `frontend/app/src/styles/modernist/`), preservando:
  - Estado ativo (`aria-current="page"`) visualmente distinto do inativo
    (na sidebar: preenchimento `--color-neutral-200` + borda esquerda
    `--color-accent`, igual ao design de referência; inativo em
    `--color-neutral-700`/`--color-neutral-500`)
  - Colapsar/expandir a sidebar desktop (funcionalidade hoje existente)
    continua funcionando, só com o visual/ícone recriados
  - O painel "Mais" (itens que não cabem na barra inferior mobile)
    continua existindo como conceito, mas deixa de usar o componente
    `Sheet` do shadcn/ui — é recriado como um painel próprio do
    Modernist (sem cantos arredondados, coerente com o resto do sistema)
- O escopo visual `.ds-modernist` do menu **não pode vazar** para o
  conteúdo das páginas que ele envolve: a área de conteúdo
  (`<Outlet />` dentro de `AppShell`) continua renderizando exatamente
  como hoje (shadcn/ui + Tailwind), sem herdar tipografia/reset do
  Modernist
- Item "Despesas" no menu aponta só para `/expenses` (listagem);
  `ExpensesListPage` ganha um botão/link "+ Nova despesa" para
  `/expenses/new` — sem esse botão, a rota de cadastro de despesa
  ficaria inacessível pelo menu
- Item "Relatórios" navega para uma nova rota protegida (dentro do
  `AppShell`, mantendo sidebar/bottom-nav visíveis) que exibe uma
  página "em breve" — sem chamada de API, sem dado real, só
  comunicando que a funcionalidade ainda não existe
- Nenhuma mudança de contrato com o backend, nenhum novo endpoint,
  nenhum recurso AWS

## User stories

### Navegação desktop

- **Given** um usuário autenticado em qualquer tela do app, em viewport
  desktop
- **When** a página carrega
- **Then** vê a sidebar recriada no Modernist, com os 5 itens (Início,
  Despesas, Relatórios, Categorias, Configurações), o item da rota atual
  destacado, e o botão de colapsar/expandir funcionando como hoje

### Navegação mobile

- **Given** um usuário autenticado em viewport mobile
- **When** a página carrega
- **Then** vê a barra inferior recriada no Modernist com os itens
  primários e um botão "Mais" para os demais, ambos com o item ativo
  destacado

### Acessar "Relatórios" (funcionalidade inexistente)

- **Given** um usuário autenticado clica em "Relatórios" (sidebar,
  barra inferior ou painel "Mais")
- **When** a navegação ocorre
- **Then** chega a uma tela dentro do app (sidebar/bottom-nav
  continuam visíveis) informando que Relatórios ainda não está
  disponível, sem erro e sem chamada de API

### Criar despesa a partir da listagem

- **Given** um usuário autenticado navega para "Despesas" no menu
- **When** a tela de listagem carrega
- **Then** vê um botão/link "+ Nova despesa" que leva a `/expenses/new`
  (fluxo de cadastro de despesa já existente, sem nenhuma mudança de
  comportamento)

### Conteúdo das páginas não migra

- **Given** qualquer página do app renderizada dentro do `AppShell`
- **When** o menu é recriado no Modernist
- **Then** a página em si (título, textos, formulários, botões que não
  sejam de navegação do menu) continua com a aparência shadcn/ui +
  Tailwind de hoje, inalterada

## Fora do escopo

- Migrar o conteúdo de `HomePage`, `ExpensesListPage` (além do botão
  "+ Nova despesa"), `CategoriesPage` ou `SettingsPage` para o Modernist
  — cada uma migra na sua própria spec futura
- Implementar Relatórios de verdade (agregação de gastos por período) —
  a página desta feature é só placeholder, sem lógica de negócio
- Qualquer alteração em `backend/`
- Provisionamento ou alteração de infraestrutura AWS
- Adicionar itens de menu que não existem hoje no app (ex.: "Membros",
  presente no design de referência para um cenário futuro de
  compartilhamento — fora do escopo por não ser uma funcionalidade
  existente)

## Critérios de aceite

- [x] `navConfig.ts` com 5 itens de topo, sem grupos/filhos, nenhum
      `status: 'disabled'` restante
- [x] `DesktopSidebar`, `MobileBottomNav`, `NavMoreSheet` recriados com
      classes/tokens do Modernist, sem shadcn `Sheet`/`Button` nesses
      três componentes
- [x] Estado ativo/inativo, colapsar/expandir (desktop) e abrir/fechar
      "Mais" (mobile) continuam funcionando
- [x] Item "Relatórios" navega para uma página "em breve" dentro do
      `AppShell`, sem erro
- [x] `ExpensesListPage` tem um botão/link "+ Nova despesa" para
      `/expenses/new`
- [x] Nenhuma página de conteúdo muda visualmente por causa desta
      feature (verificado manualmente e/ou por teste de componente)
- [x] 100% dos testes (unitários/componente) de `components/nav/` e das
      páginas afetadas passando após a migração
