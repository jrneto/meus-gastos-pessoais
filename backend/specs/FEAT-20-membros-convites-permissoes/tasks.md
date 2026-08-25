# Tasks — FEAT-20: Membros da conta, convites e permissões

Ordem pensada pra manter dependência antes de dependente (Domain →
Application → Infrastructure → Api → CognitoTriggers → testes). Cada
item é do tamanho de um commit.

## Domain

- [x] 1. Reformar `Membership`
      (`GastosApp.Domain/Accounts/Membership.cs`): `enum
      MembershipRole` ganha `Leitura`/`Lancar`/`Total` (além de
      `Titular`); novo `enum MembershipStatus { Ativo,
      ConvitePendente }`; entidade ganha `Id`/`Email`; novos factory
      methods `CreateTitular(accountId, userId, email)`,
      `CreateInvite(accountId, email, role)` (gera `Id` internamente,
      `Status=ConvitePendente`, `UserId=null`), `Restore(id, accountId,
      userId, email, role, status, createdAt)`.

## Application

- [x] 2. Adicionar `Error.Forbidden(code, message)` em
      `Common/Results/Error.cs` e o membro `Forbidden` em
      `Common/Results/ErrorType.cs`.
- [x] 3. Atualizar `IAccountRepository`
      (`Common/Interfaces/IAccountRepository.cs`): `CreateAsync` ganha
      parâmetro `email`; novo método `SetActiveAccountAsync(userId,
      accountId, ct)`.
- [x] 4. Criar `IMembershipRepository` +
      `MembershipWriteResult`/`MembershipWriteOutcome`/`AcceptedInvite`
      em `Common/Interfaces/IMembershipRepository.cs` (ver assinatura
      completa no `plan.md`, seção 1).
- [x] 5. Criar `MembershipErrors`
      (`Members/MembershipErrors.cs`): `NotFound` (404),
      `AlreadyExists` (409 `member-already-exists`),
      `CannotModifyTitular`/`CannotRemoveTitular` (422),
      `InsufficientPermission` (403 `insufficient-permission`).
- [x] 6. Criar `MemberResult`/`GetMembersResult`
      (`Members/MemberResult.cs`) com `MemberResult.FromEntity(Membership)`.
- [x] 7. Atualizar `EnsureAccountCommand`
      (`Accounts/Commands/EnsureAccount/EnsureAccountCommand.cs`): novo
      parâmetro `Email`; handler repassa pra
      `_accountRepository.CreateAsync(command.UserId, command.Email, ct)`.
- [x] 8. Remover `Accounts/Queries/ResolveAccountId/ResolveAccountIdQuery.cs`
      e criar `Members/Queries/ResolveMembership/ResolveMembershipQuery.cs`
      (`ResolveMembershipQuery(UserId)` →
      `Result<ResolveMembershipResult(AccountId, MembershipId, Role)>`):
      resolve `accountId` via `IAccountRepository`
      (`AccountErrors.NotResolved` se não achar), depois o `Membership`
      do próprio chamador via
      `IMembershipRepository.FindByAccountAndUserIdAsync` (mesmo erro
      se não achar).
- [x] 9. Criar `AcceptPendingInvitesCommand` + Handler
      (`Members/Commands/AcceptPendingInvites/`): chama
      `IMembershipRepository.AcceptPendingInvitesByEmailAsync(email, userId)`;
      se vazio, no-op; senão escolhe o `AccountId` de `CreatedAt` mais
      alto e chama `IAccountRepository.SetActiveAccountAsync`.
- [x] 10. Atualizar `LoginUserCommandHandler`
      (`Auth/Commands/Login/LoginUserCommand.cs`): repassar
      `command.Email` pro `EnsureAccountCommand`; acrescentar um
      segundo bloco `try/catch` despachando
      `AcceptPendingInvitesCommand(result.Value.UserId, command.Email)`
      logo depois, também sem propagar falha.
- [x] 11. Criar `InviteMemberCommand` + Handler +
      `InviteMemberCommandValidator` (FluentValidation: `Email`
      obrigatório + formato; `Role` obrigatório, restrito a
      `Leitura`/`Lancar`/`Total`) em `Members/Commands/InviteMember/`.
