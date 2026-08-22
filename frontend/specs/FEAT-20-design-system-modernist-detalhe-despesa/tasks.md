# Tasks — FEAT-20: Detalhe da Despesa e Acertos Finos de Transações

- [x] 1. Criar `lib/categories/CategoryLetterTile.tsx` (tile 24×24, borda `--color-divider`, inicial do nome em maiúscula, sem cor) extraindo o markup hoje inline em `CategoryList`
- [x] 2. Adicionar `lib/categories/CategoryLetterTile.test.tsx`: renderiza a inicial do nome em maiúscula
- [x] 3. Atualizar `features/categories/components/CategoryList.tsx` para usar `CategoryLetterTile` no lugar do markup inline equivalente (sem mudança de comportamento/teste esperada)
- [x] 4. Atualizar `features/expenses/components/ExpenseList.tsx`: remove `style={{ color: category.cor }}` do tag de categoria; remove a coluna "Ações" e os ícones de editar/excluir; remove `ExpenseDeleteDialog`/estado `deleteTarget` internos; prop nova `onRowClick: (item) => void` no `<tr onClick>`
- [x] 5. Atualizar `features/expenses/components/ExpenseList.test.tsx`: remove os casos de link/botão de editar/excluir por linha; adiciona teste de `onRowClick` chamado ao clicar na linha; confirma que a categoria não tem `style` de cor
- [x] 6. Criar `features/expenses/components/ExpenseDetailDialog.tsx`: popup `.dialog-backdrop`/`.dialog` (`role="dialog"`) mostrando valor/data/categoria (`CategoryLetterTile` + nome)/descrição, sem chamada à API; botões Excluir (`btn-ghost`)/Editar (`btn-secondary`)/Fechar (`btn-primary`), nessa ordem; fecha em Esc/backdrop/"Fechar"
- [x] 7. Criar `features/expenses/components/ExpenseDetailDialog.test.tsx`: renderiza os dados do item; "Editar" chama `onEdit` e fecha; "Excluir" chama `onDelete` e fecha; "Fechar"/Esc/backdrop só fecham, sem chamar a API
- [x] 8. Atualizar `routes/ExpensesListPage.tsx`: novos estados `detailTarget`/`deleteTarget` (além do `dialogTarget`/`formTarget` de criar/editar); `ExpenseList` recebe `onRowClick={setDetailTarget}`; `ExpenseDetailDialog` orquestra `handleEditFromDetail`/`handleDeleteFromDetail`; `ExpenseDeleteDialog` (que saiu de `ExpenseList`) passa a ser renderizado aqui; aplica `maxWidth:'920px', margin:'0 auto', padding:'40px 40px 60px', boxSizing:'border-box'` ao wrapper raiz
- [x] 9. Atualizar `routes/ExpensesListPage.test.tsx`: clicar numa linha abre o popup de detalhe; "Editar" no detalhe abre o popup de edição pré-preenchido; "Excluir" no detalhe abre a confirmação de exclusão e exclui com sucesso
- [x] 10. Aplicar a mesma restrição de largura (`maxWidth:'920px', margin:'0 auto', padding:'40px 40px 60px', boxSizing:'border-box'`) ao wrapper raiz de `routes/CategoriesPage.tsx`
- [x] 11. Remover a rota `expenses/:id` e o import de `ExpenseDetailPage` em `app/router.tsx`
- [x] 12. Deletar `routes/ExpenseDetailPage.tsx` e `routes/ExpenseDetailPage.test.tsx`
- [x] 13. Deletar `features/expenses/components/ExpenseNotFound.tsx` (sem consumidores)
- [x] 14. Deletar `lib/categories/CategoryBadge.tsx` e `CategoryBadge.test.tsx` (sem consumidores)
- [x] 15. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e `npm run build`; garantir 100% dos testes passando
- [x] 16. Revisão manual: conferir que nenhuma página fora do escopo (início, ajustes, relatórios, menu) mudou de aparência, e que Transações/Categorias ficam com o conteúdo centralizado em até 920px — via checklist estático (já que não há navegador neste ambiente) e/ou revisão visual do usuário
- [x] 17. Atualizar `spec.md` marcando todos os critérios de aceite concluídos (`- [x]`)
