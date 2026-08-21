# Plano técnico — FEAT-15: Migração para o design system Modernist (Menu)

## Dependência: fundação Modernist da FEAT-14

**Isto precisa ser resolvido antes de rodar `/tasks`.** Esta feature
reaproveita a fundação vendorizada na FEAT-14 (`frontend/app/src/styles/
modernist/modernist.css`, escopo `.ds-modernist`, dependência
`@fontsource-variable/archivo`). Só que `FEAT-15-design-system-
modernist-menu` nasceu de `develop` (regra do fluxo), e o PR da FEAT-14
ainda não foi mergeado — então, hoje, **essa fundação não existe nesta
branch**.

Três caminhos possíveis:

1. **Esperar o merge do PR da FEAT-14 em `develop`** e só então rodar
   `/tasks`/implementar esta feature (mais simples, sem duplicação;
   exige pausar esta feature até lá)
2. **Rebasear `FEAT-15-design-system-modernist-menu` sobre a branch da
   FEAT-14** (`git rebase FEAT-14-design-system-modernist-login`) em vez
   de `develop`, herdando a fundação já pronta; ao final, os dois PRs
   (FEAT-14→develop, FEAT-15→develop depois de FEAT-14 mergear) seguem
   o fluxo normal
3. Recriar a fundação duplicada nesta branch agora (risco de divergência
   e conflito de merge depois)

**Recomendação:** opção 2 (rebase sobre a branch da FEAT-14) — permite
seguir implementando sem esperar review/merge, sem duplicar a fundação.
Este plano assume que a fundação da FEAT-14 (arquivo `modernist.css`,
classes `.btn`/`.field`/`.input`/`.seg`, escopo `.ds-modernist`) já está
disponível na branch no momento da implementação.

## Camadas afetadas

Só frontend, dentro de `frontend/app/src/`. Nenhuma camada do backend é
tocada.

| Arquivo | O que muda |
| --- | --- |
| `components/nav/navConfig.ts` | `NAV_TREE` passa a ter 5 itens de topo, todos folha (sem `children`), nenhum `status: 'disabled'`; grupo "Despesas" (2 filhos) vira 1 item `to: '/expenses'` |
| `components/nav/DesktopSidebar.tsx` | Reescrito com classes Modernist; remove o ramo de renderização de grupo (fica morto, já que não há mais `children`) |
| `components/nav/MobileBottomNav.tsx` | Reescrito com classes Modernist; lista de itens primários passa de 4 para 3 (Início, Despesas, Configurações) |
| `components/nav/NavItemRow.tsx` | Reescrito com classes Modernist (`.btn`-like row, ou marcação equivalente) no lugar das classes Tailwind atuais |
| `components/nav/NavMoreSheet.tsx` | Reescrito **sem** o `Sheet` do shadcn/ui — painel próprio usando `.dialog-backdrop`/`.dialog` do Modernist |
| `components/nav/AppShell.tsx` | Ajuste mínimo: o escopo `.ds-modernist` é aplicado só nos componentes de navegação (`nav`/painel), nunca no wrapper de `<Outlet />` |
| `styles/modernist/modernist.css` | Estende o arquivo vendorizado na FEAT-14 com as classes `.dialog-backdrop`/`.dialog` (ainda não portadas), escopadas sob `.ds-modernist` — mesma regra da FEAT-14: só o que é usado entra |
| `routes/ExpensesListPage.tsx` | Ganha um botão/link "+ Nova despesa" → `/expenses/new` (shadcn/ui, sem Modernist — a página não migra nesta feature) |
| `routes/` (novo) `ReportsComingSoonPage.tsx` | Página placeholder "Relatórios em breve", no mesmo estilo simples de `HomePage` (shadcn/Tailwind, não Modernist — conteúdo de página é fora de escopo) |
| `app/router.tsx` | Nova rota protegida `reports` dentro do `AppShell` |

Nenhuma página de conteúdo (`HomePage`, `CategoriesPage`,
`SettingsPage`, resto de `ExpensesListPage`) muda visualmente.

## Decisão técnica: onde aplicar o escopo `.ds-modernist`

Diferente da FEAT-14 (onde `.ds-modernist` envolve a página inteira de
Login), aqui o Modernist só cobre a **casca de navegação**, que
convive na mesma árvore DOM que o conteúdo de página (`AppShell`
renderiza sidebar + `<main><Outlet /></main>` + bottom-nav lado a lado,
não aninhados). Isso é favorável: basta **não** colocar a classe
`.ds-modernist` em nenhum ancestral comum a `<Outlet />`.

