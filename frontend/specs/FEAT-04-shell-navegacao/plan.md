# Plan — FEAT-04: Navegação e menu (shell da aplicação)

Referência: [`spec.md`](./spec.md). Segue `frontend/docs/constitution.md`
(feature-based, sem dependência nova sem necessidade clara) e o padrão
já usado em `frontend/specs/FEAT-02-cadastro-despesa/plan.md` /
`FEAT-03-listagem-despesas/plan.md`.

## Camadas afetadas

Introduz um shell de navegação em `components/nav/` (cross-cutting,
usado por toda rota protegida — não pertence a nenhuma feature de
negócio), reestrutura `app/router.tsx` como rota de layout com
`<Outlet />`, e simplifica `RegisterExpensePage`/`ExpensesListPage`
(perdem cabeçalho/logout/link próprios, que agora vivem no shell).

```
frontend/app/src/
├── components/
│   ├── nav/
│   │   ├── navConfig.ts          # NOVO — árvore de navegação (registro extensível)
│   │   ├── AppShell.tsx          # NOVO — layout: DesktopSidebar + MobileBottomNav + <Outlet/>
│   │   ├── DesktopSidebar.tsx    # NOVO — sidebar colapsável (tela larga)
│   │   ├── MobileBottomNav.tsx   # NOVO — bottom nav (tela estreita)
│   │   └── NavMoreSheet.tsx      # NOVO — conteúdo do item "Mais" (mobile)
│   ├── ProtectedRoute.tsx        # inalterado
│   └── ui/
│       └── sheet.tsx             # NOVO — shadcn/ui, usado só por "Mais"
├── routes/
│   ├── HomePage.tsx              # NOVO — placeholder de Início
│   ├── SettingsPage.tsx          # NOVO — placeholder + ação "Sair"
│   ├── RegisterExpensePage.tsx   # simplificado — perde header/logout/link
│   └── ExpensesListPage.tsx      # simplificado — perde header/logout/link
└── app/
    └── router.tsx                # rota de layout (AppShell) + rotas filhas
```

## Decisões técnicas confirmadas

- **Sidebar própria, não o componente `sidebar` do shadcn/ui.** O
  registry do projeto tem um `sidebar` pronto (compatível com base-ui),
  mas seu modo mobile embutido abre como um drawer (`Sheet`) por cima
  da tela — comportamento diferente do bottom nav bar pedido na spec.
  Confirmado com o usuário: construir uma sidebar simples (só
  colapsar/expandir, sem modo mobile próprio) usando `Button` e
  `lucide-react` já existentes, evitando o conflito e a complexidade
  extra (cookie de estado, atalho de teclado, contexto) do componente
  completo, que não é necessária para o escopo desta feature.
- **Itens principais do bottom nav (mobile), confirmado com o
  usuário:** Início, Nova despesa, Listagem, Configurações — os 4
  destinos realmente navegáveis. Relatórios e Categorias (desabilitados
  hoje) ficam dentro de "Mais", já que não são clicáveis mesmo — não
  faz sentido ocupar um dos 4 slots principais com algo que não navega.
- **`AppShell` como rota de layout** (`element` da rota pai, filhos via
  `<Outlet />`), não um componente que cada página importa — mesmo
  padrão já cogitado (e adiado para esta feature) na conversa da
  FEAT-03: permite adicionar novas rotas/páginas sem tocar no shell.
- **`navConfig.ts` é a única fonte da árvore de navegação** — tanto
  `DesktopSidebar` quanto `MobileBottomNav`/`NavMoreSheet` renderizam a
  partir da mesma estrutura de dados, nunca duplicam itens. Adicionar
  um módulo futuro (Receitas, Metas, Cartões) é acrescentar uma entrada
  em `NAV_TREE` — nenhum dos componentes de shell precisa mudar.
- **Sidebar colapsada mostra um rail de ícones "achatado"** (todos os
  itens navegáveis folha, sem agrupamento visual, com `title`/
  `aria-label` para o rótulo) — não só os grupos de topo — para cumprir
  o requisito da spec de que a sidebar colapsada continua permitindo
  trocar de tela sem expandir. Usa o atributo HTML nativo `title` como
  tooltip (sem instalar um componente de tooltip só para isso — mesmo
  raciocínio de "sem dependência nova sem necessidade clara" das
  features anteriores).
- **Item "Despesas" não tem toggle de expandir/recolher** — com só 2
  filhos (Nova despesa, Listagem), a sidebar expandida sempre mostra os
  dois; não há necessidade de acordeão.