- [x] 12. Criar `GetMembersQuery` + Handler em
      `Members/Queries/GetMembers/` (lista via
      `IMembershipRepository.ListAsync`, mapeia com
      `MemberResult.FromEntity`).
- [x] 13. Criar `UpdateMemberRoleCommand` + Handler +
      `UpdateMemberRoleCommandValidator` em
      `Members/Commands/UpdateMemberRole/`: busca o membro
      (`GetByIdAsync` → 404 se não achar), bloqueia `Role == Titular`
      (422 `CannotModifyTitular`), senão `UpdateRoleAsync`.
- [x] 14. Criar `RemoveMemberCommand` + Handler em
      `Members/Commands/RemoveMember/`: mesmo fluxo de busca prévia,
      bloqueia `Role == Titular` (422 `CannotRemoveTitular`), senão
      `DeleteAsync`.

## Infrastructure

- [x] 15. Criar `DynamoDbMembershipRepository`
      (`Infrastructure/Members/DynamoDbMembershipRepository.cs`):
      `ListAsync` (`Query PK=ACCOUNT#<accountId>`,
      `begins_with(SK,"MEMBER#")`), `GetByIdAsync` (`GetItem
      PK=ACCOUNT#<accountId>, SK=MEMBER#<id>`),
      `FindByAccountAndUserIdAsync` (`Query GSI1`,
      `GSI1PK=USER#<userId> AND GSI1SK=ACCOUNT#<accountId>`),
      `CreateInviteAsync` (checa duplicidade de e-mail via `ListAsync`
      em memória, `PutItem` condicional se livre),
      `UpdateRoleAsync`/`DeleteAsync` (`UpdateItem`/`DeleteItem`
      condicionais, `NotFound` se `ConditionalCheckFailed`),
      `AcceptPendingInvitesByEmailAsync` (`Query GSI1,
      GSI1PK=EMAIL#<normalizado>`, `UpdateItem` por item encontrado
      setando `Status=Ativo`/`UserId`/`GSI1PK=USER#<userId>`). Ver
      `plan.md` seção 2 pro modelo de dados completo.
- [x] 16. Atualizar `DynamoDbAccountRepository`
      (`Infrastructure/Accounts/DynamoDbAccountRepository.cs`):
      `CreateAsync` ganha parâmetro `email`, o item de `Membership` da
      transação passa a gerar `Id` próprio (`SK=MEMBER#<id>`, era
      `MEMBER#<userId>`), grava `Email` e `Status=Ativo`. Novo método
      `SetActiveAccountAsync` (`PutItem` incondicional sobrescrevendo o
      `AccountPointer`).
- [x] 17. Registrar `IMembershipRepository → DynamoDbMembershipRepository`
      em `InfrastructureServiceCollectionExtensions.cs`.

## Api

- [x] 18. Estender `CurrentAccountContext`
      (`Api/Common/CurrentAccountContext.cs`): `MembershipRole? Role`,
      `string? MembershipId`.
- [x] 19. Atualizar `ResolveAccountEndpointFilter`
      (`Api/Common/ResolveAccountEndpointFilter.cs`): despachar
      `ResolveMembershipQuery` (em vez de `ResolveAccountIdQuery`) e
      preencher `AccountId`/`Role`/`MembershipId` no
      `CurrentAccountContext`.
- [x] 20. Criar `RoleEndpointFilters.Require(params MembershipRole[])`
      (`Api/Common/RoleEndpointFilters.cs`) — delegate factory que lê
      `CurrentAccountContext.Role` via `HttpContext.RequestServices` e
      retorna 403 (`MembershipErrors.InsufficientPermission`) se o
      papel não estiver na lista permitida.
- [x] 21. Atualizar `ResultHttpExtensions.BuildProblem`
      (`Api/Common/ResultHttpExtensions.cs`): novo caso
      `ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Acesso
      negado", error.Message)`.
