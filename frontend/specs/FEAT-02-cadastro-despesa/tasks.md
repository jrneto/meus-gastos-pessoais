# Tasks — FEAT-02: Cadastro de despesas

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a
`frontend/app/src/`.

- [x] 1. Instalar o componente `select` do shadcn/ui (`npx shadcn add select`), necessário pro campo de categoria
- [x] 2. Criar `features/expenses/constants/expenseCategories.ts` (enum de categorias do backend + label em pt-BR)
- [x] 3. Implementar `features/expenses/utils/currency.ts` (`parseCurrencyToCents`) e teste unitário `currency.test.ts` (formatos válidos, com/sem milhar, valores inválidos)
- [x] 4. Implementar `features/expenses/schemas/expenseSchema.ts` (Zod: descrição, valor com regex + transform, categoria, data) e teste unitário `expenseSchema.test.ts`
- [x] 5. Implementar `features/expenses/errors/expenseErrors.ts` (`ValidationError`, `SessionExpiredError`, `NetworkError`, `UnknownExpenseError`)
- [x] 6. Implementar `features/expenses/api/expensesApi.ts` (`registerExpense(token, payload)` via `httpClient`, mapeando status HTTP para os erros tipados)
- [x] 7. Implementar `features/expenses/hooks/useRegisterExpense.ts` (lê token da `authStore`, gerencia `isLoading`/`error`/`success`, chama `authStore.clearSession()` em `SessionExpiredError`) e teste `useRegisterExpense.test.ts` (sucesso, 400, 401 com verificação de `clearSession`, erro de rede — via MSW)
- [x] 8. Implementar `features/expenses/components/ExpenseForm.tsx` (RHF + `zodResolver(expenseSchema)`, campos descrição/valor/categoria (`Select`)/data, alertas de erro e de confirmação, `reset()` reativo ao `success`)
- [x] 9. Criar `routes/RegisterExpensePage.tsx` (cabeçalho com ação de logout + `ExpenseForm`) e remover `routes/HomePage.tsx`
- [x] 10. Atualizar `app/router.tsx` (rota índice passa a renderizar `RegisterExpensePage` em vez de `HomePage`)
- [x] 11. Escrever teste de componente `features/expenses/components/ExpenseForm.test.tsx` (validação inline, submit com sucesso limpa o formulário e mostra confirmação, erro 400 mantém os dados preenchidos — via MSW mockando `POST /expenses`)
- [x] 12. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [x] 13. Validação manual end-to-end: login → tela de cadastro de despesas, cadastrar despesa com sucesso (confirmação + formulário limpo), erro de validação inline, e logout a partir da nova página
- [x] 14. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)