- **Itens desabilitados (Relatórios, Categorias) não são `<Link>`** —
  renderizados como `<span aria-disabled="true">` (ou `<button
  disabled>`, a definir no código), sem `href`/rota, sem `onClick`,
  garantindo que não há navegação por clique, toque ou Enter/Espaço via
  teclado, não só uma diferença visual.
- **`RegisterExpensePage`/`ExpensesListPage` perdem `<main>` e
  `<header>` próprios** — passam a ser só o conteúdo (formulário /
  filtros + lista), já que `AppShell` fornece a área de conteúdo
  (`<main>`) compartilhada. O botão "Sair" que existia duplicado nos
  dois sai de lá e vai para `SettingsPage` (única instância).

## Contratos técnicos

Caminhos relativos a `frontend/app/src/`.

### `components/nav/navConfig.ts`
```ts
import type { LucideIcon } from 'lucide-react'
import { BarChart3, Home, ListFilter, PlusCircle, Settings, Tag } from 'lucide-react'

export type NavItemStatus = 'active' | 'disabled' | 'placeholder'

export interface NavItem {
  id: string
  label: string
  icon: LucideIcon
  to?: string              // ausente em itens desabilitados (sem rota navegável)
  status: NavItemStatus
  mobilePrimary?: boolean  // true = um dos 4 slots principais do bottom nav
  children?: NavItem[]
}

export const NAV_TREE: NavItem[] = [
  { id: 'home', label: 'Início', icon: Home, to: '/', status: 'placeholder', mobilePrimary: true },
  {
    id: 'expenses',
    label: 'Despesas',
    icon: PlusCircle,
    status: 'active',
    children: [
      { id: 'expenses-new', label: 'Nova despesa', icon: PlusCircle, to: '/expenses/new', status: 'active', mobilePrimary: true },
      { id: 'expenses-list', label: 'Listagem / Filtros', icon: ListFilter, to: '/expenses', status: 'active', mobilePrimary: true },
    ],
  },
  { id: 'reports', label: 'Relatórios', icon: BarChart3, status: 'disabled' },
  { id: 'categories', label: 'Categorias', icon: Tag, status: 'disabled' },
  { id: 'settings', label: 'Configurações', icon: Settings, to: '/settings', status: 'placeholder', mobilePrimary: true },
]

// Achatado, só folhas navegáveis (usado pela sidebar colapsada e por
// helpers de "item ativo") — grupos sem `to` (ex.: "Despesas") não
// aparecem aqui, só seus filhos.
export function flattenNavItems(items: NavItem[] = NAV_TREE): NavItem[] {
  return items.flatMap((item) => (item.children ? flattenNavItems(item.children) : [item]))
}
```
Adicionar um módulo futuro = acrescentar um objeto em `NAV_TREE` (com
`status: 'disabled'` até implementar, depois `'active'` + `to` quando
a rota existir) — nenhum componente de shell precisa mudar.

### `components/nav/DesktopSidebar.tsx`
Visível só em telas largas (`hidden md:flex`, `md` = mesmo breakpoint
usado pelo `MobileBottomNav` para se esconder). Estado local
`const [collapsed, setCollapsed] = useState(false)`. Expandida: renderiza
`NAV_TREE` com rótulos e agrupamento (Despesas com seus 2 filhos
indentados); colapsada: renderiza `flattenNavItems(NAV_TREE)` como rail
de ícones com `title`. Item ativo via `useLocation()` comparado a
`item.to` (rota exata para folhas; `NavLink`'s matching não é usado
diretamente porque itens desabilitados não são links). Itens
desabilitados sempre não-clicáveis, independente do estado colapsado.

### `components/nav/MobileBottomNav.tsx`
Visível só em telas estreitas (`flex md:hidden`), fixo na base da tela.
Renderiza os 4 itens com `mobilePrimary: true` (via
`flattenNavItems(NAV_TREE).filter((i) => i.mobilePrimary)`) mais um 5º
botão "Mais", que abre `NavMoreSheet`.

### `components/nav/NavMoreSheet.tsx`
Conteúdo do `Sheet` (shadcn, `side="bottom"`) aberto pelo botão "Mais":
lista os itens de `NAV_TREE` que não são `mobilePrimary` (Relatórios,
Categorias hoje — desabilitados, mesma regra de não-clicável).

### `components/nav/AppShell.tsx`
```tsx
export function AppShell() {
  return (
    <div className="flex min-h-svh w-full">
      <DesktopSidebar />
      <main className="flex-1 overflow-y-auto pb-16 md:pb-0">
        <Outlet />
      </main>
      <MobileBottomNav />
    </div>
  )
}
```
`pb-16` no conteúdo evita que o bottom nav fixo sobreponha o final da
página em telas estreitas.

