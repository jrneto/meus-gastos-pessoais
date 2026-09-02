# Tasks: FEAT-35 — Confirmação de cadastro via código (OTP)

- [x] 1. Adicionar `ConfirmSignUpAsync(string email, string code, CancellationToken)` e `ResendConfirmationCodeAsync(string email, CancellationToken)` (ambos retornando `Task<Result>`) à interface `IAuthService` (`backend/src/GastosApp.Application/Common/Interfaces/IAuthService.cs`)

- [x] 2. Adicionar `AuthErrors.InvalidConfirmationCode` (`Error.Validation("invalid-confirmation-code", "Código de confirmação inválido.")`) e `AuthErrors.ExpiredConfirmationCode` (`Error.Validation("expired-confirmation-code", "Código de confirmação expirado.")`) em `backend/src/GastosApp.Application/Auth/AuthErrors.cs`

- [x] 3. Implementar `ConfirmSignUpAsync` em `CognitoAuthService` (`backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs`) — chama `_cognitoClient.ConfirmSignUpAsync`; mapeia `ExpiredCodeException` → `ExpiredConfirmationCode`, `CodeMismatchException`/`UserNotFoundException` → `InvalidConfirmationCode`, `NotAuthorizedException` → `Result.Success()` (idempotência, usuário já confirmado)

- [x] 4. Implementar `ResendConfirmationCodeAsync` em `CognitoAuthService` — chama `_cognitoClient.ResendConfirmationCodeAsync`; absorve `UserNotFoundException` e `InvalidParameterException` sem erro; sempre retorna `Result.Success()` (qualquer outra exceção propaga)

- [x] 5. Criar `backend/src/GastosApp.Application/Auth/Commands/Confirm/ConfirmSignUpCommand.cs` com `ConfirmSignUpCommand(string Email, string Code) : ICommand<Result>` e `ConfirmSignUpCommandHandler` (repasse direto a `IAuthService.ConfirmSignUpAsync`)

- [x] 6. Adicionar `ConfirmSignUpCommandValidator` (mesmo arquivo ou arquivo próprio na mesma pasta) — `RuleFor(c => c.Email).NotEmpty()` e `RuleFor(c => c.Code).NotEmpty()`

- [x] 7. Criar `backend/src/GastosApp.Application/Auth/Commands/ResendConfirmation/ResendConfirmationCodeCommand.cs` com `ResendConfirmationCodeCommand(string Email) : ICommand<Result>` e `ResendConfirmationCodeCommandHandler` (repasse direto a `IAuthService.ResendConfirmationCodeAsync`)

- [x] 8. Adicionar `ResendConfirmationCodeCommandValidator` (mesmo arquivo ou arquivo próprio na mesma pasta) — `RuleFor(c => c.Email).NotEmpty()`

- [x] 9. Adicionar `POST /auth/confirm` em `AuthEndpoints.MapAuthEndpoints` (`backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs`) — handler `ConfirmSignUp` (envia `ConfirmSignUpCommand`, `result.ToHttpResult(Results.Ok)`), record `ConfirmRequest(string Email, string Code)`, `.Produces(StatusCodes.Status200OK)` + `.ProducesProblem(StatusCodes.Status400BadRequest)`

- [x] 10. Adicionar `POST /auth/resend-confirmation` em `AuthEndpoints.MapAuthEndpoints` — handler `ResendConfirmation` (envia `ResendConfirmationCodeCommand`, `result.ToHttpResult(Results.Ok)`), record `ResendConfirmationRequest(string Email)`, `.Produces(StatusCodes.Status200OK)` + `.ProducesProblem(StatusCodes.Status400BadRequest)`

- [x] 11. Adicionar `[JsonSerializable(typeof(ConfirmRequest))]` e `[JsonSerializable(typeof(ResendConfirmationRequest))]` em `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`

- [x] 12. Rodar `dotnet build backend/GastosApp.sln` e confirmar que compila sem erro (Native AOT trim warnings incluídos)

- [x] 13. Adicionar `ConfirmSignUpAsync_ShouldSucceed_WhenCognitoCallSucceeds` em `backend/tests/GastosApp.UnitTests/Infrastructure/CognitoAuthServiceTests.cs`

- [x] 14. Adicionar `ConfirmSignUpAsync_ShouldReturnExpiredConfirmationCode_WhenCognitoThrowsExpiredCodeException` (mesmo arquivo)

- [x] 15. Adicionar `ConfirmSignUpAsync_ShouldReturnInvalidConfirmationCode_WhenCognitoThrowsCodeMismatchException` (mesmo arquivo)

- [x] 16. Adicionar `ConfirmSignUpAsync_ShouldReturnInvalidConfirmationCode_WhenCognitoThrowsUserNotFoundException` (mesmo arquivo)

