# Tasks — FEAT-30: Ajustes (migrar para Modernist + exportação CSV)

- [x] 1. Criar `lib/downloadFile.ts` com `downloadBlob(blob, filename)`
      (cria object URL, dispara clique num `<a download>` temporário,
      revoga a URL) e `downloadFile.test.ts` cobrindo: cria e revoga a
      object URL, define `href`/`download` corretos no link temporário e
      aciona o clique (mockar `URL.createObjectURL`/`revokeObjectURL` e
      espionar `HTMLAnchorElement.prototype.click`, indisponíveis por
      padrão no jsdom)
- [x] 2. Criar `features/settings/errors/settingsErrors.ts`:
      `SessionExpiredError`, `NetworkError`, `UnknownExportError`
      (mesmas mensagens padrão já usadas nas outras features)
- [x] 3. Criar `features/settings/api/settingsApi.ts`: constante
      `EXPORT_FILENAME = 'transacoes.csv'` e
      `settingsApi.exportTransactionsCsv(token)` (`GET
      /transactions/export`, mesmo padrão `safeFetch`/`assertOk` de
      `reportsApi.ts`, devolve `Blob`) e `settingsApi.test.ts` cobrindo:
      chamada com `Authorization` correto, `401` lança
      `SessionExpiredError`, falha de rede lança `NetworkError`, outro
      status lança `UnknownExportError`, sucesso devolve o `Blob` do
      corpo da resposta
- [x] 4. Criar `features/settings/hooks/useExportTransactions.ts`
      (`useExportTransactions()` → `{ exportCsv, isExporting, error,
      success }`, mesmo esqueleto de `useInviteMember.ts`: chama
      `settingsApi.exportTransactionsCsv`, aciona `downloadBlob` no
      sucesso, limpa a `authStore` quando o erro é
      `SessionExpiredError`) e `useExportTransactions.test.ts` cobrindo:
      `isExporting` fica `true` durante a chamada e `false` ao final,
      sucesso aciona `downloadBlob` com `EXPORT_FILENAME` e marca
      `success = true`, erro de sessão expirada limpa a `authStore` e
      expõe o erro, erro de rede expõe `NetworkError` sem mexer na
      `authStore`
- [x] 5. Criar `components/nav/AccountFooter.tsx` (avatar "VC" + rótulo
      "Sua conta" + botão "Sair", reaproveitando `useLogout()`, prop
      opcional `onBeforeLogout` chamada antes do logout) e
      `AccountFooter.test.tsx` cobrindo: renderiza avatar/rótulo, clicar
      em "Sair" chama `POST /auth/logout`, limpa a sessão e navega pro
      login, chama `onBeforeLogout` (quando informado) antes de navegar
- [x] 6. Atualizar `components/nav/navConfig.ts`: item `settings` com
      `label: 'Ajustes'` (era `'Configurações'`) e `status: 'active'`
      (era `'placeholder'`), e `navConfig.test.ts` refletindo o novo
      rótulo/status
- [x] 7. Atualizar `components/nav/DesktopSidebar.tsx`: renderizar
      `<AccountFooter />` após a lista de itens de navegação, com o
      divisor superior do protótipo; atualizar `DesktopSidebar.test.tsx`
      (rótulo "Ajustes" no lugar de "Configurações" nos testes
      existentes) e adicionar cobertura do rodapé "Sua conta / Sair"
      (renderiza e aciona logout)
- [x] 8. Atualizar `components/nav/NavMoreSheet.tsx`: renderizar
      `<AccountFooter onBeforeLogout={...} />` ao final do painel,
      fechando o painel (`onOpenChange(false)`) antes do logout;
      atualizar `NavMoreSheet.test.tsx` com a mesma cobertura do rodapé
- [ ] 9. Reescrever `routes/SettingsPage.tsx` para o Modernist: título
      "Ajustes", linha "Exportar dados" / botão "Exportar CSV" (estado
      ocupado com `isExporting`, rótulo "Exportando..." + `disabled`),
      erro inline (`role="alert"`) quando `error` não é `null`, toast de
      sucesso "Transações exportadas." (via `success` do
      `useExportTransactions` + `Toast`), `<AppVersion />` mantido; sem
      nenhum botão "Sair" nem classe shadcn/ui/Tailwind remanescente
- [ ] 10. Reescrever `routes/SettingsPage.test.tsx`: remover o teste do
      botão "Sair" (comportamento migrado pra `AccountFooter.test.tsx`
      na task 5); manter o teste de versão (`AppVersion`); adicionar
      cenários de exportação via MSW — sucesso (aciona download e
      mostra o toast), estado de carregamento (botão desabilitado com
      rótulo "Exportando..."), sessão expirada (limpa sessão, navega pro
      login), erro de rede (mensagem inline, botão volta ao normal)
- [ ] 11. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e `npm
      run build`; confirmar 100% dos testes passando, sem erro de tipo e
      sem warning novo de lint
- [ ] 12. Revisão manual/visual: no app real (backend local +
      LocalStack/cognito-local), conferir a tela "Ajustes" (título,
      "Exportar CSV" com download de verdade, estado ocupado, toast de
      sucesso, erro de sessão expirada) e o rodapé "Sua conta / Sair"
      tanto na `DesktopSidebar` (janela larga) quanto no painel "Mais" da
      `MobileBottomNav` (janela estreita), contra
      `frontend/design-system/web/jrnexpenses-web.dc.html` (blocos
      `isSet` e o rodapé de conta da sidebar)
- [ ] 13. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
