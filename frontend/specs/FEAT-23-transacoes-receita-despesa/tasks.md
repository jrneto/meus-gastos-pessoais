# Tasks — FEAT-23: Transações — generalizar despesa para receita/despesa

- [x] 1. Mover/renomear toda a pasta `features/expenses/` para
      `features/transactions/`, renomeando cada arquivo interno (e seu
      `.test.*`) para o nome novo listado no `plan.md`
      (`expensesApi.ts`→`transactionsApi.ts`,
      `expenseErrors.ts`→`transactionErrors.ts`,
      `expenseSchema.ts`→`transactionSchema.ts`,
      `expenseFilterSchema.ts`→`transactionFilterSchema.ts`,
      `ExpenseForm(.test).tsx`→`TransactionForm(.test).tsx`,
      `ExpenseFormDialog(.test).tsx`→`TransactionFormDialog(.test).tsx`,
      `ExpenseList(.test).tsx`→`TransactionList(.test).tsx`,
      `ExpenseFilters(.test).tsx`→`TransactionFilters(.test).tsx`,
      `ExpenseDetailDialog(.test).tsx`→`TransactionDetailDialog(.test).tsx`,
      `ExpenseDeleteDialog(.test).tsx`→`TransactionDeleteDialog(.test).tsx`,
      `useExpense(.test).ts`→`useTransaction(.test).ts`,
      `useExpensesQuery(.test).ts`→`useTransactionsQuery(.test).ts`,
      `useRegisterExpense(.test).ts`→`useRegisterTransaction(.test).ts`,
      `useUpdateExpense(.test).ts`→`useUpdateTransaction(.test).ts`,
      `useDeleteExpense(.test).ts`→`useDeleteTransaction(.test).ts`) e
      ajustar os imports relativos entre eles — puro rename mecânico
      (arquivo/pasta), sem mudar endpoint, campos ou nomes de
      classe/tipo ainda; rodar a suíte da pasta renomeada e confirmar
      100% passando antes de seguir
- [x] 2. Atualizar `features/transactions/api/transactionsApi.ts`:
      endpoint `/expenses`→`/transactions` em todas as funções; renomear
      tipos/interfaces (`ExpenseQueryItem`→`TransactionQueryItem`,
      `ExpenseDetail`→`TransactionDetail`,
      `GetExpensesParams`→`GetTransactionsParams`,
      `GetExpensesResponse`→`GetTransactionsResponse`, payloads de
      registro/atualização); `expenseDate`→`date`; adicionar `tipo`,
      `createdByUserId`, `createdByLabel` aos tipos de leitura e `tipo`
      obrigatório aos payloads de escrita; `GetTransactionsParams` ganha
      `tipo?: 'despesa' | 'receita'` opcional (sem uso por UI ainda)
- [x] 3. Atualizar `features/transactions/errors/transactionErrors.ts`:
      renomear `UnknownExpenseError`→`UnknownTransactionError` e
      `UnknownExpenseQueryError`→`UnknownTransactionQueryError`;
      mensagem de `NotFoundError` generalizada para "Transação não
      encontrada."; demais classes mantidas como estão
- [x] 4. Atualizar `features/transactions/schemas/transactionSchema.ts`
      e `transactionSchema.test.ts`: campo `expenseDate`→`date`
      (mesmas regras de validação, resto do schema inalterado)
- [x] 5. Atualizar
      `features/transactions/schemas/transactionFilterSchema.ts` e seu
      teste: só ajustar imports/nomes — sem mudança de campo (`tipo`
      não é exposto no filtro nesta feature)
- [x] 6. Atualizar `features/transactions/hooks/useRegisterTransaction.ts`
      e seu teste: importar de `transactionsApi`/`transactionErrors`;
      montar o payload com `tipo: 'despesa'` fixo e `date` no lugar de
      `expenseDate`
- [x] 7. Atualizar `features/transactions/hooks/useUpdateTransaction.ts`
      e seu teste: mesma mudança de payload (`tipo: 'despesa'` fixo,
      `date`) e imports
