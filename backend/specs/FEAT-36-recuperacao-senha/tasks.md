# Tasks: FEAT-36 — Recuperação de senha

- [ ] 1. Adicionar `ForgotPasswordAsync(string email, CancellationToken)` e `ConfirmForgotPasswordAsync(string email, string code, string newPassword, CancellationToken)` (ambos retornando `Task<Result>`) à interface `IAuthService` (`backend/src/GastosApp.Application/Common/Interfaces/IAuthService.cs`)

- [ ] 2. Adicionar `AuthErrors.InvalidResetCode` (`Error.Validation("invalid-reset-code", "Código de recuperação inválido.")`) e `AuthErrors.ExpiredResetCode` (`Error.Validation("expired-reset-code", "Código de recuperação expirado.")`) em `backend/src/GastosApp.Application/Auth/AuthErrors.cs`

- [ ] 3. Criar `IEmailSender` (`backend/src/GastosApp.Application/Common/Interfaces/IEmailSender.cs`) — `Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default)`

- [ ] 4. Criar `IPasswordChangedEmailSender` (`backend/src/GastosApp.Application/Common/Interfaces/IPasswordChangedEmailSender.cs`) — `Task SendAsync(string email, string? userAgent, CancellationToken cancellationToken = default)`

- [ ] 5. Implementar `ForgotPasswordAsync` em `CognitoAuthService` (`backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs`) — chama `_cognitoClient.ForgotPasswordAsync`; absorve `UserNotFoundException` e `InvalidParameterException` sem erro; sempre retorna `Result.Success()` (qualquer outra exceção propaga)

- [ ] 6. Implementar `ConfirmForgotPasswordAsync` em `CognitoAuthService` — chama `_cognitoClient.ConfirmForgotPasswordAsync`; mapeia `ExpiredCodeException` → `ExpiredResetCode`, `CodeMismatchException`/`UserNotFoundException` → `InvalidResetCode`, `InvalidPasswordException` → `AuthErrors.Validation("Senha deve ter no mínimo 8 caracteres, com letra maiúscula, minúscula, número e símbolo.")`

- [ ] 7. Criar `backend/src/GastosApp.Application/Auth/Commands/ForgotPassword/ForgotPasswordCommand.cs` com `ForgotPasswordCommand(string Email) : ICommand<Result>` e `ForgotPasswordCommandHandler` (repasse direto a `IAuthService.ForgotPasswordAsync`)

- [ ] 8. Adicionar `ForgotPasswordCommandValidator` (mesmo arquivo ou arquivo próprio na mesma pasta) — `RuleFor(c => c.Email).NotEmpty()`

- [ ] 9. Criar `backend/src/GastosApp.Application/Auth/Commands/ResetPassword/ResetPasswordCommand.cs` com `ResetPasswordCommand(string Email, string Code, string NewPassword, string? UserAgent) : ICommand<Result>` e `ResetPasswordCommandHandler` — chama `IAuthService.ConfirmForgotPasswordAsync`; se sucesso, chama `IPasswordChangedEmailSender.SendAsync` dentro de `try/catch` que só loga (`ILogger<ResetPasswordCommandHandler>`) e nunca propaga; retorna sempre `Result.Success()` quando a troca de senha deu certo, independente do envio do email

- [ ] 10. Adicionar `ResetPasswordCommandValidator` (mesmo arquivo ou arquivo próprio na mesma pasta, `ClassLevelCascadeMode = CascadeMode.Stop`) — `RuleFor(c => c.Email).NotEmpty()`, `RuleFor(c => c.Code).NotEmpty()`, `RuleFor(c => c.NewPassword).NotEmpty()` (sem `MinimumLength`/regra de política — decisão técnica 4 do `plan.md`)

- [ ] 11. Registrar `IValidator<ForgotPasswordCommand>` e `IValidator<ResetPasswordCommand>` em `ApplicationServiceCollectionExtensions` (`backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`)

- [ ] 12. Criar `SesOptions` (`backend/src/GastosApp.Infrastructure/Configuration/SesOptions.cs`) — `SectionName = "Ses"`, `SenderEmail`

- [ ] 13. Copiar `frontend/design-system/emails/03-senha-alterada.html` para `backend/src/GastosApp.Infrastructure/Email/Templates/03-senha-alterada.html`, já com o texto ajustado (decisão 4 do `spec.md` — ver task 22), e marcar como `EmbeddedResource` em `GastosApp.Infrastructure.csproj`

- [ ] 14. Criar `PasswordChangedEmailTemplateProvider` (`backend/src/GastosApp.Infrastructure/Email/PasswordChangedEmailTemplateProvider.cs`) — carrega o template embarcado da task 13 uma vez no cold start, mesmo padrão de `EmailTemplateProvider` (`GastosApp.CognitoTriggers.CustomMessage`)

- [ ] 15. Adicionar `PackageReference` `AWSSDK.SimpleEmailV2` (versão estável mais recente) em `backend/src/GastosApp.Infrastructure/GastosApp.Infrastructure.csproj`