- [x] 22. Atualizar `CategoryEndpoints.cs`: `MapPost`/`MapPut`/`MapDelete`
      ganham `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total,
      MembershipRole.Titular))` + `.ProducesProblem(StatusCodes.Status403Forbidden)`.
- [x] 23. Atualizar `ExpenseEndpoints.cs`: `MapPost` ganha
      `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar,
      MembershipRole.Total, MembershipRole.Titular))`;
      `MapPut`/`MapDelete` ganham
      `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total,
      MembershipRole.Titular))`; todos com
      `.ProducesProblem(StatusCodes.Status403Forbidden)`.
- [x] 24. Criar `MemberEndpoints.cs` (`MapGroup("/members")`,
      `.RequireAuthorization().AddEndpointFilter<ResolveAccountEndpointFilter>()`):
      `GetMembers` (sem filtro de papel extra), `InviteMember`/
      `UpdateMemberRole`/`RemoveMember` com
      `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))`.
      Records `InviteMemberRequest(Email, Role)`/
      `UpdateMemberRoleRequest(Role)` no fim do arquivo, mesmo padrão
      de `CategoryEndpoints.cs`.
- [x] 25. Registrar `app.MapMemberEndpoints()` em `Program.cs`.
- [x] 26. Atualizar `AppJsonSerializerContext.cs`: novos
      `[JsonSerializable]` para `InviteMemberRequest`,
      `UpdateMemberRoleRequest`, `MemberResult`, `GetMembersResult`.

## GastosApp.CognitoTriggers

- [x] 27. Atualizar `AccountTriggerHandler.HandleAsync`: extrair
      `email` de `evt.Request.UserAttributes` além de `sub`; despachar
      `EnsureAccountCommand(userId, email)`; se `email` vier
      ausente/vazio, logar e não despachar nada (postura defensiva,
      não deveria ocorrer em uso normal).

## Testes — Domain/Application (UnitTests)

- [x] 28. Reescrever `UnitTests/Domain/MembershipTests.cs` pros novos
      factory methods (`CreateTitular`/`CreateInvite`/`Restore` com
      `Id`/`Email`/`Status`).
- [x] 29. Atualizar `ResultHttpExtensionsTests.cs`: novo caso
      `ErrorType.Forbidden` → 403 com título "Acesso negado".
- [x] 30. Atualizar `EnsureAccountCommandHandlerTests.cs`: cenários
      passam a exigir `Email` no comando e verificam repasse pro
      `CreateAsync`.
- [x] 31. Criar `ResolveMembershipQueryHandlerTests.cs` (substitui
      `ResolveAccountIdQueryHandlerTests.cs`): sucesso retorna
      `AccountId`/`MembershipId`/`Role`; `AccountErrors.NotResolved`
      quando a conta não resolve; mesmo erro quando a conta resolve mas
      não há `Membership` do usuário nela (inconsistência de dado).
- [x] 32. Criar `InviteMemberCommandHandlerTests.cs`: sucesso retorna
      `MemberResult` com `Status=ConvitePendente`; e-mail já membro
      retorna `MembershipErrors.AlreadyExists` (409).
- [x] 33. Criar `InviteMemberCommandValidatorTests.cs`: `email`
      ausente/formato inválido, `role` ausente/fora do conjunto
      permitido.
- [x] 34. Criar `GetMembersQueryHandlerTests.cs`: lista vazia; lista
      com Titular + membros pendentes/ativos mapeados corretamente.
- [x] 35. Criar `UpdateMemberRoleCommandHandlerTests.cs`: sucesso;
      `id` inexistente → 404; alvo é o Titular → 422
      `CannotModifyTitular`.
- [x] 36. Criar `RemoveMemberCommandHandlerTests.cs`: sucesso; `id`
      inexistente → 404; alvo é o Titular → 422 `CannotRemoveTitular`.
- [x] 37. Criar `AcceptPendingInvitesCommandHandlerTests.cs`: nenhum
      convite pendente → no-op, `SetActiveAccountAsync` não chamado;
      um convite pendente → troca a conta ativa pra ele; convites em
      duas contas diferentes → escolhe o de `CreatedAt` mais recente.
