# Tasks — FEAT-29: Permissões por role na UI

- [x] 1. Criar `lib/permissions/types.ts` (`MemberRole`, mesmo union já
      usado em `features/members/api/membersApi.ts`)
- [x] 2. Criar `lib/permissions/permissionErrors.ts`: `NetworkError`,
      `SessionExpiredError`, `UnknownPermissionError` (mesmo padrão de
      `lib/categories/categoryErrors.ts`)
- [x] 3. Criar `lib/permissions/membershipReadApi.ts` (tipo
      `MembershipItem { email, role }`,
      `membershipReadApi.getMembers(token)` → `GET /members`, mesmo
      padrão `safeFetch`/`assertListOk` de `lib/categories/
      categoriesReadApi.ts`) e `membershipReadApi.test.ts` cobrindo:
      sucesso retorna `items`, 401 expõe `SessionExpiredError`, falha de
      rede expõe `NetworkError`, outro status expõe
      `UnknownPermissionError`
- [x] 4. Criar `lib/permissions/rules.ts`
      (`canCreateTransaction(role)`, `canManageTransaction(role,
      isOwn)`, `canWriteCategories(role)`) e `rules.test.ts` cobrindo a
      matriz completa dos 4 papéis (incluindo `role: null`) das 3
      funções
- [x] 5. Criar `lib/permissions/useMyRole.ts` (cruza `useCurrentUser()`
      de `lib/auth` + `membershipReadApi.getMembers` por e-mail, mesmo
      esqueleto de loading/erro combinados de `routes/MembersPage.tsx`)
      e `useMyRole.test.ts` cobrindo: resolve `role`/`userId` quando o
      e-mail bate com um item de `GET /members`, `isLoading` verdadeiro
      enquanto qualquer uma das duas chamadas está pendente, erro de
      qualquer uma das duas é exposto (`SessionExpiredError` limpa a
      authStore), `role` permanece `null` se nenhum item bater com o
      e-mail
- [x] 6. Adicionar `ForbiddenError` em
      `features/transactions/errors/transactionErrors.ts` (mensagem
      "Seu nível de acesso não permite esta ação.", mesmo texto de
      `features/members/errors/memberErrors.ts#ForbiddenError`)
- [x] 7. Atualizar `features/transactions/api/transactionsApi.ts`:
      `assertOk`, `assertUpdateOk` e `assertDeleteOk` passam a mapear
      403 → `ForbiddenError`; atualizar `transactionsApi.test.ts` com um
      caso de 403 para cada um dos três métodos de escrita
      (`registerTransaction`, `updateTransaction`, `deleteTransaction`)
- [x] 8. Atualizar `features/transactions/components/
      TransactionDetailDialog.tsx`: novo prop `canManage: boolean`
      controlando a exibição de "Excluir" e "Editar" (mantendo "Fechar"
      sempre visível; ajustar o alinhamento do rodapé quando
      `canManage=false`); atualizar `TransactionDetailDialog.test.tsx`
      cobrindo `canManage=true` (mostra os dois botões) e
      `canManage=false` (esconde os dois, só "Fechar")
- [x] 9. Atualizar `routes/TransactionsListPage.tsx`: consumir
      `useMyRole()`; `canCreate = canCreateTransaction(role)` controla
      "+ Nova despesa"/"+ Nova receita"; ao abrir
      `TransactionDetailDialog`, calcular `canManage =
      canManageTransaction(role, transaction.createdByUserId ===
      userId)` e repassar como prop; atualizar
      `TransactionsListPage.test.tsx` cobrindo papel `Leitura` (sem
      botões de criar, sem "Editar"/"Excluir" em nenhuma transação),
      `Lancar` (com botões de criar; "Editar"/"Excluir" só na transação
      própria), `Total`/`Titular` (tudo visível, inclusive em transação
      de outro membro), e o estado de carregamento do papel (nenhum
      botão de escrita antes de `useMyRole` resolver)
- [x] 10. Adicionar `ForbiddenError` em
      `features/categories/errors/categoryErrors.ts` (mesma mensagem)
- [x] 11. Atualizar `features/categories/api/categoriesWriteApi.ts`:
      `assertWriteOk` e `assertDeleteOk` passam a mapear 403 →
      `ForbiddenError`; atualizar `categoriesWriteApi.test.ts` com um
      caso de 403 para `createCategory`, `updateCategory` e
      `deleteCategory`
- [x] 12. Atualizar `features/categories/components/CategoryList.tsx`:
      novo prop `canWrite: boolean` controlando a exibição do bloco de
      ícones editar/excluir de cada linha (rótulo de orçamento continua
      sempre visível); atualizar `CategoryList.test.tsx` cobrindo
      `canWrite=true`/`false`
- [x] 13. Atualizar `routes/CategoriesPage.tsx`: consumir `useMyRole()`;
      `canWrite = canWriteCategories(role)` controla o botão "+ Nova
      categoria" e é repassado pra `CategoryList`; atualizar
      `CategoriesPage.test.tsx` cobrindo papel `Leitura`/`Lancar` (sem
      botão de criar, sem ícones de editar/excluir em nenhuma
      categoria), `Total`/`Titular` (tudo visível), e o estado de
      carregamento do papel
- [x] 14. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e
      `npm run build`; confirmar 100% dos testes passando, sem erro de
      tipo e sem warning novo de lint
- [ ] 15. Revisão manual no app real (backend local + LocalStack/
      cognito-local), com contas nos 4 papéis (`Leitura`, `Lancar`,
      `Total`/`Titular`, e uma transação lançada por outro membro pra
      testar o caso de `Lancar` sobre transação alheia) — conferir que
      cada botão de escrita aparece/some exatamente conforme a matriz
      da spec nas telas de Transações e Categorias
      **Adiada por decisão do usuário ao final da implementação** — a
      suíte automatizada (517/517) já cobre cada cenário por papel nas
      duas telas; retomar quando conveniente
- [x] 16. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