- [ ] 16. Criar `SesEmailService` (`backend/src/GastosApp.Infrastructure/Email/SesEmailService.cs`) implementando `IEmailSender` via `IAmazonSimpleEmailServiceV2.SendEmailAsync`

- [ ] 17. Criar `SesPasswordChangedEmailSender` (`backend/src/GastosApp.Infrastructure/Email/SesPasswordChangedEmailSender.cs`) implementando `IPasswordChangedEmailSender` — monta o assunto fixo, substitui `{{email}}`/`{{data}}` (`dd/MM/yyyy HH:mm` + sufixo `"UTC"` literal)/`{{dispositivo}}` (`User-Agent` cru, `"Desconhecido"` se nulo/vazio) no template da task 14, chama `IEmailSender.SendAsync`

- [ ] 18. Criar `InfraEmailExtensions.AddSesSdk` (`backend/src/GastosApp.Infrastructure/Extensions/InfraEmailExtensions.cs`) — leitura manual de `IConfiguration` (sem `Configure<T>()`/reflection, AOT-safe) para `SesOptions`; registra `IAmazonSimpleEmailServiceV2` (reaproveitando a região de `CognitoOptions.Region`), `IEmailSender` → `SesEmailService`, `IPasswordChangedEmailSender` → `SesPasswordChangedEmailSender`

- [ ] 19. Chamar `services.AddSesSdk(configuration)` em `AddAwsInfrastructure` (`backend/src/GastosApp.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`)

- [ ] 20. Adicionar `POST /auth/forgot-password` em `AuthEndpoints.MapAuthEndpoints` (`backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs`) — handler `ForgotPassword` (envia `ForgotPasswordCommand`, `result.ToHttpResult(Results.Ok)`), record `ForgotPasswordRequest(string Email)`, `.Produces(StatusCodes.Status200OK)` + `.ProducesProblem(StatusCodes.Status400BadRequest)`

- [ ] 21. Adicionar `POST /auth/reset-password` em `AuthEndpoints.MapAuthEndpoints` — handler `ResetPassword` (extrai `User-Agent` de `HttpContext.Request.Headers`, envia `ResetPasswordCommand`, `result.ToHttpResult(Results.Ok)`), record `ResetPasswordRequest(string Email, string Code, string NewPassword)`, `.Produces(StatusCodes.Status200OK)` + `.ProducesProblem(StatusCodes.Status400BadRequest)`

- [ ] 22. Adicionar `[JsonSerializable(typeof(ForgotPasswordRequest))]` e `[JsonSerializable(typeof(ResetPasswordRequest))]` em `backend/src/GastosApp.Api/Common/AppJsonSerializerContext.cs`

- [ ] 23. Ajustar `frontend/design-system/emails/03-senha-alterada.html` — trocar `"Olá, {{nome}}. A senha da conta {{email}} foi redefinida com sucesso."` por `"A senha da conta {{email}} foi redefinida com sucesso."` (decisão 4 do `spec.md`); refletir a mesma mudança na cópia da task 13, que precisa nascer já com o texto final

- [ ] 24. Adicionar recurso `aws_ssm_parameter.ses_sender_email` (`/GastosApp/Ses/SenderEmail`, tipo `String`, valor `aws_cognito_user_pool.main.email_configuration[0].from_email_address`) em `backend/infra/terraform/environments/prod/parameter-store.tf`

- [ ] 25. Adicionar o mesmo recurso (`/GastosApp/Hom/Ses/SenderEmail`) em `backend/infra/terraform/environments/hom/parameter-store.tf`

- [ ] 26. Rodar `terraform fmt`/`terraform validate` (ou `terraform plan`, sem aplicar) nos dois ambientes para confirmar que os 2 parâmetros novos são a única mudança — aplicação em si segue o fluxo normal de deploy (fora do escopo desta task)

- [ ] 27. Rodar `dotnet build backend/GastosApp.sln` e confirmar que compila sem erro (Native AOT trim warnings incluídos)

- [ ] 28. Adicionar `ForgotPasswordAsync_ShouldSucceed_WhenCognitoCallSucceeds` em `backend/tests/GastosApp.UnitTests/Infrastructure/CognitoAuthServiceTests.cs`

- [ ] 29. Adicionar `ForgotPasswordAsync_ShouldSucceed_WhenCognitoThrowsUserNotFoundException` (mesmo arquivo)

- [ ] 30. Adicionar `ForgotPasswordAsync_ShouldSucceed_WhenCognitoThrowsInvalidParameterException` (mesmo arquivo)

- [ ] 31. Adicionar `ConfirmForgotPasswordAsync_ShouldSucceed_WhenCognitoCallSucceeds` (mesmo arquivo)

- [ ] 32. Adicionar `ConfirmForgotPasswordAsync_ShouldReturnExpiredResetCode_WhenCognitoThrowsExpiredCodeException` (mesmo arquivo)

- [ ] 33. Adicionar `ConfirmForgotPasswordAsync_ShouldReturnInvalidResetCode_WhenCognitoThrowsCodeMismatchException` (mesmo arquivo)

