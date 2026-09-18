# Tasks — FEAT-35: Membros inativos

Checklist sequencial (cada item ~1 commit). Ordem segue dependência:
erros/tipos → API → hooks (+testes) → componentes (+testes) → página
(+testes) → spec.md.

- [x] 1. `errors/memberErrors.ts`: adicionar `CannotModifyInactiveMemberError`
      ("Não é possível alterar o papel de um membro inativo.") e
      `MemberAlreadyInactiveError` ("Este membro já está inativo."),
      mesmo padrão de `CannotModifyTitularError`/`CannotRemoveTitularError`.

- [x] 2. `api/membersApi.ts`: `MemberStatus` ganha `'Inativo'`;
      `assertUpdateOk` mapeia 422 `cannot-modify-inactive-member` →
      `CannotModifyInactiveMemberError`; `assertRemoveOk` mapeia 422
      `member-already-inactive` → `MemberAlreadyInactiveError` (mesmo
      padrão de `extractErrorCode` já usado pros dois 422 existentes).

- [x] 3. `hooks/useUpdateMemberRole.test.ts`: novo caso "422
      cannot-modify-inactive-member expõe CannotModifyInactiveMemberError"
      (mesmo formato do teste já existente pra `cannot-modify-titular`).

- [x] 4. `hooks/useRemoveMember.test.ts`: novo caso "422
      member-already-inactive expõe MemberAlreadyInactiveError".

- [x] 5. `utils/statusLabels.ts` (novo arquivo): `MEMBER_STATUS_LABEL`
      (`ConvitePendente` → "Convite pendente", `Ativo` → "Ativo",
      `Inativo` → "Inativo"), mesmo padrão de `utils/roleLabels.ts`.

- [x] 6. `utils/statusLabels.test.ts` (novo arquivo): testa os 3
      valores de `MEMBER_STATUS_LABEL` (mesmo formato de
      `roleLabels.test.ts`).

- [x] 7. `hooks/useMembers.ts`: extrai a busca pra
      `fetchMembers(showLoading: boolean)`; expõe `refetch()` (chama
      `fetchMembers(false)`, ativa `isRefetching` em vez de `isLoading`)
      mantendo o `useEffect(..., [token])` de montagem inalterado
      (`fetchMembers(true)`).

- [x] 8. `hooks/useMembers.test.ts`: novos casos — `refetch()` atualiza
      `items` sem tocar `isLoading` (deve permanecer `false` durante a
      chamada); `refetch()` ativa e desativa `isRefetching`; erro numa
      `refetch()` atualiza `error` normalmente.

- [x] 9. `components/MemberRow.tsx`: `isEffectivelyReadOnly = readOnly
      || member.status === 'Inativo'` usada nos dois pontos que hoje só
      checam `readOnly` (seletor de papel vs. texto; botão de remover);
      subtítulo de status passa a usar `MEMBER_STATUS_LABEL[member.status]`
      em vez da expressão hardcoded atual.

- [x] 10. `components/MemberRow.test.tsx`: novos casos — membro
      `Inativo` renderiza somente-leitura (papel como texto, sem botão
      de remover) mesmo com `readOnly={false}` (simulando o Titular);
      subtítulo mostra "Inativo" pra esse status (não "Ativo").

- [x] 11. `components/MemberRemoveDialog.tsx`: atualizar o texto de
      confirmação (ver `plan.md`): "Tem certeza que deseja remover
      "{email}" da conta? A pessoa perde acesso imediatamente. Se ela
      já tiver lançamentos registrados, o histórico é mantido e o
      vínculo fica marcado como inativo em vez de apagado."

- [x] 12. `components/MemberRemoveDialog.test.tsx`: atualizar a
      asserção do texto de confirmação pro novo copy; novo caso "422
      member-already-inactive mantém o dialog aberto com a mensagem
      específica, sem chamar onRemoved" (mesmo formato do teste já
      existente de erro inesperado/500).

- [x] 13. `routes/MembersPage.tsx`: `handleRemoved` chama `refetch()`
      (de `useMembers`) em vez de filtrar `localOthers`; renderiza um
      spinner discreto (`.je-spin` reduzido, `role="status"`) acima de
      `MemberList` quando `isRefetching` é `true`, sem esconder a
      lista.

- [x] 14. `routes/MembersPage.test.tsx`: atualizar "Titular remove um
      membro com confirmação" para um `GET /members` com estado
      mutável (mesmo padrão já usado no teste de convite,
      `getMembersCount`) que reflita o resultado após o `DELETE` — dois
      casos:
      - membro removido de fato: segundo `GET /members` não o inclui
        mais → some da lista (comportamento atual preservado)
      - membro inativado: segundo `GET /members` o inclui com
        `status: "Inativo"` → continua na lista, agora somente-leitura
        com rótulo "Inativo" (não some)

- [x] 15. `routes/MembersPage.test.tsx`: novo caso — durante o
      `refetch()` pós-remoção, a lista antiga permanece visível (não
      pisca/some) e o spinner (`role="status"`) aparece; usar promise
      controlada manualmente no mock do segundo `GET /members` (mesma
      técnica já usada pra evitar flakiness de timer, ver
      `frontend/docs/backlog.md` › item de flakiness da FEAT-32) em vez
      de um delay fixo.

- [x] 16. `hooks/useInviteMember.test.ts` (ou
      `components/InviteMemberDialog.test.tsx`): novo caso — convidar
      um e-mail que hoje pertence a um membro `Inativo` retorna 201 e
      não lança `ConflictError` (US8; sem mudança de código nesta
      feature, só cobertura).

- [x] 17. `frontend/specs/FEAT-35-membros-inativos/spec.md`: marcar os
      critérios de aceite concluídos (checklist `- [x]`).

- [x] 18. Rodar suíte completa (`npm run lint`, `npm test`,
      `tsc --noEmit`) e confirmar 100% verde antes de dar a feature por
      concluída (constitution).