- [x] 17. Adicionar `ConfirmSignUpAsync_ShouldSucceed_WhenCognitoThrowsNotAuthorizedException` (mesmo arquivo) — cobre a idempotência de usuário já confirmado

- [x] 18. Adicionar `ResendConfirmationCodeAsync_ShouldSucceed_WhenCognitoCallSucceeds` (mesmo arquivo)

- [x] 19. Adicionar `ResendConfirmationCodeAsync_ShouldSucceed_WhenCognitoThrowsUserNotFoundException` (mesmo arquivo)

- [x] 20. Adicionar `ResendConfirmationCodeAsync_ShouldSucceed_WhenCognitoThrowsInvalidParameterException` (mesmo arquivo) — valida a suposição documentada no plan.md (decisão técnica 4/ponto de confirmação 1); se o Cognito real lançar outro tipo de exceção, ajustar o `catch` da task 4 e este teste juntos

- [x] 21. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~CognitoAuthServiceTests` e confirmar tudo passando

- [x] 22. Adicionar `Confirm_ComCodigoCorreto_Retorna200SemCorpo` em `backend/tests/GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs`

- [x] 23. Adicionar `Confirm_ComParametrosInvalidos_Retorna400SemChamarAuthService` (Theory: email vazio, code vazio) — confirma `validation-error` e que `AuthServiceMock.ConfirmSignUpAsync` nunca é chamado

- [x] 24. Adicionar `Confirm_QuandoAuthServiceRetornaErro_PropagaProblemDetails` (Theory: `AuthErrors.InvalidConfirmationCode` → 400 `invalid-confirmation-code`, `AuthErrors.ExpiredConfirmationCode` → 400 `expired-confirmation-code`)

- [x] 25. Adicionar `ResendConfirmation_ComEmailValido_Retorna200SemCorpo` (mesmo arquivo)

- [x] 26. Adicionar `ResendConfirmation_ComEmailVazio_Retorna400SemChamarAuthService` (mesmo arquivo)

- [x] 27. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~AuthEndpointsTests` e confirmar tudo passando

- [x] 28. Adicionar `Confirm_UsuarioJaConfirmado_Retorna200Idempotente` em `backend/tests/GastosApp.IntegrationTests/Auth/AuthFlowTests.cs` — reusa `TestAccountFixture.CreateAsync()` (já confirmado), chama `POST /auth/confirm` com código qualquer, espera 200

- [x] 29. Adicionar `Confirm_CodigoIncorreto_Retorna400` (mesmo arquivo) — **divergiu da redação original** ("mesma fixture, código inválido"): a fixture já confirmada sempre cai no branch de idempotência (200) independente do código, por definição (spec.md US5, "qualquer code") — reusá-la aqui nunca produziria 400. Implementado como spec.md US2 realmente descreve (usuário NÃO confirmado): registra conta nova sem confirmar, chama `/auth/confirm` com código errado, espera 400 `invalid-confirmation-code`; limpeza manual via `AdminDeleteUserAsync` em `finally` (mesmo padrão da task 31)

- [x] 30. Adicionar `Confirm_EmailInexistente_Retorna400` (mesmo arquivo) — sem fixture, email inexistente, espera 400 `invalid-confirmation-code`

- [x] 31. Adicionar `ResendConfirmation_UsuarioNaoConfirmado_Retorna200` (mesmo arquivo) — registra conta nova via `POST /auth/register` sem confirmar (sem `TestAccountFixture` completa), chama `POST /auth/resend-confirmation`, espera 200; limpeza manual do usuário Cognito criado (`AdminDeleteUserAsync`) em `finally`

- [x] 32. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unit + componente) sem regressão

- [x] 33. Rodar `backend/infra/lambda/run-local.sh` (binário Native AOT via Runtime Interface Emulator) e os testes integrados relevantes (`AuthFlowTests`, `--filter Category=Integration`) localmente, confirmando que passam (constitution: feature só é concluída com testes integrados relevantes rodando localmente)

- [x] 34. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que `backend/docs/openapi.json` mudou só para incluir `POST /auth/confirm` e `POST /auth/resend-confirmation`

- [ ] 35. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-35-confirmacao-cadastro-otp/spec.md` e preencher uma seção "Status", resumindo o que foi implementado (incluir a confirmação empírica da suposição da task 20/ponto 4 do plan.md, e o escopo real do teste integrado de sucesso conforme decisão técnica 5)

- [ ] 36. Atualizar `backend/docs/backlog.md` — mover/marcar o item da FEAT-35 como concluído, seguindo a convenção já usada para features anteriores (ver commit da FEAT-34)
