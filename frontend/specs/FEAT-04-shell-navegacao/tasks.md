# Tasks — FEAT-04: Navegação e menu (shell da aplicação)

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a
`frontend/app/src/`.

- [x] 1. Instalar o componente `sheet` do shadcn/ui (`npx shadcn add sheet`), necessário pro conteúdo de "Mais" no mobile
- [x] 2. Implementar `components/nav/navConfig.ts` (`NavItem`, `NavItemStatus`, `NAV_TREE` com Início/Despesas (Nova despesa + Listagem)/Relatórios/Categorias/Configurações, `flattenNavItems`) e teste unitário `navConfig.test.ts` (achatamento inclui filhos e não inclui grupos sem `to`, filtro `mobilePrimary` retorna os 4 itens esperados)
- [x] 3. Criar `routes/HomePage.tsx` (placeholder de Início)
- [x] 4. Criar `routes/SettingsPage.tsx` (placeholder + botão "Sair" com `clearSession()` + `navigate('/login', { replace: true })`) e teste `SettingsPage.test.tsx` (clicar em "Sair" limpa a sessão e navega para `/login`)
- [x] 5. Implementar `components/nav/DesktopSidebar.tsx` (hierarquia expandida com Despesas + filhos, estado local de colapsar/expandir, rail de ícones achatado quando colapsada via `flattenNavItems`, item ativo destacado via `useLocation`, Relatórios/Categorias não-clicáveis) e teste `DesktopSidebar.test.tsx` (via `MemoryRouter`: hierarquia completa, destaque do item ativo por rota, itens desabilitados sem navegação ao clicar, colapsar mantém todos os itens folha acessíveis)
- [x] 6. Implementar `components/nav/NavMoreSheet.tsx` (lista os itens não-`mobilePrimary` de `NAV_TREE`, mesma regra de não-clicável para desabilitados)
- [x] 7. Implementar `components/nav/MobileBottomNav.tsx` (4 itens `mobilePrimary` + botão "Mais" abrindo `NavMoreSheet`) e teste `MobileBottomNav.test.tsx` (4 itens principais renderizados, "Mais" abre o sheet com Relatórios/Categorias não-clicáveis)
- [x] 8. Implementar `components/nav/AppShell.tsx` (`DesktopSidebar` + `<main><Outlet /></main>` + `MobileBottomNav`) e teste `AppShell.test.tsx` (navegar entre rotas filhas troca o conteúdo do `Outlet` mantendo o shell montado)
- [x] 9. Ajustar `routes/RegisterExpensePage.tsx` (remover `<main>`/`<header>`/`handleLogout`/link cruzado; manter só `<ExpenseForm />` num wrapper leve)
- [x] 10. Ajustar `routes/ExpensesListPage.tsx` (remover `<main>`/`<header>`/`handleLogout`/link cruzado; manter só `ExpenseFilters` + `ExpenseList` num wrapper leve)
- [x] 11. Atualizar `app/router.tsx` (rota de layout `AppShell` dentro de `ProtectedRoute`, com filhas `index → HomePage`, `expenses/new → RegisterExpensePage`, `expenses → ExpensesListPage`, `settings → SettingsPage`)
- [x] 12. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [x] 13. Validação manual: fluxo completo (sidebar/bottom nav, colapsar/expandir, "Mais", navegação entre todas as rotas, item ativo, itens desabilitados não-clicáveis, logout) validado pelo usuário com o backend real — confirmado funcionando
- [x] 14. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)