- [x] 8. Atualizar `features/transactions/hooks/useTransactionsQuery.ts`,
      `useTransaction.ts` e `useDeleteTransaction.ts` (e seus testes):
      importar de `transactionsApi`/`transactionErrors`; atualizar
      mocks MSW dos testes para `http://localhost:5049/transactions` e
      os novos campos de resposta (`tipo`/`date`/`createdByUserId`/
      `createdByLabel`) — sem mudança de lógica nestes três hooks
- [x] 9. Atualizar `features/transactions/components/TransactionForm.tsx`
      e seu teste: importar `transactionSchema`/`useRegisterTransaction`/
      `useUpdateTransaction`; campo de data usa `date`; dropdown de
      categoria filtra `categories.filter(c => c.tipo === 'despesa')`
      (`expenseCategories`); estado "nenhuma categoria cadastrada" passa
      a checar `expenseCategories.length === 0`; testes cobrindo:
      dropdown não lista categoria de receita, e estado vazio quando só
      existem categorias de receita
- [x] 10. Atualizar
      `features/transactions/components/TransactionFormDialog.tsx` e
      seu teste: importar `TransactionForm`/`useTransaction`;
      `initialValues` usa `date` no lugar de `expenseDate`; título
      continua fixo "Nova despesa"/"Editar despesa"
- [x] 11. Atualizar `features/transactions/components/TransactionList.tsx`
      e seu teste: coluna de data usa `item.date`; célula de valor ganha
      sinal (`- `/`+ `) e cor (`var(--color-accent-700)`/
      `var(--color-positive-700)`) a partir de `item.tipo`; teste novo
      cobrindo uma receita na lista (sinal `+`, cor positive) ao lado de
      uma despesa (sinal `-`, cor accent)
- [x] 12. Atualizar
      `features/transactions/components/TransactionFilters.tsx` e seu
      teste: só ajustar imports/nomes — sem mudança de campo
- [x] 13. Atualizar
      `features/transactions/components/TransactionDetailDialog.tsx` e
      seu teste: usar `transaction.date`; adicionar bloco "Lançado por"
      exibindo `transaction.createdByLabel`; título do dialog e cor do
      valor continuam fixos como despesa (sem checar `tipo`); testes
      novos cobrindo "Lançado por" com "Você" e com e-mail de outro
      membro
- [x] 14. Atualizar
      `features/transactions/components/TransactionDeleteDialog.tsx` e
      seu teste: só ajustar imports/nomes
- [x] 15. Renomear `routes/ExpensesListPage.tsx` para
      `routes/TransactionsListPage.tsx` (e seu teste): importar dos
      componentes/hooks renomeados; manter só o botão "+ Nova despesa"
      (sem "+ Nova receita" nesta feature, ver `plan.md`)
- [x] 16. Atualizar `app/router.tsx`: path `expenses`→`transactions`,
      import de `TransactionsListPage` no lugar de `ExpensesListPage`
- [x] 17. Atualizar `components/nav/navConfig.ts` e
      `navConfig.test.ts`: `id: 'expenses'`→`'transactions'`,
      `to: '/expenses'`→`'/transactions'` (label "Transações" já
      correto, sem mudança)
- [x] 18. Atualizar `components/nav/DesktopSidebar.test.tsx`,
      `components/nav/MobileBottomNav.test.tsx` e
      `components/nav/AppShell.test.tsx`: trocar as referências de rota
      `/expenses` por `/transactions`
- [x] 19. Atualizar os comentários em
      `features/categories/components/CategoryDeleteDialog.tsx` e
      `lib/categories/CategoryLetterTile.tsx` que citam
      `ExpenseDeleteDialog`/`ExpenseDetailDialog`, apontando para
      `TransactionDeleteDialog`/`TransactionDetailDialog`
- [x] 20. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e
      `npm run build`; confirmar 100% dos testes passando, sem erro de
      tipo e sem warning novo de lint
- [ ] 21. Revisão manual/visual: conferir a tela de Transações (criar
      despesa, dropdown de categoria só com despesa, editar, excluir,
      abrir detalhe mostrando "Lançado por: Você") contra
      `frontend/design-system/web/jrnexpenses-web.dc.html` (tela
      "Transações") — se possível, seedar também uma receita via API
      diretamente para conferir sinal/cor `+`/verde na listagem
- [ ] 22. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