### `routes/HomePage.tsx`
```tsx
export function HomePage() {
  return (
    <div className="p-4">
      <h1 className="text-2xl font-semibold">Início</h1>
      <p className="text-muted-foreground">Em breve.</p>
    </div>
  )
}
```

### `routes/SettingsPage.tsx`
```tsx
export function SettingsPage() {
  const clearSession = useAuthStore((state) => state.clearSession)
  const navigate = useNavigate()

  function handleLogout() {
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">Configurações</h1>
      <Button variant="outline" onClick={handleLogout}>Sair</Button>
    </div>
  )
}
```
Mesma lógica de logout que hoje existe duplicada em
`RegisterExpensePage`/`ExpensesListPage` (`clearSession()` +
`navigate('/login', { replace: true })`), agora com uma única
instância.

### `routes/RegisterExpensePage.tsx` / `routes/ExpensesListPage.tsx` (ajuste)
Perdem `<main>`, `<header>`, `handleLogout`, o `Link` cruzado e os
imports de `useAuthStore`/`useNavigate`/`Link`/`Button` que só existiam
por causa disso. Ficam só com o conteúdo de negócio (`<ExpenseForm />`
/ `<ExpenseFilters />` + `<ExpenseList />`), dentro de um wrapper leve
próprio (`<div className="p-4">`) — `AppShell` já cuida do `<main>`
compartilhado.

### `app/router.tsx`
```tsx
export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/',
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <HomePage /> },
          { path: 'expenses/new', element: <RegisterExpensePage /> },
          { path: 'expenses', element: <ExpensesListPage /> },
          { path: 'settings', element: <SettingsPage /> },
        ],
      },
    ],
  },
])
```

## Novas dependências
- **shadcn `sheet`**: `npx shadcn add sheet` — usado só pelo conteúdo
  de "Mais" no mobile (drawer inferior). Único componente novo desta
  feature; a sidebar em si não usa nenhuma dependência nova (só
  `Button` e `lucide-react`, já existentes).

## Recursos AWS
**Nenhum recurso novo.** Feature exclusivamente de navegação/UI no
frontend, sem chamada HTTP nova.

## Mapeamento de erros
Não aplicável — esta feature não introduz chamadas de API. O
tratamento de sessão expirada (401) durante o uso de Despesas continua
inteiramente dentro de `useRegisterExpense`/`useExpensesQuery`
(FEAT-02/FEAT-03), inalterado; o shell não interfere nesse fluxo (a
navegação para `/login` em caso de sessão inválida continua vindo de
`ProtectedRoute` reagindo à `authStore`, como já documentado nos plans
anteriores).

## Testes (Vitest + Testing Library)
- `components/nav/navConfig.test.ts` — `flattenNavItems` achata
  corretamente a árvore (inclui filhos de "Despesas", não inclui o
  grupo "Despesas" em si por não ter `to`), filtro de `mobilePrimary`
  retorna exatamente os 4 itens esperados
- `components/nav/DesktopSidebar.test.tsx` (via `MemoryRouter`,
  `initialEntries` variando a rota atual) — renderiza a hierarquia
  completa; item da rota atual destacado; Relatórios/Categorias não
  respondem a clique (nenhuma navegação, sem `href`); colapsar oculta
  rótulos mas mantém todos os itens folha clicáveis (rail de ícones)
- `components/nav/MobileBottomNav.test.tsx` — renderiza os 4 itens
  principais + "Mais"; clicar em "Mais" abre o `Sheet` com
  Relatórios/Categorias, ambos não-clicáveis
- `routes/SettingsPage.test.tsx` — clicar em "Sair" chama
  `clearSession()` e navega para `/login`
- `components/nav/AppShell.test.tsx` — com rotas filhas de teste,
  navegar entre elas troca o conteúdo do `<Outlet />` mantendo o shell
  (sidebar/bottom nav) montado

Não há teste dedicado para `HomePage.tsx` (só texto estático) nem para
o `app/router.tsx` como integração ponta a ponta — o comportamento de
guarda de sessão já está coberto por `ProtectedRoute.test.tsx`
(FEAT-01), a composição de rotas por `AppShell.test.tsx`, e o conteúdo
de cada página pelos testes que já existem (`ExpenseForm.test.tsx`,
`ExpenseFilters.test.tsx`, `ExpenseList.test.tsx`).

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente — uso de sidebar própria (não o `sidebar` do shadcn) e
seleção dos 4 itens principais do bottom nav já confirmados pelo
usuário.
