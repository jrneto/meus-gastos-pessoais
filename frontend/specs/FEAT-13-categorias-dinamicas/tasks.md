# Tasks — FEAT-13: Categorias dinâmicas

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Caminhos relativos a
`frontend/app/src/`.

## `lib/categories/` — leitura compartilhada

- [x] 1. Criar `lib/categories/types.ts` (`CategoryItem`)
- [x] 2. Criar `lib/categories/categoryIcons.ts` (`CATEGORY_ICONS` com os 16 ícones curados, `findCategoryIcon`)
- [x] 3. Criar `lib/categories/categoryErrors.ts` (`SessionExpiredError`, `NetworkError`, `UnknownCategoryError`)
- [x] 4. Criar `lib/categories/categoriesReadApi.ts` (`getCategories(token)`, `assertListOk` mapeando 401/outros, `safeFetch`) e teste `categoriesReadApi.test.ts` (sucesso, 401, erro de rede, erro inesperado — via MSW)
- [x] 5. Implementar `lib/categories/useCategories.ts` (fetch on mount, cancelled guard, `clearSession()` em `SessionExpiredError`) e teste `useCategories.test.ts` (sucesso, 401 com verificação de `clearSession`, erro de rede — via MSW)
- [x] 6. Implementar `lib/categories/CategoryBadge.tsx` (ícone + nome com a cor da categoria; `category` indefinida renderiza rótulo genérico "Categoria não encontrada") e teste `CategoryBadge.test.tsx`

## `features/categories/` — CRUD

- [x] 7. Criar `features/categories/schemas/categorySchema.ts` (`nome`/`cor`/`icone`, `ICON_VALUES` a partir de `CATEGORY_ICONS`) e teste `categorySchema.test.ts` (válidos/inválidos por campo)
- [x] 8. Criar `features/categories/errors/categoryErrors.ts` (`ValidationError`, `NameConflictError`, `CategoryInUseError`, `NotFoundError`, re-exportando `SessionExpiredError`/`NetworkError`/`UnknownCategoryError` de `lib/categories/categoryErrors`)
- [x] 9. Criar `features/categories/api/categoriesWriteApi.ts` (`createCategory`, `updateCategory`, `deleteCategory`, `extractErrorCode` lendo o `type` do `ProblemDetails`, `assertWriteOk`/`assertDeleteOk` distinguindo `name-conflict`/`category-in-use`) e teste `categoriesWriteApi.test.ts` (sucesso e cada erro mapeado, incluindo a distinção dos dois 422 — via MSW)
- [x] 10. Implementar `features/categories/hooks/useRegisterCategory.ts` e teste (`success`, 400, 422 `name-conflict`, 401 com `clearSession`)
- [x] 11. Implementar `features/categories/hooks/useUpdateCategory.ts` e teste (`success`, 400, 404, 422 `name-conflict`, 401 com `clearSession`)
- [x] 12. Implementar `features/categories/hooks/useDeleteCategory.ts` e teste (`success`, 404, 422 `category-in-use`, 401 com `clearSession`)
- [x] 13. Implementar `features/categories/components/IconPicker.tsx` (grid de botões, `aria-pressed`, `Controller`-friendly) e teste `IconPicker.test.tsx` (seleção via clique, estado `aria-pressed`)
- [x] 14. Implementar `features/categories/components/CategoryFormFields.tsx` (nome, `<input type="color">` com hex visível, `IconPicker` via `Controller`, erros inline)
- [x] 15. Implementar `features/categories/components/NewCategoryForm.tsx` (`useForm` + `categorySchema` + `useRegisterCategory`, reset em sucesso, erro 422 `name-conflict` inline no campo nome, demais erros em `Alert`) e teste `NewCategoryForm.test.tsx`
- [x] 16. Implementar `features/categories/components/CategoryNotFound.tsx`
- [x] 17. Implementar `features/categories/components/EditCategoryForm.tsx` (recebe `category` como prop, `useUpdateCategory`, navega para `/categories` em sucesso) e teste `EditCategoryForm.test.tsx`
- [x] 18. Implementar `features/categories/components/CategoryDeleteDialog.tsx` (mesmo formato de `ExpenseDeleteDialog`, `CategoryInUseError` como alerta dentro do diálogo, `NotFoundError` remove item)
- [x] 19. Implementar `features/categories/components/CategoryList.tsx` (lista vazia com CTA, itens com `CategoryBadge` + ações editar/excluir, integra `CategoryDeleteDialog`) e teste `CategoryList.test.tsx` (vazio, itens, exclusão remove item, `category-in-use` mantém item com alerta)
- [x] 20. Criar `routes/CategoriesPage.tsx` e teste `CategoriesPage.test.tsx` (integração via MSW)
- [x] 21. Criar `routes/NewCategoryPage.tsx` e teste `NewCategoryPage.test.tsx`
- [x] 22. Criar `routes/EditCategoryPage.tsx` (usa `useCategories()` + `useParams`, localiza por `id`, `CategoryNotFound` se não encontrar) e teste `EditCategoryPage.test.tsx` (carregando, não encontrada, sucesso pré-preenchido)
- [x] 23. Atualizar `app/router.tsx` (rotas `categories`, `categories/new`, `categories/:id/edit`, dentro de `AppShell`)
- [x] 24. Atualizar `components/nav/navConfig.ts` (item `categories`: `disabled` → `active`, `to: '/categories'`) e ajustar `navConfig.test.ts`/testes de `AppShell` relacionados