- [ ] 34. Adicionar `ConfirmForgotPasswordAsync_ShouldReturnInvalidResetCode_WhenCognitoThrowsUserNotFoundException` (mesmo arquivo)

- [ ] 35. Adicionar `ConfirmForgotPasswordAsync_ShouldReturnValidationError_WhenCognitoThrowsInvalidPasswordException` (mesmo arquivo) — confirma a mensagem fixa (decisão técnica 3 do `plan.md`), não o `ex.Message` do SDK

- [ ] 36. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~CognitoAuthServiceTests` e confirmar tudo passando

- [ ] 37. Criar `backend/tests/GastosApp.UnitTests/Infrastructure/SesPasswordChangedEmailSenderTests.cs` com `SendAsync_ShouldCallEmailSender_WithSubjectAndFilledTemplate` (mock de `IEmailSender` via NSubstitute, confirma assunto e placeholders substituídos)

- [ ] 38. Adicionar `SendAsync_ShouldUseFallbackDevice_WhenUserAgentIsNullOrEmpty` (Theory: `null`, `""`) no mesmo arquivo

- [ ] 39. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~SesPasswordChangedEmailSenderTests` e confirmar tudo passando

- [ ] 40. Adicionar `ForgotPassword_ComEmailValido_Retorna200SemCorpo` em `backend/tests/GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs`

- [ ] 41. Adicionar `ForgotPassword_ComEmailVazio_Retorna400SemChamarAuthService` (mesmo arquivo)

- [ ] 42. Adicionar `ResetPassword_ComParametrosCorretos_Retorna200EEnviaEmail` (mesmo arquivo) — confirma 200 e que `IPasswordChangedEmailSender.SendAsync` foi chamado

- [ ] 43. Adicionar `ResetPassword_ComParametrosInvalidos_Retorna400SemChamarAuthService` (Theory: email vazio, code vazio, newPassword vazio) no mesmo arquivo

- [ ] 44. Adicionar `ResetPassword_QuandoAuthServiceRetornaErro_PropagaProblemDetails` (Theory: `AuthErrors.InvalidResetCode` → 400 `invalid-reset-code`, `AuthErrors.ExpiredResetCode` → 400 `expired-reset-code`, `AuthErrors.Validation(...)` → 400 `bad-request`) no mesmo arquivo

- [ ] 45. Adicionar `ResetPassword_QuandoEmailFalha_AindaAssimRetorna200` (mesmo arquivo) — mock de `IPasswordChangedEmailSender.SendAsync` lançando exceção, espera 200 mesmo assim

- [ ] 46. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~AuthEndpointsTests` e confirmar tudo passando

- [ ] 47. Adicionar `ForgotPassword_EmailDeContaExistente_Retorna200` em `backend/tests/GastosApp.IntegrationTests/Auth/AuthFlowTests.cs` — reusa `TestAccountFixture.CreateAsync()`, chama `POST /auth/forgot-password`, espera 200

- [ ] 48. Adicionar `ForgotPassword_EmailInexistente_Retorna200` (mesmo arquivo) — email inexistente, espera 200 igualmente

- [ ] 49. Adicionar `ResetPassword_CodigoIncorreto_Retorna400` (mesmo arquivo) — conta existente (`TestAccountFixture`), código claramente inválido, espera 400 `invalid-reset-code`

- [ ] 50. Adicionar `ResetPassword_EmailInexistente_Retorna400` (mesmo arquivo) — sem fixture, espera 400 `invalid-reset-code`

- [ ] 51. Investigar se `ResetPassword_SenhaForaDaPolitica_Retorna400` é viável na suíte de integração (ponto de confirmação 2 do `plan.md` — depende da ordem de validação interna do Cognito entre código e senha). Se viável, implementar (mesmo arquivo); se não, registrar a conclusão como comentário no arquivo de teste e deixar coberto só pelos testes unitário/componente (tasks 35 e 44)

- [ ] 52. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unit + componente) sem regressão

- [ ] 53. Rodar `backend/infra/lambda/run-local.sh` (binário Native AOT via Runtime Interface Emulator) e os testes integrados relevantes (`AuthFlowTests`, `--filter Category=Integration`) localmente, confirmando que passam (constitution: feature só é concluída com testes integrados relevantes rodando localmente)

- [ ] 54. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que `backend/docs/openapi.json` mudou só para incluir `POST /auth/forgot-password` e `POST /auth/reset-password`

- [ ] 55. Atualizar `backend/infra/CLAUDE.md` com uma nota sobre os 2 novos parâmetros `Ses/SenderEmail` no Parameter Store (mesmo padrão das seções já existentes de Cognito/CORS)

- [ ] 56. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-36-recuperacao-senha/spec.md` e preencher uma seção "Status", resumindo o que foi implementado (incluir o resultado da investigação da task 51 e a confirmação empírica de qualquer suposição do `plan.md` que precisou de ajuste)

- [ ] 57. Atualizar `backend/docs/backlog.md` — mover/marcar o item da FEAT-36 como concluído, seguindo a convenção já usada para features anteriores (ver commit da FEAT-35)
