# Tasks — FEAT-06: Exclusão de despesa

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a
`frontend/app/src/`.

- [x] 1. Instalar o componente `alert-dialog` do shadcn/ui (`npx shadcn add alert-dialog`), necessário pro popup de confirmação
- [x] 2. Acrescentar o método `delete` em `lib/httpClient.ts` (mesmo padrão de `get`, sem corpo)
- [x] 3. Acrescentar `deleteExpense(token, id)` em `features/expenses/api/expensesApi.ts` (`assertDeleteOk` mapeando 404/401/outros para `NotFoundError`/`SessionExpiredError`/`UnknownExpenseError`, já existentes; reaproveitando `safeFetch`)
- [x] 4. Implementar `features/expenses/hooks/useDeleteExpense.ts` (`deleteExpense(id)`, mesmo formato de `useUpdateExpense` sem `id` fixo, `clearSession()` em `SessionExpiredError`) e teste `useDeleteExpense.test.ts` (sucesso, 404, 401 com verificação de `clearSession`, erro de rede — via MSW)
- [x] 5. Acrescentar `removeItem(id)` em `features/expenses/hooks/useExpensesQuery.ts` (filtra `items` localmente, sem chamar API) e teste em `useExpensesQuery.test.ts` (remove só o item do `id` informado, mantém os demais)
- [x] 6. Implementar `features/expenses/components/ExpenseDeleteDialog.tsx` (`AlertDialog` controlado por `expense`, descrição da despesa, `useDeleteExpense`, `onDeleted` em sucesso e em `NotFoundError`, `Alert` inline + dialog permanece aberto nos demais erros, botão de ação `variant="destructive"` desabilitado durante `isLoading`) e teste `ExpenseDeleteDialog.test.tsx` (fechado sem `expense`, aberto exibe descrição, cancelar não chama a API, sucesso chama `onDeleted`, 404 chama `onDeleted` com mensagem própria, 5xx mantém aberto com alerta sem chamar `onDeleted` — via MSW)
- [x] 7. Ajustar `features/expenses/components/ExpenseList.tsx` (prop `onDeleted`, estado local `deleteTarget`, botão ícone `Trash2` por item ao lado do de editar, `ExpenseDeleteDialog` com `key={deleteTarget?.id ?? 'closed'}`) e estender `ExpenseList.test.tsx` (botão de excluir abre o dialog com a descrição correta; confirmar via MSW remove o item da lista renderizada e chama `onDeleted` com o `id` certo)
- [x] 8. Ajustar `routes/ExpensesListPage.tsx` (passar `onDeleted={query.removeItem}` para `ExpenseList`)
- [x] 9. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [x] 10. Validação manual: fluxo completo (excluir, cancelar, confirmar, despesa já removida) validado pelo usuário com o backend real — confirmado funcionando. Corrigido warning do Base UI sobre `role`/semântica de botão em três `Button`+`Link` (edit em `ExpenseList`, `ExpenseNotFound`, `Cancelar` em `EditExpenseForm`), trocados por `<Link>` estilizado com `buttonVariants()`
- [x] 11. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)