## Migração de `features/expenses/` para `categoryId`

- [x] 25. Atualizar `features/expenses/schemas/expenseSchema.ts` (`category` enum → `categoryId: z.string().min(1, ...)`, remove dependência de `EXPENSE_CATEGORIES`) e ajustar `expenseSchema.test.ts`
- [x] 26. Atualizar `features/expenses/schemas/expenseFilterSchema.ts` (`category` → `categoryId`) e ajustar `expenseFilterSchema.test.ts`
- [x] 27. Atualizar `features/expenses/api/expensesApi.ts` (`category` → `categoryId` em todos os payloads/params/responses) e ajustar testes que dependam desses tipos
- [x] 28. Atualizar `features/expenses/components/ExpenseFormFields.tsx` (recebe `categories: CategoryItem[]` como prop, `Select` de `categoryId` populado a partir dela, remove import de `EXPENSE_CATEGORIES`)
- [x] 29. Atualizar `features/expenses/components/ExpenseForm.tsx` (`useCategories()`, guarda de lista vazia com CTA "Criar categoria" para `/categories/new`, repassa `categories` para `ExpenseFormFields`) e ajustar `ExpenseForm.test.tsx` (envia `categoryId`, caso de lista vazia)
- [x] 30. Atualizar `features/expenses/components/EditExpenseForm.tsx` (mesma guarda/`useCategories()`) e ajustar `EditExpenseForm.test.tsx`
- [x] 31. Atualizar `features/expenses/components/ExpenseFilters.tsx` (`Select` de `categoryId` dinâmico via `useCategories()`) e ajustar `ExpenseFilters.test.tsx` (via MSW mockando `GET /categories`)
- [x] 32. Atualizar `features/expenses/components/ExpenseList.tsx` (remove `categoryLabel`, resolve `categoryId` via `useCategories()` + `Map`, renderiza `CategoryBadge`) e ajustar `ExpenseList.test.tsx` (categoria resolvida, `categoryId` sem correspondência renderiza rótulo genérico)
- [x] 33. Atualizar `routes/ExpenseDetailPage.tsx` (mesma resolução via `CategoryBadge`) e ajustar `ExpenseDetailPage.test.tsx`
- [x] 34. Remover `features/expenses/constants/expenseCategories.ts` (e teste dedicado, se existir) — confirmar (`grep`) que não há mais nenhuma referência a `EXPENSE_CATEGORIES`/campo `category` em `features/expenses/`

## Fechamento

- [x] 35. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [ ] 36. Validação manual: fluxo completo (criar/editar/excluir categoria, bloqueio de exclusão com despesa associada, cadastro/edição/listagem/filtro de despesa por categoria, usuário sem categoria orientado a criar uma) contra o backend real
- [x] 37. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)
