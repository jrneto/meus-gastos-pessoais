# Tasks — FEAT-28: Membros da conta e convites

- [ ] 1. Atualizar `styles/modernist/modernist.css`: novas classes
      `.je-spin`, `.je-indet`, `.je-toast` (+ `@keyframes`
      correspondentes), escopadas sob `.ds-modernist`, seguindo o
      padrão já usado pelas classes existentes (só do `.dc.html`, fora
      do bundle base)
- [ ] 2. Criar `lib/auth/currentUserApi.ts` (tipo `CurrentUser`,
      `currentUserApi.getCurrentUser(token)` → `GET /auth/me`, mesmo
      padrão `safeFetch`/`assertOk` de `lib/categories/
      categoriesReadApi.ts`) e `lib/auth/currentUserErrors.ts`
      (`SessionExpiredError`, `NetworkError`,
      `UnknownCurrentUserError`)
- [ ] 3. Criar `lib/auth/useCurrentUser.ts` (mesmo esqueleto de
      `lib/categories/useCategories.ts`) e `useCurrentUser.test.ts`
      cobrindo: carrega ao montar, erro 401 expõe `SessionExpiredError`
      e limpa a authStore, falha de rede expõe `NetworkError`, outro
      status expõe `UnknownCurrentUserError`
- [ ] 4. Criar `components/Toast.tsx` e `Toast.test.tsx`: renderiza a
      mensagem quando `message` não é `null`; não renderiza nada quando
      é `null`; chama `onDismiss` automaticamente após o timeout
      (`vi.useFakeTimers`); reagenda o timeout quando `message` muda
      pra um novo valor antes do timeout anterior disparar
- [ ] 5. Criar `components/ProcessingOverlay.tsx` e
      `ProcessingOverlay.test.tsx`: renderiza o `label` recebido e o
      spinner/barra indeterminada
- [ ] 6. Criar `features/members/api/membersApi.ts`: tipos
      `MemberRole`/`MemberStatus`/`MemberItem`/`InviteMemberPayload` e
      `membersApi.{getMembers,inviteMember,updateMemberRole,
      removeMember}` (mesmo padrão `safeFetch`/`extractErrorCode` de
      `categoriesWriteApi.ts` pra disambiguar 409/422 pelo `type`)
- [ ] 7. Criar `features/members/errors/memberErrors.ts`:
      `SessionExpiredError`, `NetworkError`, `ValidationError`,
      `ForbiddenError`, `NotFoundError`, `ConflictError`,
      `CannotModifyTitularError`, `CannotRemoveTitularError`,
      `UnknownMemberError`
- [ ] 8. Criar `features/members/utils/roleLabels.ts`
      (`ROLE_LABEL`/`ROLE_DESCRIPTION`) e `roleLabels.test.ts` cobrindo
      os 4/3 mapeamentos
- [ ] 9. Criar `features/members/hooks/useMembers.ts` (`GET /members`,
      mesmo esqueleto de `useCategories`, sem `refetch` — ver plan.md)
      e `useMembers.test.ts` cobrindo: carrega ao montar, erro 401
      expõe `SessionExpiredError` e limpa a authStore, falha de rede
      expõe `NetworkError`, outro status expõe `UnknownMemberError`
- [ ] 10. Criar `features/members/hooks/useInviteMember.ts` (`POST
      /members`, mesmo esqueleto de `useRegisterCategory`) e
      `useInviteMember.test.ts` cobrindo: sucesso expõe `success`/
      `data`, 400 expõe `ValidationError`, 409 expõe `ConflictError`,
      403 expõe `ForbiddenError`, 401 expõe `SessionExpiredError` e
      limpa a authStore
- [ ] 11. Criar `features/members/hooks/useUpdateMemberRole.ts` (`PUT
      /members/{id}`, mesmo esqueleto de `useUpdateCategory`) e
      `useUpdateMemberRole.test.ts` cobrindo: sucesso expõe `success`/
      `data`, 404 expõe `NotFoundError`, 422 expõe
      `CannotModifyTitularError`, 403 expõe `ForbiddenError`
