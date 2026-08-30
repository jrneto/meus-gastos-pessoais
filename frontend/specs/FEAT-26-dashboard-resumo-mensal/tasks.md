# Tasks — FEAT-26: Dashboard (Início) — resumo mensal

- [ ] 1. Atualizar `styles/modernist/modernist.css`: novo token
      `--shadow-sm`; classes `.card`, `.card-kicker`, `.elev-sm`
      (bundle base do design system); `.je-track`/`.je-fill` (barra de
      progresso, do próprio `.dc.html`) — tudo escopado sob
      `.ds-modernist`, seguindo o padrão já usado pelas classes
      existentes
- [ ] 2. Criar `features/summary/utils/month.ts` com
      `getCurrentYearMonth()` e `formatMonthLabel(month)`, e
      `month.test.ts` cobrindo: `getCurrentYearMonth()` retorna
      `YYYY-MM` do mês corrente (mockar `Date`), `formatMonthLabel`
      formata meses variados (incluindo janeiro/dezembro) no formato
      "Mês de Ano" com a primeira letra maiúscula
- [ ] 3. Criar `features/summary/api/summaryApi.ts`: tipos
      `SummaryResponse`/`CategorySummaryItem`/`SummaryTransactionItem`
      e `summaryApi.getSummary(token, month)` (`GET /summary?month=`,
      mesmo padrão `safeFetch`/`assertOk` de `transactionsApi.ts`)
- [ ] 4. Criar `features/summary/errors/summaryErrors.ts`:
      `SessionExpiredError`, `NetworkError`, `UnknownSummaryError`
      (mesmas mensagens padrão já usadas nas outras features)
- [ ] 5. Criar `features/summary/hooks/useSummary.ts` (`useSummary(month)`
      → `{ data, isLoading, error, refetch }`) e `useSummary.test.ts`
      cobrindo: carrega ao montar, `refetch()` refaz a busca, erro 401
      expõe `SessionExpiredError` e limpa a authStore, falha de rede
      expõe `NetworkError`, outro status expõe `UnknownSummaryError`
- [ ] 6. Criar `features/summary/components/SummaryCards.tsx` e
      `SummaryCards.test.tsx`: os 5 cartões com os valores formatados;
      saldo negativo com sinal `-` e cor accent; restante negativo com
      sinal `-`, cor accent, e barra travada em 100%; restante positivo
      com barra proporcional (`gasto/orçamentoTotal`)
- [ ] 7. Criar `features/summary/components/CategorySpendingList.tsx`
      e `CategorySpendingList.test.tsx`: lista os itens de
      `porCategoria` com gasto/orçamento formatado e barra de
      progresso; categoria com `gastoCents > orcamentoMensalCents`
      aparece com texto/barra na cor de destaque e barra travada em
      100%; lista vazia mostra estado vazio
- [ ] 8. Criar `features/summary/components/RecentTransactionsList.tsx`
      e `RecentTransactionsList.test.tsx`: lista os itens de
      `ultimosLancamentos` com categoria (via `useCategories`/
      `CategoryLetterTile`), descrição, data e valor com sinal/cor por
      tipo; itens sem `onClick`/não interativos; lista vazia mostra
      estado vazio
- [ ] 9. Atualizar `features/transactions/hooks/useTransactionsQuery.ts`:
      parâmetro opcional `initialFilters: GetTransactionsParams = {}`,
      usado na busca de montagem; atualizar
      `useTransactionsQuery.test.ts` com um teste novo cobrindo
      `initialFilters` sendo usado na primeira página, e confirmar que
      os testes existentes (sem argumento) continuam passando
- [ ] 10. Atualizar `features/transactions/components/TransactionFilters.tsx`:
      prop opcional `initialValues`; painel "Filtros avançados" abre
      expandido quando há valor inicial; atualizar
      `TransactionFilters.test.tsx` com um teste novo cobrindo
      `initialValues={{ yearMonth: '2026-08' }}` abrindo o painel já
      expandido com o campo preenchido, e confirmar que os testes
      existentes continuam passando
- [ ] 11. Atualizar `routes/TransactionsListPage.tsx`: lê `yearMonth`
      da query string (`useSearchParams`) e repassa como filtro
      inicial pros dois itens acima; atualizar
      `TransactionsListPage.test.tsx` com um teste novo cobrindo
      `/transactions?yearMonth=2026-08` já chegando filtrada, e
      confirmar que os testes existentes continuam passando
- [ ] 12. Renomear `routes/HomePage.tsx` para `routes/DashboardPage.tsx`,
      implementando a tela de verdade: cabeçalho ("Resumo" +
      `formatMonthLabel`) com os botões "+ Nova receita"/"+ Nova
      despesa" (mesma ordem/estilo da FEAT-24), `SummaryCards`,
      `CategorySpendingList` (com "Ver todas (N) →" para
      `/categories`), `RecentTransactionsList` (com "Ver todas →" para
      `/transactions?yearMonth=<mês corrente>`), `TransactionFormDialog`
      reaproveitado com `onSaved` chamando `refetch`; estados de
      carregando/erro seguindo o padrão já usado nas demais telas
- [ ] 13. Criar `routes/DashboardPage.test.tsx` cobrindo: renderiza os
      cinco cartões com os valores do mock de `GET /summary`; clicar
      em "+ Nova despesa"/"+ Nova receita" abre o popup já existente
      fixo no tipo certo; salvar uma transação refaz a busca do resumo;
      "Ver todas" de últimos lançamentos aponta para
      `/transactions?yearMonth=`; "Ver todas" de categorias aponta para
      `/categories`; estados vazios das duas listas; erro de sessão
      expirada
- [ ] 14. Atualizar `app/router.tsx`: import/uso de `DashboardPage` no
      lugar de `HomePage`
- [ ] 15. Atualizar `components/nav/navConfig.ts`: item `home` com
      `status: 'active'` (era `'placeholder'`)
- [ ] 16. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e
      `npm run build`; confirmar 100% dos testes passando, sem erro de
      tipo e sem warning novo de lint
- [ ] 17. Revisão manual/visual: no app real (backend local +
      LocalStack/cognito-local), conferir a tela "Início" com dados
      reais (transações e categorias com orçamento seedadas via API) —
      os 5 cartões, a barra do Restante (inclusive um cenário de
      estouro, pra ver o negativo/100% na prática), "Onde o dinheiro
      foi", "Últimos lançamentos", os botões de nova despesa/receita
      atualizando o resumo ao salvar, e os dois links "Ver todas" —
      contra `frontend/design-system/web/jrnexpenses-web.dc.html`
      (bloco `isDash`)
- [ ] 18. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
