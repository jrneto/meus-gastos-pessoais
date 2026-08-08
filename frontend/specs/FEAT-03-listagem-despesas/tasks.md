# Tasks — FEAT-03: Listagem de despesas com filtros

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a
`frontend/app/src/`.

- [x] 1. Acrescentar `formatCentsToCurrency` em `features/expenses/utils/currency.ts` e teste unitário em `currency.test.ts` (ex.: `4590` → `"R$ 45,90"`)
- [x] 2. Acrescentar `InvalidFilterError` e `UnknownExpenseQueryError` em `features/expenses/errors/expenseErrors.ts`
- [x] 3. Implementar `features/expenses/schemas/expenseFilterSchema.ts` (todos os campos opcionais, transform de `minAmount`/`maxAmount` via `parseCurrencyToCents`, `refine` de `dateFrom <= dateTo` e `minAmountInCents <= maxAmountInCents`) e teste unitário `expenseFilterSchema.test.ts`
- [x] 4. Acrescentar `getExpenses(token, params)` em `features/expenses/api/expensesApi.ts` (query string a partir dos filtros, `assertQueryOk` mapeando 400/401/outros para `InvalidFilterError`/`SessionExpiredError`/`UnknownExpenseQueryError`, reaproveitando `safeFetch`)
- [x] 5. Implementar `features/expenses/hooks/useExpensesQuery.ts` (carga inicial sem filtro no mount, `applyFilters` reiniciando `items`/`cursor`, `loadMore` anexando página via `nextCursor`, `clearSession()` em `SessionExpiredError`) e teste `useExpensesQuery.test.ts` (carga inicial, `applyFilters`, `loadMore`, 400, 401 com verificação de `clearSession`, lista vazia — via MSW)
- [x] 6. Implementar `features/expenses/components/ExpenseFilters.tsx` (RHF + `zodResolver(expenseFilterSchema)`; campos `yearMonth` (`input type="month"`), `category` (`Select` com opção "Todas" + `EXPENSE_CATEGORIES`), `dateFrom`/`dateTo` (`input type="date"`), `minAmount`/`maxAmount` (texto); `onApply(data)` via prop) e teste de componente `ExpenseFilters.test.tsx` (validação inline dos dois `refine`, submit chama `onApply` com dados transformados)
- [x] 7. Implementar `features/expenses/components/ExpenseList.tsx` (lista semântica dos itens formatados, estado vazio, alerta de erro, botão "Carregar mais" condicionado a `hasMore`/`isLoadingMore`, tudo via props) e teste de componente `ExpenseList.test.tsx` (renderização dos itens, estado vazio, botão some sem `hasMore`, alerta de erro)
- [x] 8. Criar `routes/ExpensesListPage.tsx` (header próprio com logout + link para `/`, compõe `ExpenseFilters` e `ExpenseList` via `useExpensesQuery`)
- [x] 9. Ajustar `routes/RegisterExpensePage.tsx` (acrescentar link "Ver despesas" para `/expenses` ao lado do botão "Sair", sem outra mudança)
- [x] 10. Atualizar `app/router.tsx` (nova rota filha `path: 'expenses'` renderizando `ExpensesListPage`, dentro da `ProtectedRoute` já existente)
- [x] 11. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [x] 12. Validação manual end-to-end: `tsc -b`, `vite build` e dev server confirmados sem erro no ambiente de desenvolvimento; fluxo completo (login → "Ver despesas" → filtros → paginação → navegação de volta) validado pelo usuário com o backend real — confirmado funcionando
- [x] 13. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)
