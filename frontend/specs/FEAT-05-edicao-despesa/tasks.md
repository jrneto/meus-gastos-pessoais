# Tasks — FEAT-05: Edição de despesa

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a
`frontend/app/src/`.

- [x] 1. Acrescentar o método `put` em `lib/httpClient.ts` (mesmo padrão de `post`)
- [x] 2. Acrescentar `centsToAmountInput` em `features/expenses/utils/currency.ts` e teste unitário em `currency.test.ts` (ex.: `4590` → `"45,90"`, `123456` → `"1.234,56"`)
- [x] 3. Acrescentar `NotFoundError` e `UpdateValidationError` em `features/expenses/errors/expenseErrors.ts`
- [x] 4. Acrescentar `getExpenseById(token, id)` e `updateExpense(token, id, payload)` em `features/expenses/api/expensesApi.ts` (`assertDetailOk`/`assertUpdateOk` mapeando 404/401/400/outros para `NotFoundError`/`SessionExpiredError`/`UpdateValidationError`/`UnknownExpenseError`, reaproveitando `safeFetch`)
- [x] 5. Implementar `features/expenses/hooks/useExpense.ts` (carrega despesa por `id`, cancela `setState` em unmount, `clearSession()` em `SessionExpiredError`) e teste `useExpense.test.ts` (sucesso, 404, 401 com verificação de `clearSession`, erro de rede — via MSW)
- [x] 6. Implementar `features/expenses/hooks/useUpdateExpense.ts` (mesmo formato de `useRegisterExpense`, parametrizado por `id`) e teste `useUpdateExpense.test.ts` (sucesso, 400, 404, 401 com verificação de `clearSession` — via MSW)
- [x] 7. Extrair `features/expenses/components/ExpenseFormFields.tsx` (os 4 campos de `ExpenseForm.tsx`, como componente apresentacional recebendo `register`/`control`/`errors`) e refatorar `ExpenseForm.tsx` para usá-lo, sem alterar comportamento/DOM (confirmar que `ExpenseForm.test.tsx` continua passando sem modificação)
- [x] 8. Implementar `features/expenses/components/ExpenseNotFound.tsx` (mensagem + link "Voltar à listagem")
- [x] 9. Implementar `features/expenses/components/EditExpenseForm.tsx` (`useForm` com `defaultValues` a partir de `expense` via `centsToAmountInput`, `useUpdateExpense`, `useEffect` navega para `/expenses` em `success`, `ExpenseNotFound` em `NotFoundError`, alerta genérico para os demais erros, botões "Salvar"/"Cancelar") e teste `EditExpenseForm.test.tsx` (pré-preenchimento, validação inline, sucesso navega para `/expenses`, 400 mantém dados com alerta, 404 troca para `ExpenseNotFound`, "Cancelar" navega sem chamar a API — via MSW)
- [x] 10. Criar `routes/EditExpensePage.tsx` (lê `id` via `useParams`, `useExpense`, estados de carregamento/erro/`ExpenseNotFound`/formulário) e teste `EditExpensePage.test.tsx` (carregamento, 404 renderiza `ExpenseNotFound`, sucesso renderiza `EditExpenseForm` pré-preenchido — via MSW)
- [x] 11. Ajustar `features/expenses/components/ExpenseList.tsx` (acrescentar link de editar com ícone `Pencil` por item, apontando para `/expenses/{id}/edit`) e estender `ExpenseList.test.tsx` (link de editar presente com `href` correto por item)
- [x] 12. Atualizar `app/router.tsx` (nova rota filha `path: 'expenses/:id/edit'` renderizando `EditExpensePage`, dentro de `AppShell`)
- [x] 13. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [x] 14. Validação manual: fluxo completo (editar a partir da listagem, formulário pré-preenchido, salvar, cancelar, validação inline, despesa inexistente) validado pelo usuário com o backend real — confirmado funcionando
- [x] 15. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)