- `DesktopSidebar`: `.ds-modernist` no `<nav>` raiz do componente
- `MobileBottomNav`: `.ds-modernist` no `<nav>` raiz do componente
- `NavMoreSheet`: `.ds-modernist` no backdrop/`.dialog` do painel próprio
  (o painel é renderizado como filho de `MobileBottomNav`, não precisa
  de portal para fora da árvore — dispensa a complexidade de portal do
  `Sheet` do shadcn/ui, que não é necessária aqui)
- `AppShell`: **não** recebe `.ds-modernist` — continua só o layout
  flex (`sidebar + main + bottom-nav`) que já existe hoje

Isso garante, por construção (sem precisar de exclusões/overrides), que
nenhum seletor de `modernist.css` (`.ds-modernist h1`, `.ds-modernist a`,
`.ds-modernist :focus-visible` etc.) alcança o conteúdo das páginas.

## Decisão técnica: `NavMoreSheet` sem `Sheet` do shadcn/ui

O `Sheet` (Radix Dialog) traz cantos arredondados, animação e overlay
próprios do shadcn/ui — incompatível com a linguagem Modernist (raio
zero, regras de 2px, sem decoração). Como o Modernist já documenta um
padrão de overlay (`.dialog-backdrop` + `.dialog` +
`.dialog-title`/`.dialog-body`/`.dialog-actions`, ver
`frontend/design-system/_ds/.../styles.css`), o painel "Mais" é
recriado como um componente próprio:

```tsx
interface NavMoreSheetProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}
```
(mesma assinatura de hoje — troca só a implementação interna)

- Fecha ao clicar no backdrop, pressionar Esc, ou navegar (mesmo
  comportamento hoje garantido pelo `Sheet`) — reimplementado com um
  `onClick` no backdrop + um `useEffect` com listener de `keydown` para
  Esc (padrão simples, sem nova dependência)
- `role="dialog"` `aria-modal="true"` no `.dialog`, mantendo
  acessibilidade equivalente à do `Sheet` anterior
- As classes `.dialog-backdrop`/`.dialog` entram em `modernist.css`
  nesta feature (não estavam na FEAT-14, que só precisava de
  `.btn`/`.field`/`.input`/`.seg`)

## Decisão técnica: `navConfig.ts` — nova forma da árvore

```ts
export const NAV_TREE: NavItem[] = [
  { id: 'home', label: 'Início', icon: Home, to: '/', status: 'active', mobilePrimary: true },
  { id: 'expenses', label: 'Despesas', icon: ListFilter, to: '/expenses', status: 'active', mobilePrimary: true },
  { id: 'reports', label: 'Relatórios', icon: BarChart3, to: '/reports', status: 'active' },
  { id: 'categories', label: 'Categorias', icon: Tag, to: '/categories', status: 'active' },
  { id: 'settings', label: 'Configurações', icon: Settings, to: '/settings', status: 'active', mobilePrimary: true },
]
```

- `NavItem.status` deixa de ter nenhum item `'disabled'`; o valor
  `'placeholder'` (hoje usado em Início/Configurações, que já apontam
  para rotas reais com conteúdo mínimo) também deixa de fazer sentido
  como estado distinto agora que toda funcionalidade "não existente"
  vira uma rota real (mesmo que fake) — **decisão**: simplificar
  `NavItemStatus` para `'active' | 'placeholder'` neste ciclo, ou
  removê-lo de vez e usar só a presença de `to` para decidir
  clicável/não-clicável (já que todo item terá `to`)? Ver "Pontos a
  confirmar" abaixo — o plano assume, por ora, que o tipo `NavItemStatus`
  é mantido para não quebrar consumidores externos ao componente, mas
  simplificado
- `NavItem.children` continua existindo no tipo (não remover — é
  reaproveitável se o menu ganhar grupos no futuro), mas nenhum item o
  usa mais; `flattenNavItems` vira efetivamente identidade sobre
  `NAV_TREE` até que isso mude — mantido como utilitário, sem remover,
  para não forçar `MobileBottomNav` a mudar sua forma de obter a lista
  de primários

## Decisão técnica: `DesktopSidebar` — remoção do ramo de grupo

Hoje `DesktopSidebar` bifurca entre "item com filhos" (renderiza um
cabeçalho de grupo + lista indentada) e "item folha" (`NavItemRow`
direto). Como `NAV_TREE` não terá mais nenhum item com `children`, esse
ramo fica morto — é removido, simplificando o componente para sempre
mapear `NAV_TREE`/`flattenNavItems(NAV_TREE)` direto em `NavItemRow`,
com estilo Modernist:

- Ativo: `background: var(--color-neutral-200)` + `border-left: 2px
  solid var(--color-accent)` (igual ao design de referência)
- Inativo: `color: var(--color-neutral-700)`, hover com tingimento
  neutro
- Colapsar/expandir preserva a funcionalidade e o ícone (`PanelLeft`/
  `PanelLeftClose` do lucide-react, já dependência do projeto), agora
  estilizado com `.btn`/`.btn-ghost` do Modernist no lugar do `Button`
  shadcn/ui

## Decisão técnica: `MobileBottomNav` — itens primários

`mobilePrimary` passa a marcar exatamente 3 itens: Início, Despesas,
Configurações. "Mais" (`NavMoreSheet`) mostra os 2 restantes:
Relatórios, Categorias — mesma lógica de filtro já existente
(`!item.mobilePrimary` e, agora, sem checar `!item.children` porque
nenhum item tem filhos).

## Recursos AWS

**Nenhum.** Só frontend (React/CSS), sem novo endpoint, sem
infraestrutura.

## Mapeamento de erros

Não aplicável — nenhum componente desta feature chama API. A página
`ReportsComingSoonPage` é estática, sem estado de erro.

## Testes afetados

- `navConfig.test.ts`: reescrito para a nova forma da árvore — 5 itens
  folha, nenhum `disabled`, `mobilePrimary` = `['home', 'expenses',
  'settings']`, "Relatórios" ativo e navegável
- `DesktopSidebar.test.tsx`: ajustar para o novo markup; manter a
  cobertura de estado ativo por rota e do toggle colapsar/expandir
- `MobileBottomNav.test.tsx`: ajustar para 3 itens primários + botão
  "Mais"; manter cobertura de item ativo
- `NavMoreSheet.test.tsx` (novo): abre/fecha via `open`/`onOpenChange`,
  fecha ao pressionar Esc e ao clicar no backdrop, lista os itens
  esperados (Relatórios, Categorias)
- `AppShell.test.tsx`: ajustar se necessário; adicionar/skip conforme
  garantir que o conteúdo de `<Outlet />` não recebe a classe
  `.ds-modernist`
- Novo `ReportsComingSoonPage.test.tsx`: renderiza o texto de
  placeholder
- `ExpensesListPage.test.tsx`: novo caso cobrindo o botão/link
  "+ Nova despesa" navegando para `/expenses/new`
- `router.tsx`: se existir teste de rotas, incluir a nova rota
  `reports`

## Resumo das decisões

1. Esta feature depende da fundação Modernist da FEAT-14; recomendado
   rebasear a branch sobre `FEAT-14-design-system-modernist-login` em
   vez de esperar o merge em `develop`
2. `.ds-modernist` fica contido nos três componentes de navegação
   (`DesktopSidebar`, `MobileBottomNav`, `NavMoreSheet`), nunca no
   wrapper de `<Outlet />` — nenhuma página de conteúdo herda o reset/
   tipografia do Modernist
3. `NavMoreSheet` troca o `Sheet` do shadcn/ui por um painel próprio
   usando `.dialog-backdrop`/`.dialog` do Modernist (classes novas,
   adicionadas ao `modernist.css` vendorizado)
4. `navConfig.ts`: "Despesas" vira item único (`/expenses`); nenhum
   item fica `disabled` — "Relatórios" ganha rota real
   (`ReportsComingSoonPage`, dentro do `AppShell`)
5. `DesktopSidebar` perde o ramo de renderização de grupo (código morto
   após a mudança acima)
6. `ExpensesListPage` ganha um botão "+ Nova despesa" (shadcn/ui, sem
   Modernist) para preservar o acesso a `/expenses/new`

## Pontos a confirmar antes do `/tasks`

- **Ordem de execução**: confirmar se rebaseia esta branch sobre a
  branch da FEAT-14 (recomendado) ou aguarda o merge em `develop` antes
  de implementar
- Confirmar se `NavItemStatus` deve manter os 3 valores
  (`'active' | 'disabled' | 'placeholder'`) só sem nenhum item usando
  `'disabled'` hoje, ou se simplifica o tipo para `'active' |
  'placeholder'` já que "funcionalidade inexistente" agora é resolvida
  com uma rota fake, não com um estado desabilitado — proposta do plano:
  simplificar, mas é um detalhe de tipagem sem impacto visível, pode ser
  decidido livremente na implementação
- Texto exato de `ReportsComingSoonPage` (proposta: título "Relatórios
  em breve", corpo curto no mesmo tom de `HomePage`/`SignupComingSoonPage`)
