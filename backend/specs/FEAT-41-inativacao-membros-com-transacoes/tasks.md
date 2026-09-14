# Tasks — FEAT-41: Inativação de membros com transações lançadas

- [x] 1. Domain: adicionar `MembershipStatus.Inativo` ao enum em
      `GastosApp.Domain/Accounts/Membership.cs`, atualizando o comentário
      do enum pra descrever o novo valor (membro perdeu acesso, mas o
      registro persiste)

- [x] 2. Application: adicionar `MembershipErrors.CannotModifyInactiveMember`
      (`cannot-modify-inactive-member`, 422) e
      `MembershipErrors.MemberAlreadyInactive` (`member-already-inactive`,
      422) em `Members/MembershipErrors.cs`

- [x] 3. Application: adicionar `Task<bool> InactivateAsync(string accountId, string membershipId, CancellationToken)`
      à interface `IMembershipRepository`

- [x] 4. Application: adicionar `Task<bool> ExistsByCreatedByUserIdAsync(string accountId, string userId, CancellationToken)`
      à interface `ITransactionRepository`

- [x] 5. Infrastructure: implementar `DynamoDbMembershipRepository.InactivateAsync`
      (`UpdateItem` de `Status`, `ConditionExpression: attribute_exists(PK) AND Status = Ativo`,
      `GSI1PK`/demais atributos preservados, ver plan.md decisão técnica 1/3)

- [x] 6. Infrastructure: ajustar `DynamoDbMembershipRepository.CreateInviteAsync`
      pra ignorar membros `Inativo` na checagem de duplicidade de e-mail
      (`existingMembers.Any(m => m.Status != MembershipStatus.Inativo && ...)`)

- [x] 7. Infrastructure: implementar `DynamoDbTransactionRepository.ExistsByCreatedByUserIdAsync`
      (`Query` por `PK=ACCOUNT#<accountId>` + `begins_with(SK,"TXN#")` +
      `FilterExpression CreatedByUserId`, paginando em loop com
      `MaxPaginationIterations` até achar um item ou esgotar, ver plan.md)

- [x] 8. Application: atualizar `RemoveMemberCommandHandler` — injetar
      `ITransactionRepository`, adicionar as ramificações: `Inativo` existente
      → `MemberAlreadyInactive`; `Ativo` com transação lançada → chama
      `InactivateAsync` em vez de `DeleteAsync`; demais casos (sem transação,
      `ConvitePendente`) seguem removendo de fato como hoje

- [x] 9. Application: atualizar `UpdateMemberRoleCommandHandler` — bloquear
      troca de papel de um membro `Inativo` com `CannotModifyInactiveMember`,
      antes de chamar `UpdateRoleAsync`

- [x] 10. Application: atualizar `ResolveMembershipQueryHandler` — só resolver
      quando `membership.Status == MembershipStatus.Ativo`; caso contrário
      (incluindo `Inativo`), retornar `AccountErrors.NotResolved` (401)

- [x] 11. Application: atualizar o comentário de `CreatedByLabelResolver`
      (`Transactions/Common/CreatedByLabelResolver.cs`) removendo a menção ao
      débito técnico pendente e referenciando a FEAT-41 como quem o resolveu
      — sem mudança de código/comportamento

- [x] 12. Docs: atualizar `backend/docs/data-model.md`, seção `Membership` —
      `Status` passa a aceitar `Inativo`; registrar que `GSI1PK` permanece
      `USER#<userId>` mesmo depois da inativação (decisão técnica 1 do
      `plan.md`)

- [x] 13. Testes unitários (Domain): `MembershipTests` — round-trip de
      `Membership.Restore` com `Status=Inativo`

- [x] 14. Testes unitários (Application): `RemoveMemberCommandHandlerTests` —
      novos casos: `Ativo` sem transação (remove de fato, `InactivateAsync`
      nunca chamado); `Ativo` com transação (inativa, `DeleteAsync` nunca
      chamado); já `Inativo` (`MemberAlreadyInactive`, nenhum repositório de
      escrita chamado); `ConvitePendente` (remove de fato sem checar
      transações, `ExistsByCreatedByUserIdAsync` nunca chamado)

- [x] 15. Testes unitários (Application): `UpdateMemberRoleCommandHandlerTests`
      — novo caso: membro `Inativo` retorna `CannotModifyInactiveMember`,
      `UpdateRoleAsync` nunca chamado

- [x] 16. Testes unitários (Application/Infrastructure):
      `InviteMemberCommandHandlerTests`/`DynamoDbMembershipRepositoryTests` —
      novo caso: convite pro e-mail de um membro `Inativo` já existente tem
      sucesso (não é tratado como conflito)

- [x] 17. Testes unitários (Application): `ResolveMembershipQueryHandlerTests`
      — novo caso: `FindByAccountAndUserIdAsync` retorna membro `Inativo` →
      `AccountErrors.NotResolved`

- [x] 18. Testes unitários (Infrastructure): novos testes para
      `DynamoDbTransactionRepository.ExistsByCreatedByUserIdAsync` — sem
      nenhuma transação (`false`); com transação do autor (`true`); só com
      transação de outro autor (`false`)

- [x] 19. Testes de componente: `MemberEndpointsTests` — novos casos:
      `DELETE /members/{id}` de `Ativo` com transação → 204 +
      `InactivateAsync` chamado; `DELETE /members/{id}` de `Ativo` sem
      transação → 204 + `DeleteAsync` chamado (regressão); `DELETE
      /members/{id}` de membro já `Inativo` → 422
      `member-already-inactive`; `PUT /members/{id}` de membro `Inativo` →
      422 `cannot-modify-inactive-member`; `GET /members` incluindo um item
      `Inativo` no mock → aparece na resposta com `status: "Inativo"`

- [x] 20. Teste de integração: `MembersFlowTests` — fluxo completo (convidar
      → aceitar via login → lançar transação como o membro → Titular chama
      `DELETE /members/{id}` → 204 → `GET /members` lista o membro como
      `Inativo` → nova chamada autenticada desse membro retorna 401)

- [x] 21. Rodar `dotnet build GastosApp.sln` e `dotnet test GastosApp.sln`
      (unit + component) — confirmar 100% dos testes passando, sem quebrar
      nenhum teste existente de `/members`/`/transactions`

- [x] 22. Rodar a suíte de integração localmente via
      `backend/infra/lambda/run-local.sh` (`--filter Category=Integration`) —
      obrigatório antes de considerar a feature concluída (constitution)

- [x] 23. Rodar `backend/scripts/export-openapi.sh` e conferir `git diff
      --stat backend/docs/openapi.json` — diff esperado vazio (nenhum campo
      ou status code novo no schema); documentar a confirmação

- [x] 24. Atualizar `backend/docs/backlog.md` — marcar o item "DÉBITO —
      `DELETE /members` remove o membro em vez de inativá-lo" como `[x]`,
      com nota apontando pra esta FEAT-41

- [x] 25. Atualizar `backend/specs/FEAT-41-inativacao-membros-com-transacoes/spec.md`
      — marcar todos os critérios de aceite concluídos (`- [x]`) e adicionar
      a seção "Status" resumindo o que foi implementado (mesmo padrão de
      FEAT-20/FEAT-22)
