# Tasks — FEAT-24: Popup de nova receita

- [x] 1. Atualizar `features/transactions/hooks/useRegisterTransaction.ts`:
      hook passa a receber `tipo: 'despesa' | 'receita'` como argumento
      e usa esse valor no payload (em vez do `'despesa'` hardcoded);
      atualizar `useRegisterTransaction.test.ts` para passar `'despesa'`
      nas chamadas existentes e adicionar um teste cobrindo
      `tipo: 'receita'` no payload enviado
- [x] 2. Atualizar `features/transactions/hooks/useUpdateTransaction.ts`:
      mesma mudança (`tipo` como segundo argumento, ao lado de `id`);
      atualizar `useUpdateTransaction.test.ts` (passar `'despesa'` nas
      chamadas existentes) e adicionar teste cobrindo `tipo: 'receita'`
- [x] 3. Atualizar `features/transactions/components/TransactionForm.tsx`:
      prop obrigatória `tipo: 'despesa' | 'receita'`; renomear
      `expenseCategories` para `categoriesForTipo`, filtrando por
      `category.tipo === tipo`; texto do estado vazio interpola
      `` `categoria de ${tipo}` ``; rótulo do botão de criar interpola
      `` `Registrar ${tipo}` `` (edição continua "Salvar alterações");
      hooks chamados como `useRegisterTransaction(tipo)`/
      `useUpdateTransaction(transactionId ?? '', tipo)`
- [x] 4. Atualizar `features/transactions/components/TransactionForm.test.tsx`:
      passar `tipo="despesa"` em todos os `renderForm()` existentes
      (mantendo os testes de despesa passando sem mudança de
      comportamento); adicionar testes com `tipo="receita"`: dropdown
      só com categoria de receita, estado vazio com o texto "categoria
      de receita", botão "Registrar receita", submit chama
      `POST /transactions` com `tipo: "receita"`, edição de receita
      chama `PUT` preservando `tipo: "receita"`
- [x] 5. Atualizar
      `features/transactions/components/TransactionFormDialog.tsx`:
      prop opcional `tipo` (usada só ao criar); `effectiveTipo` deriva
      de `data.tipo` (editar, com fallback `'despesa'` durante
      `isLoading`) ou de `tipo` (criar); título "Nova receita"/"Editar
      receita" quando aplicável; `TransactionForm` recebe
      `tipo={data.tipo}` (editar) ou `tipo={effectiveTipo}` (criar)
- [x] 6. Atualizar
      `features/transactions/components/TransactionFormDialog.test.tsx`:
      passar `tipo="despesa"` nos `renderDialog()` de criação
      existentes; adicionar testes: criar com `tipo="receita"` mostra
      título "Nova receita" e salva com sucesso; editar uma transação
      cujo `GET /transactions/{id}` retorna `tipo: "receita"` mostra
      título "Editar receita" (sem precisar passar `tipo` por fora)
- [x] 7. Atualizar
      `features/transactions/components/TransactionDetailDialog.tsx`:
      título ("Detalhe da despesa"/"Detalhe da receita"), cor
      (`--color-accent-700`/`--color-positive-700`) e sinal (`- `/`+ `)
      do valor derivados de `transaction.tipo`
- [x] 8. Atualizar
      `features/transactions/components/TransactionDetailDialog.test.tsx`:
      manter os testes existentes (fixture de despesa); adicionar
      teste com fixture de receita cobrindo título "Detalhe da
      receita", cor positive e sinal `+` no valor exibido
- [x] 9. Atualizar
      `features/transactions/components/TransactionDeleteDialog.tsx`:
      título ("Excluir despesa"/"Excluir receita") derivado de
      `transaction.tipo`
- [x] 10. Atualizar
      `features/transactions/components/TransactionDeleteDialog.test.tsx`:
      manter os testes existentes; adicionar teste com fixture de
      receita cobrindo o título "Excluir receita"
- [x] 11. Atualizar `routes/TransactionsListPage.tsx`: `TransactionFormTarget`
      ganha `tipo` no branch `create`; novo botão "+ Nova receita"
      (secundário, antes do "+ Nova despesa" primário — ordem do
      `.dc.html`) chamando `setFormTarget({ mode: 'create', tipo: 'receita' })`;
      `TransactionFormDialog` recebe `tipo`; `key` do dialog inclui o
      tipo no modo criar (`` `create-${formTarget.tipo}` ``); remover o
      comentário que adiava o botão pra esta feature
- [x] 12. Atualizar `routes/TransactionsListPage.test.tsx`: remover/
      substituir o teste que checava a ausência do botão "+ Nova
      receita" (FEAT-23) por um teste que confirma sua presença;
      adicionar teste cobrindo o fluxo completo de nova receita
      (clicar "+ Nova receita" → preencher → `POST /transactions` com
      `tipo: "receita"` → aparece na listagem)
- [x] 13. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e
      `npm run build`; confirmar 100% dos testes passando, sem erro de
      tipo e sem warning novo de lint
- [x] 14. Revisão manual/visual: no app real (backend local +
      LocalStack/cognito-local), conferir os dois botões na tela de
      Transações, criar uma receita do zero pela UI (sem seedar via
      API), editar essa receita, ver o detalhe dela (título/cor/sinal
      corretos), excluí-la — e confirmar que o fluxo de despesa
      continua idêntico ao da FEAT-23, contra
      `frontend/design-system/web/jrnexpenses-web.dc.html`
- [x] 15. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
