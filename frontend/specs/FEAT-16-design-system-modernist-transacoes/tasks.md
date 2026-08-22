# Tasks — FEAT-16: Migração para o design system Modernist (Transações)

- [x] 1. Completar tokens em `frontend/app/src/styles/modernist/modernist.css` (`--color-neutral-100/300/400/600/800`, `--color-accent-800`) portando os valores da folha de referência (`frontend/design-system/_ds/modernist-a01587a5-394c-4dcb-a692-c51267a2ceac/styles.css`)
- [x] 2. Adicionar `.tag`/`.tag-neutral`/`.tag-accent` a `modernist.css`, escopados sob `.ds-modernist`
- [x] 3. Adicionar `.table`/`.table th`/`.table td`/`.table tbody tr:hover` a `modernist.css`, escopados sob `.ds-modernist`
- [x] 4. Atualizar `components/nav/navConfig.ts`: `label` do item `id: 'expenses'` de `'Despesas'` para `'Transações'` (demais campos inalterados)
- [x] 5. Atualizar `components/nav/navConfig.test.ts` para a nova asserção de rótulo (`'Transações'`)
- [x] 6. Ajustar `components/nav/DesktopSidebar.test.tsx` e `components/nav/MobileBottomNav.test.tsx` caso algum teste assira o texto literal "Despesas"
- [x] 7. Reescrever `features/expenses/components/ExpenseDeleteDialog.tsx`: `.dialog-backdrop`/`.dialog`/`.dialog-title`/`.dialog-actions` do Modernist no lugar do `AlertDialog` do shadcn/ui, `role="alertdialog"` `aria-modal="true"`, fecha em Esc/clique no backdrop/"Cancelar", preservando os dois `useEffect` (`success`→`onDeleted`, `NotFoundError`→`onDeleted` silencioso) e o estado de carregamento/erro
- [x] 8. Atualizar `features/expenses/components/ExpenseDeleteDialog.test.tsx` para o novo markup, mantendo os casos existentes (sucesso, `NotFoundError`, outro erro, cancelar, fechar por Esc/backdrop)
- [x] 9. Reescrever `features/expenses/components/ExpenseFilters.tsx`: linha de chips de categoria (`.tag-neutral`/`.tag-accent`, via `useCategories()`) que aplica a busca imediatamente ao clicar (seleciona/desseleciona `categoryId`), sem importar `CategoryBadge`
- [x] 10. Adicionar o painel "Filtros avançados" colapsável em `ExpenseFilters.tsx` (estado local `advancedOpen`), contendo `yearMonth`/`dateFrom`/`dateTo`/`minAmount`/`maxAmount` com `.field`/`.input` do Modernist, botão "Filtrar" (`.btn.btn-primary`) e indicador visual (`•` em `var(--color-accent)`) quando algum desses campos tem valor, mesma validação/mensagens do `expenseFilterSchema`
- [x] 11. Atualizar `features/expenses/components/ExpenseFilters.test.tsx`: clique em chip aplica/limpa filtro de categoria; abrir/fechar painel avançado; validações existentes (data inicial após final, valor mín. maior que máx.) continuam cobertas
- [x] 12. Reescrever `features/expenses/components/ExpenseList.tsx` como `<table class="table">` (colunas Categoria/Descrição/Data/Valor), linha navegável para `/expenses/:id` via `useNavigate()`, ações de editar/excluir por linha com `stopPropagation()`, tag de categoria renderizada localmente (sem `CategoryBadge`), estado vazio e de erro recriados no Modernist, botão "Carregar mais" (`.btn.btn-secondary`) preservando `hasMore`/`isLoadingMore`
- [x] 13. Atualizar `features/expenses/components/ExpenseList.test.tsx` para a marcação de tabela, mantendo cobertura de navegação por linha, editar, excluir, estado vazio/erro e paginação
- [x] 14. Reescrever `routes/ExpensesListPage.tsx`: `.ds-modernist` no wrapper raiz, título "Transações", compõe `ExpenseFilters` + `ExpenseList`, mantém o link "+ Nova despesa" (`.btn.btn-primary`)
- [x] 15. Atualizar `routes/ExpensesListPage.test.tsx` para o título "Transações" e a nova composição, mantendo o caso do link "+ Nova despesa"
- [x] 16. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e `npm run build`; garantir 100% dos testes passando
- [x] 17. Revisão manual: conferir que nenhuma página fora do escopo (cadastro/edição/detalhe de despesa, categorias, ajustes, início) mudou de aparência — via checklist estático (já que não há navegador neste ambiente) e/ou revisão visual do usuário
- [x] 18. Atualizar `spec.md` marcando todos os critérios de aceite concluídos (`- [x]`)
