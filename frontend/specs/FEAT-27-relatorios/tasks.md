# Tasks — FEAT-27: Relatórios

- [ ] 1. Atualizar `styles/modernist/modernist.css`: nova classe
      `.card-title` (bundle base do design system, ainda não
      vendorizada), escopada sob `.ds-modernist`, seguindo o padrão já
      usado pelas classes `.card`/`.card-kicker`/`.elev-sm` (FEAT-26)
- [ ] 2. Criar `features/reports/utils/period.ts` com `getCurrentDate()`,
      `formatPercent(value)` e `formatComparisonLabel(variacaoPercentual,
      period)`, e `period.test.ts` cobrindo: `getCurrentDate()` retorna
      `YYYY-MM-DD` da data corrente (mockar `Date`), `formatPercent`
      formata inteiro sem casa decimal e valor com 1 casa decimal com
      vírgula (`54.4` → `"54,4"`), `formatComparisonLabel` monta o sinal
      `+`/`-` e o rótulo certo pra cada `period` (semana/mês/ano
      passada(o)), incluindo `variacaoPercentual = 0` (sinal `+`)
- [ ] 3. Criar `features/reports/api/reportsApi.ts`: tipos `ReportPeriod`,
      `ReportsResponse`, `ReportCategoryItem`, `ReportTopCategory` e
      `reportsApi.getReports(token, period, date)` (`GET
      /reports?period=&date=`, mesmo padrão `safeFetch`/`assertOk` de
      `summaryApi.ts`)
- [ ] 4. Criar `features/reports/errors/reportsErrors.ts`:
      `SessionExpiredError`, `NetworkError`, `UnknownReportsError`
      (mesmas mensagens padrão já usadas nas outras features)
- [ ] 5. Criar `features/reports/hooks/useReports.ts` (`useReports(period,
      date)` → `{ data, isLoading, error }`) e `useReports.test.ts`
      cobrindo: carrega ao montar, refaz a busca quando `period` ou
      `date` mudam, erro 401 expõe `SessionExpiredError` e limpa a
      authStore, falha de rede expõe `NetworkError`, outro status expõe
      `UnknownReportsError`
- [ ] 6. Criar `features/reports/components/PeriodToggle.tsx` e
      `PeriodToggle.test.tsx`: três opções (Semana/Mês/Ano) via
      `.seg`/`.seg-opt`, a selecionada reflete `value`, clicar em cada
      uma chama `onChange` com o período certo
- [ ] 7. Criar `features/reports/components/CategoryReportList.tsx` e
      `CategoryReportList.test.tsx`: lista os itens de `porCategoria` na
      ordem recebida, com nome, valor formatado e barra proporcional ao
      maior gasto da lista (primeiro item = 100%), sempre cor neutra;
      lista vazia mostra "Nenhuma despesa neste período."
- [ ] 8. Criar `features/reports/components/TotalPeriodCard.tsx` e
      `TotalPeriodCard.test.tsx`: mostra o total formatado; linha de
      comparação aparece com o texto certo quando `variacaoPercentual`
      não é `null` (casos positivo/negativo/zero, e rótulo certo por
      `period`); linha de comparação ausente quando `variacaoPercentual`
      é `null`
- [ ] 9. Criar `features/reports/components/TopCategoryCard.tsx` e
      `TopCategoryCard.test.tsx`: mostra nome e valor da categoria
      quando `category` não é `null`; mostra "X% do orçamento" quando
      `percentualOrcamento` não é `null`; omite o percentual quando é
      `null`; mostra "Nenhum gasto registrado" quando `category` é
      `null`
- [ ] 10. Renomear `routes/ReportsComingSoonPage.tsx` (e seu teste) para
      `routes/ReportsPage.tsx`, implementando a tela de verdade:
      cabeçalho ("Relatórios" + `PeriodToggle`, período inicial `month`),
      grid com `CategoryReportList` à esquerda e `TotalPeriodCard` +
      `TopCategoryCard` à direita; estados de carregando/erro seguindo o
      padrão já usado nas demais telas
- [ ] 11. Reescrever `routes/ReportsPage.test.tsx` (era
      `ReportsComingSoonPage.test.tsx`) cobrindo: renderiza com período
      `month` por padrão a partir do mock de `GET /reports`; trocar pra
      "Semana"/"Ano" refaz a busca com o `period` certo (mesma data);
      estado vazio (`totalCents=0`, `porCategoria=[]`, `maiorGasto=null`)
      não quebra a tela; erro de sessão expirada
- [ ] 12. Atualizar `app/router.tsx`: import/uso de `ReportsPage` no
      lugar de `ReportsComingSoonPage`
- [ ] 13. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e
      `npm run build`; confirmar 100% dos testes passando, sem erro de
      tipo e sem warning novo de lint
- [ ] 14. Revisão manual/visual: no app real (backend local +
      LocalStack/cognito-local), conferir a tela "Relatórios" com dados
      reais (despesas em várias categorias, em semanas/meses/anos
      diferentes, pra exercitar os três períodos e a comparação com o
      período anterior) — o seletor de período, a lista "Gasto por
      categoria", os cards "Total no período" e "Maior gasto" (com e sem
      orçamento definido), e o estado vazio — contra
      `frontend/design-system/web/jrnexpenses-web.dc.html` (bloco
      `isRep`)
- [ ] 15. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
