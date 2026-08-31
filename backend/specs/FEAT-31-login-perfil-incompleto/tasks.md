# Tasks: FEAT-31 — Login bloqueado quando o perfil está incompleto

- [x] 1. Adicionar `AuthErrors.ProfileIncomplete` em `backend/src/GastosApp.Application/Auth/AuthErrors.cs` — `Error.Forbidden("profile-incomplete", "Cadastro incompleto. Este usuário não possui perfil (nome, telefone e CPF) cadastrado.")`

- [x] 2. Atualizar `LoginUserCommandHandler` (`backend/src/GastosApp.Application/Auth/Commands/Login/LoginUserCommand.cs`) — injetar `IUserProfileRepository` no construtor e, depois de `_authService.LoginAsync` ter sucesso e antes de `EnsureAccountCommand`/`AcceptPendingInvitesCommand`, chamar `FindByUserIdAsync(result.Value.UserId, cancellationToken)`; se retornar `null`, `return Result.Failure<LoginUserResult>(AuthErrors.ProfileIncomplete)`

- [x] 3. Atualizar `AuthEndpoints.MapAuthEndpoints` (`backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs`) — adicionar `.ProducesProblem(StatusCodes.Status403Forbidden)` na definição de `POST /login`

- [x] 4. Atualizar `LoginUserCommandHandlerTests` (`backend/tests/GastosApp.UnitTests/Application/LoginUserCommandHandlerTests.cs`) — adicionar campo `_userProfileRepositoryMock` (`IUserProfileRepository`), passar ao `_handler` no construtor, e configurar `FindByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())` para retornar um `UserProfile` completo por padrão (ex.: `UserProfile.Restore("user-id-123", "Fulano da Silva", "11999998888", "11144477735", DateTimeOffset.UtcNow)`), preservando os 9 testes já existentes sem editá-los

- [x] 5. Adicionar `Handle_ShouldReturnForbiddenFailure_WhenProfileDoesNotExist` (mesmo arquivo) — `FindByUserIdAsync` retorna `null`; espera `result.IsFailure`, `Error.Type == ErrorType.Forbidden`, `Error.Code == "profile-incomplete"`

- [x] 6. Adicionar `Handle_ShouldNotDispatchEnsureAccountCommand_WhenProfileIsIncomplete` (mesmo arquivo) — mesmo arranjo da task 5; `_senderMock.DidNotReceiveWithAnyArgs().Send(Arg.Any<EnsureAccountCommand>(), ...)`

- [x] 7. Adicionar `Handle_ShouldNotDispatchAcceptPendingInvitesCommand_WhenProfileIsIncomplete` (mesmo arquivo) — mesmo arranjo da task 5; `_senderMock.DidNotReceiveWithAnyArgs().Send(Arg.Any<AcceptPendingInvitesCommand>(), ...)`

- [x] 8. Adicionar `Handle_ShouldCheckProfile_OnlyAfterCredentialsAreValidated` (mesmo arquivo) — credenciais inválidas (`AuthErrors.InvalidCredentials`); espera `result.Error.Code == "invalid-credentials"` e `_userProfileRepositoryMock.DidNotReceiveWithAnyArgs().FindByUserIdAsync(default!, default)`

- [x] 9. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~LoginUserCommandHandlerTests` e confirmar tudo passando

- [x] 10. Atualizar `BuildDefaultUserProfileRepositoryMock` em `ComponentTestWebApplicationFactory.cs` (`backend/tests/GastosApp.ComponentTests/Support/`) — `FindByUserIdAsync` passa a retornar, por padrão, um `UserProfile` completo (ex.: `UserProfile.Restore(callInfo.Arg<string>(), "Fulano da Silva", "11999998888", "11144477735", DateTimeOffset.UtcNow)`) em vez de `null`; atualizar o comentário do método (linhas ~61-66), que hoje descreve o default oposto

- [x] 11. Atualizar `Me_SemPerfilCadastrado_Retorna200ComCamposNulos` (`backend/tests/GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs`) — configurar explicitamente `_factory.UserProfileRepositoryMock.FindByUserIdAsync("uuid-123", Arg.Any<CancellationToken>()).Returns((UserProfile?)null)`, já que o default deixou de ser `null` (task 10); ajustar o comentário do teste

- [x] 12. Adicionar `Login_ComUsuarioSemPerfil_Retorna403ComProfileIncomplete` (mesmo arquivo) — `AuthServiceMock.LoginAsync` sucesso + `UserProfileRepositoryMock.FindByUserIdAsync` retornando `null`; espera `403`, `problem.type == "https://gastosapp.dev/errors/profile-incomplete"`, e ausência de header `Set-Cookie`

- [x] 13. Adicionar `Login_ComUsuarioSemPerfil_NaoCriaAccountNemAceitaConvite` (mesmo arquivo) — mesmo arranjo da task 12; `AccountRepositoryMock.DidNotReceiveWithAnyArgs().FindAccountIdByUserIdAsync(...)`/`.CreateAsync(...)` e `MembershipRepositoryMock.DidNotReceiveWithAnyArgs().AcceptPendingInvitesByEmailAsync(...)`

- [x] 14. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unit + componente) — sem regressão, especialmente nos demais testes de `Login_*`/`Register_*`/`Me_*` que dependem do default de `UserProfileRepositoryMock`

- [x] 15. Rodar `backend/infra/lambda/run-local.sh` (binário Native AOT via Runtime Interface Emulator) e o teste integrado relevante (`AuthFlowTests`, `--filter Category=Integration`) localmente, confirmando que o fluxo normal de registro+login continua passando (constitution: feature só é concluída com testes integrados relevantes rodando localmente)

- [x] 16. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que `backend/docs/openapi.json` mudou só para incluir `403` em `POST /auth/login`

- [x] 17. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-31-login-perfil-incompleto/spec.md` e preencher uma seção "Status", resumindo o que foi implementado (inclui registrar a troca do "default esperto" do mock de perfil nos testes de componente/unitário)

- [x] 18. Confirmar que a entrada do bug em `backend/docs/backlog.md` (seção "Bugs") já aponta para esta FEAT como resolvida (feito no `/specify`) — sem ação adicional se já estiver correta
