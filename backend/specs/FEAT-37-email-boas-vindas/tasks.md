# Tasks: FEAT-37 — E-mail de boas-vindas

- [ ] 1. Corrigir domínio em `frontend/design-system/emails/04-boas-vindas.html` — trocar `jrnexpenses.com.br` por `jrnexpenses.com` nos 3 lugares (link "Começar agora", link "Gerenciar preferências", e-mail de suporte `suporte@jrnexpenses.com`)

- [ ] 2. Copiar o template corrigido (task 1) para `backend/src/GastosApp.Infrastructure/Email/Templates/04-boas-vindas.html` e marcar como `EmbeddedResource` em `GastosApp.Infrastructure.csproj` (mesmo `ItemGroup` de `03-senha-alterada.html`)

- [ ] 3. Criar `WelcomeEmailTemplateProvider` (`backend/src/GastosApp.Infrastructure/Email/WelcomeEmailTemplateProvider.cs`) — mesmo padrão de `PasswordChangedEmailTemplateProvider`, carregando `04-boas-vindas.html` via `GetManifestResourceStream` uma vez no cold start

- [ ] 4. Criar `IWelcomeEmailSender` (`backend/src/GastosApp.Application/Common/Interfaces/IWelcomeEmailSender.cs`) — `Task SendAsync(string userId, string email, CancellationToken cancellationToken = default)`

- [ ] 5. Criar `SesWelcomeEmailSender` (`backend/src/GastosApp.Infrastructure/Email/SesWelcomeEmailSender.cs`) implementando `IWelcomeEmailSender` — busca o perfil via `IUserProfileRepository.FindByUserIdAsync(userId)`; se `null`, lança `InvalidOperationException` (sem fallback textual, ver `plan.md` decisão técnica 3); senão substitui `{{nome}}`/`{{email}}` no template da task 3 e chama `IEmailSender.SendAsync` com assunto fixo `"Bem-vindo ao jrn.expenses"` (igual ao `<title>` do template)

- [ ] 6. Registrar `services.AddScoped<IWelcomeEmailSender, SesWelcomeEmailSender>();` em `AddSesSdk` (`backend/src/GastosApp.Infrastructure/Extensions/AddSesSdk.cs`)

- [ ] 7. Alterar `EnsureAccountCommandHandler` (`backend/src/GastosApp.Application/Accounts/Commands/EnsureAccount/EnsureAccountCommand.cs`) — injetar `IWelcomeEmailSender` e `ILogger<EnsureAccountCommandHandler>`; após `_accountRepository.CreateAsync`, quando `!created.AlreadyExisted`, chamar `_welcomeEmailSender.SendAsync(command.UserId, command.Email, cancellationToken)` dentro de `try/catch` que só loga (`LogError`) e nunca propaga — retorno do handler não muda

- [ ] 8. Rodar `dotnet build backend/GastosApp.sln` e confirmar que compila sem erro

- [ ] 9. Ajustar o construtor usado pelos 3 testes existentes em `backend/tests/GastosApp.UnitTests/Application/EnsureAccountCommandHandlerTests.cs` — mockar `IWelcomeEmailSender`/`ILogger<EnsureAccountCommandHandler>` novos, sem mudar as asserções existentes

- [ ] 10. Adicionar `Handle_ShouldSendWelcomeEmail_WhenAccountIsCreatedForTheFirstTime` (mesmo arquivo) — `CreateAsync` retorna `AlreadyExisted: false`, confirma `IWelcomeEmailSender.Received(1).SendAsync(...)`

- [ ] 11. Adicionar `Handle_ShouldNotSendWelcomeEmail_WhenAccountAlreadyExisted` (mesmo arquivo, `Theory` cobrindo os 2 caminhos que levam a `AlreadyExisted: true` — resolução antecipada por `FindAccountIdByUserIdAsync` e corrida resolvida dentro de `CreateAsync`) — confirma `IWelcomeEmailSender.DidNotReceiveWithAnyArgs().SendAsync(...)` nos dois casos

- [ ] 12. Adicionar `Handle_ShouldNotPropagate_WhenWelcomeEmailSenderThrows` (mesmo arquivo) — `IWelcomeEmailSender.SendAsync` lança exceção, confirma que `Handle` ainda retorna `Result.Success` com o `AccountId`/`AlreadyExisted` corretos

- [ ] 13. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~EnsureAccountCommandHandlerTests` e confirmar tudo passando

- [ ] 14. Criar `backend/tests/GastosApp.UnitTests/Infrastructure/SesWelcomeEmailSenderTests.cs` com `SendAsync_ShouldCallEmailSender_WithSubjectAndFilledTemplate` (mock de `IUserProfileRepository` retornando um `UserProfile` e de `IEmailSender` via NSubstitute — confirma assunto fixo e `{{nome}}`/`{{email}}` substituídos no HTML)

- [ ] 15. Adicionar `SendAsync_ShouldThrow_WhenProfileNotFound` (mesmo arquivo) — `IUserProfileRepository.FindByUserIdAsync` retorna `null`, confirma `InvalidOperationException` e que `IEmailSender.SendAsync` nunca é chamado

- [ ] 16. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~SesWelcomeEmailSenderTests` e confirmar tudo passando

- [ ] 17. Adicionar `Ses__SenderEmail` (mesmo literal de `aws_ssm_parameter.ses_sender_email` em `parameter-store.tf`, `"jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>"`) ao bloco `environment.variables` de `aws_lambda_function.account_trigger` em `backend/infra/terraform/environments/hom/lambda-account-trigger.tf`

- [ ] 18. Repetir a task 17 em `backend/infra/terraform/environments/prod/lambda-account-trigger.tf`, com o literal de prod (`"jrn.expenses <no-reply@jrnexpenses.com>"`)

- [ ] 19. Rodar `terraform fmt`/`terraform validate` (ou `terraform plan`, sem aplicar) nos dois ambientes e confirmar que a variável de ambiente nova é a única mudança — `terraform apply` em si fica fora desta task, segue o fluxo normal com aprovação explícita do usuário antes de cada ambiente (ver "Pontos que precisam de confirmação" do `plan.md`)

- [ ] 20. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unitário) sem regressão

- [ ] 21. Validar manualmente em ambiente local via `AccountTriggerHandlerManualDebug.cs` (LocalStack + cognito-local no ar, `Skip` removido temporariamente) — confirma que o perfil é buscado e o template montado corretamente; a chamada real a `ses:SendEmail` não tem equivalente local (LocalStack Community não emula SES, ver `backend/infra/CLAUDE.md`) e deve cair no `catch` defensivo da task 7 sem quebrar o fluxo — comportamento esperado, não um erro. Devolver o `Skip` ao final

- [ ] 22. Confirmar que `backend/docs/openapi.json` não muda (sem rota HTTP nova) — não é necessário rodar `export-openapi.sh`

- [ ] 23. Atualizar `backend/infra/CLAUDE.md` com uma nota sobre a variável `Ses__SenderEmail` na Lambda de trigger de conta (mesmo padrão da nota já existente sobre `Ses/SenderEmail` no Parameter Store, seção "E-mail transacional (SES, FEAT-33)")

- [ ] 24. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-37-email-boas-vindas/spec.md` e preencher uma seção "Status" — incluir a confirmação (ou correção) empírica de qualquer suposição do `plan.md`, e o resultado da validação manual/deploy em hom (e-mail recebido de verdade, remetente/assunto/link corretos) assim que aplicável

- [ ] 25. Atualizar `backend/docs/backlog.md` — marcar o item da FEAT-37 como concluído, seguindo a convenção já usada para features anteriores (ver commit da FEAT-36)