- [ ] 12. Criar `features/members/hooks/useRemoveMember.ts` (`DELETE
      /members/{id}`, mesmo esqueleto de `useDeleteCategory`) e
      `useRemoveMember.test.ts` cobrindo: sucesso expõe `success`, 404
      expõe `NotFoundError`, 422 expõe `CannotRemoveTitularError`, 403
      expõe `ForbiddenError`
- [ ] 13. Criar `features/members/components/MemberRow.tsx` e
      `MemberRow.test.tsx`: modo Titular (`readOnly=false`) mostra
      seletor de papel refletindo `member.role` e ícone de remover;
      trocar o seletor chama `PUT` e reflete a troca imediatamente
      (otimista); falha na troca reverte o seletor pro papel anterior e
      mostra erro inline; clicar em remover dispara
      `onRemoveRequested`; modo somente leitura (`readOnly=true`)
      mostra o papel como texto, sem seletor nem ícone de remover;
      `isMe` mostra o indicador "(você)"
- [ ] 14. Criar `features/members/components/MemberList.tsx` e
      `MemberList.test.tsx`: linha do Titular sempre destacada (tag
      "Titular", descrição fixa, "(você)" quando aplicável); lista os
      `others` via `MemberRow`, repassando `readOnly`/`isMe` corretos
      por linha
- [ ] 15. Criar `features/members/components/MemberRemoveDialog.tsx` e
      `MemberRemoveDialog.test.tsx`: mesmo padrão de
      `CategoryDeleteDialog` (confirmar chama `DELETE` e `onRemoved`;
      cancelar não chama a API; 404 trata como sucesso silencioso)
- [ ] 16. Criar `features/members/components/InviteMemberDialog.tsx` e
      `InviteMemberDialog.test.tsx`: papel inicial "Lançar"; mostra a
      descrição do papel selecionado; ao enviar, mostra
      `ProcessingOverlay` e desabilita os botões; sucesso chama
      `onInvited` e fecha; erro (400/409) mostra mensagem inline sem
      fechar o popup
- [ ] 17. Criar `routes/MembersPage.tsx`: busca `useMembers` +
      `useCurrentUser` em paralelo; deriva `titular`/`others`/
      `isViewerTitular`; cabeçalho "Membros da conta" + botão "+
      Convidar pessoa" só quando `isViewerTitular`; `MemberList`,
      `InviteMemberDialog`, `Toast`; estados de carregando/erro
      seguindo o padrão já usado nas demais telas
- [ ] 18. Criar `routes/MembersPage.test.tsx` cobrindo: Titular vê
      lista completa com ações e botão de convidar; não-Titular vê
      lista completa sem ações e sem botão de convidar; convidar com
      sucesso mostra o toast e atualiza a lista sem novo `GET`; trocar
      papel de um membro; remover um membro com confirmação; erro de
      sessão expirada
- [ ] 19. Atualizar `app/router.tsx`: nova rota `{ path: 'members',
      element: <MembersPage /> }`
- [ ] 20. Atualizar `components/nav/navConfig.ts`: novo item `members`
      ("Membros"), entre `categories` e `settings`
- [ ] 21. Rodar a suíte completa (`npm test`), `tsc -b`, `oxlint` e
      `npm run build`; confirmar 100% dos testes passando, sem erro de
      tipo e sem warning novo de lint
- [ ] 22. Revisão manual/visual: no app real (backend local +
      LocalStack/cognito-local), com pelo menos duas contas Cognito
      (uma Titular, outra convidada com papel não-Titular) — conferir
      convite, overlay "Enviando convite", toast, troca de papel,
      remoção com confirmação, e a visão somente leitura de quem não é
      Titular — contra `frontend/design-system/web/
      jrnexpenses-web.dc.html` (bloco `isMem` e diálogo
      `showInviteDialog`)
- [ ] 23. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos (`- [x]`)
