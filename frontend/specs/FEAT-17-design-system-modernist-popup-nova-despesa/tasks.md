# Tasks — FEAT-17: Migração para o design system Modernist (Popup de Nova Despesa)

- [x] 1. Adicionar `refetch(): void` a `features/expenses/hooks/useExpensesQuery.ts`, reexecutando `fetchPage(filters, null, false)` com os filtros já guardados no estado
- [x] 2. Adicionar teste de `refetch()` em `features/expenses/hooks/useExpensesQuery.test.ts` (reexecuta a busca com os filtros atuais, primeira página)
- [x] 3. ~~Reescrever `ExpenseFormFields.tsx`~~ — revertido: é compartilhado com `EditExpenseForm` (fora do escopo); `ExpenseForm` passa a ter campos Modernist próprios, inline, em vez de reescrever o componente compartilhado
- [x] 4. Reescrever `features/expenses/components/ExpenseForm.tsx`: tokens/classes do Modernist com campos próprios (`.field`/`.input`, `<select class="input">` para Categoria), ganha props opcionais `onSuccess`/`onCancel`; remove o alerta "Despesa registrada" — sucesso chama `onSuccess?.()` (mantendo `reset()`); botão "Cancelar" (`.btn.btn-secondary`) renderizado só quando `onCancel` é passado
- [x] 5. Atualizar `features/expenses/components/ExpenseForm.test.tsx`: teste de sucesso passa a verificar que `onSuccess` é chamado (spy) em vez do alerta "Despesa registrada" com o formulário aberto; demais casos (sem categoria, validação, erro 400) ajustados ao novo markup
- [x] 6. Criar `features/expenses/components/NewExpenseDialog.tsx`: popup `.dialog-backdrop`/`.dialog` (`role="dialog"` `aria-modal="true"`), props `open`/`onOpenChange`/`onCreated`, fecha em Esc/backdrop/"Cancelar", renderiza `ExpenseForm` com `onSuccess` chamando `onCreated()` + `onOpenChange(false)`
- [x] 7. Criar `features/expenses/components/NewExpenseDialog.test.tsx`: abre/fecha via `open`/`onOpenChange`; fecha ao pressionar Esc; fecha ao clicar no backdrop; "Cancelar" fecha sem chamar a API; cadastro com sucesso chama `onCreated` e fecha o popup
- [x] 8. Atualizar `routes/ExpensesListPage.tsx`: botão "+ Nova despesa" (`<button>` em vez de `<Link>`) abre `NewExpenseDialog` (estado local `isAddOpen`), passando `query.refetch` como `onCreated`
- [x] 9. Atualizar `routes/ExpensesListPage.test.tsx`: substituir o teste do link `/expenses/new` por um teste que clica no botão "+ Nova despesa" e verifica que o popup abre (`role="dialog"` com os campos do formulário)
- [x] 10. Remover a rota `expenses/new` e o import de `RegisterExpensePage` em `app/router.tsx`
- [x] 11. Deletar `routes/RegisterExpensePage.tsx`
- [x] 12. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e `npm run build`; garantir 100% dos testes passando
- [x] 13. Revisão manual: conferir que nenhuma página fora do escopo (edição/detalhe de despesa, categorias, ajustes, início, menu) mudou de aparência — via checklist estático (já que não há navegador neste ambiente) e/ou revisão visual do usuário
- [x] 14. Atualizar `spec.md` marcando todos os critérios de aceite concluídos (`- [x]`)