- [x] 38. Atualizar `LoginUserCommandHandlerTests.cs`: login
      bem-sucedido despacha `AcceptPendingInvitesCommand` com o e-mail
      do comando; exceção nesse despacho é capturada e o login
      continua retornando sucesso.

## Testes — Infrastructure (UnitTests)

- [x] 39. Criar `DynamoDbMembershipRepositoryTests.cs`: `ListAsync`,
      `GetByIdAsync` (achou/não achou), `FindByAccountAndUserIdAsync`
      (achou/não achou), `CreateInviteAsync` (sucesso e
      `EmailConflict`), `UpdateRoleAsync`/`DeleteAsync`
      (sucesso e `NotFound`), `AcceptPendingInvitesByEmailAsync` (0, 1
      e N contas diferentes).
- [x] 40. Atualizar `DynamoDbAccountRepositoryTests.cs`: `CreateAsync`
      agora recebe `email` e o item de `Membership` gerado reflete
      `Id`/`SK=MEMBER#<id>`/`Email`/`Status=Ativo`; novo teste de
      `SetActiveAccountAsync` (sobrescreve o `AccountPointer`
      existente).
- [x] 41. Atualizar `UnitTests/CognitoTriggers/AccountTriggerHandlerTests.cs`:
      evento com `sub`+`email` despacha `EnsureAccountCommand(userId,
      email)`; evento sem `email` não despacha nada e ainda retorna o
      evento (loga o motivo).

## Testes — ComponentTests

- [x] 42. Adicionar `MembershipRepositoryMock`
      (+ `ResetMembershipRepositoryMock`) em
      `ComponentTestWebApplicationFactory.cs`. Mock padrão resolve
      `FindByAccountAndUserIdAsync(qualquer, qualquer)` retornando um
      `Membership` com `Role=Titular`, pra não quebrar os testes de
      `Category`/`Expense` já existentes (mesmo motivo documentado no
      `plan.md`, seção "Testes").
- [x] 43. Criar `ComponentTests/Members/MemberEndpointsTests.cs`
      cobrindo a matriz completa da spec: `POST /members` (201, 400,
      403, 409), `GET /members` (200, qualquer papel), `PUT
      /members/{id}` (200, 400, 403, 404, 422 no Titular), `DELETE
      /members/{id}` (204, 403, 404, 422 no Titular).
- [x] 44. Atualizar `ComponentTests/Categories/CategoryEndpointsTests.cs`:
      novos casos 403 pra `Leitura`/`Lancar` em `POST`/`PUT`/`DELETE
      /categories`.
- [x] 45. Atualizar `ComponentTests/Expenses/ExpenseEndpointsTests.cs`:
      novos casos 403 pra `Leitura` em `POST /expenses`, e pra
      `Leitura`/`Lancar` em `PUT`/`DELETE /expenses`.
- [x] 46. Atualizar `ComponentTests/Auth/AuthEndpointsTests.cs`: ajustar
      chamadas ao `AccountRepositoryMock.CreateAsync` pra nova
      assinatura (`email`); novo caso cobrindo login que aceita convite
      pendente (via `MembershipRepositoryMock`) e troca a conta ativa
      (verifica chamada a `AccountRepositoryMock.SetActiveAccountAsync`).

## Fechamento

- [x] 47. Rodar `./scripts/export-openapi.sh` e conferir
      `backend/docs/openapi.json`: os 3 endpoints novos de `/members`
      e os novos status `403`/`409`/`422` aparecendo nos endpoints já
      existentes de `/categories`/`/expenses`.
- [x] 48. Rodar a suíte completa (`dotnet test GastosApp.sln`) e
      confirmar 100% dos testes passando
      (`[[feedback_tests_must_pass]]`).
- [x] 49. Atualizar `spec.md`: marcar os critérios de aceite concluídos
      (`- [x]`) e adicionar a seção "Status" (mesmo padrão de
      `backend/specs/FEAT-19-conta-multi-tenant/spec.md`) resumindo o
      que foi implementado.
