# Tasks — FEAT-34: Custom Message trigger do Cognito (e-mails de auth com HTML)

Ordem pensada pra manter dependência antes de dependente (Application/
Infrastructure do `name` no Cognito → novo projeto Lambda → Terraform →
CI/CD → validação manual → testes → fechamento). Cada item é do tamanho
de um commit, exceto onde marcado como ação manual (aprovação/`apply`/
validação em hom).

## `name` como atributo do Cognito (`RegisterAsync`)

- [x] 1. Atualizar `IAuthService.RegisterAsync`
      (`GastosApp.Application/Common/Interfaces/IAuthService.cs`):
      assinatura ganha o parâmetro `string name` (entre `password` e
      `cancellationToken`).
- [x] 2. Atualizar `CognitoAuthService.RegisterAsync`
      (`GastosApp.Infrastructure/Auth/CognitoAuthService.cs`): recebe
      `name` e inclui `new AttributeType { Name = "name", Value = name }`
      em `SignUpRequest.UserAttributes`, ao lado do `email` já existente.
- [x] 3. Atualizar `RegisterUserCommandHandler.Handle`
      (`GastosApp.Application/Auth/Commands/Register/RegisterUserCommand.cs`):
      chamada a `_authService.RegisterAsync(...)` passa também
      `command.Name.Trim()`.
- [x] 4. Atualizar `UnitTests/Infrastructure/CognitoAuthServiceTests.cs`:
      teste de `RegisterAsync` cobre que `UserAttributes` inclui `name`
      além de `email`.
- [x] 5. Atualizar `UnitTests/Application/RegisterUserCommandHandlerTests.cs`:
      ajustar assinatura do mock de `IAuthService.RegisterAsync` (novo
      parâmetro `name`) nas chamadas existentes, sem mudança de
      comportamento esperado.
- [x] 6. Atualizar `ComponentTests/Auth/AuthEndpointsTests.cs`: mesma
      atualização de assinatura do mock de `IAuthService`, confirmando
      que o contrato HTTP de `POST /auth/register` continua idêntico.

## Novo projeto — `GastosApp.CognitoTriggers.CustomMessage`

- [x] 7. Criar o projeto `GastosApp.CognitoTriggers.CustomMessage`
      (`net10.0`, `PublishAot=true`, `InvariantGlobalization=true`,
      `OutputType=Exe`, `AWSProjectType=Lambda`; pacotes
      `Amazon.Lambda.Core`/`Amazon.Lambda.RuntimeSupport`/
      `Amazon.Lambda.Serialization.SystemTextJson`/
      `Microsoft.Extensions.Logging.Console`; **sem** `ProjectReference`
      para `Application`/`Infrastructure`) e adicioná-lo à
      `GastosApp.sln`.
- [x] 8. Criar `CognitoCustomMessageEvent.cs`
      (`CognitoCustomMessageEvent`/`CallerContext`/`Request`/`Response`,
      POCO próprio — ver `plan.md` seção 1), com
      `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`
      nos campos opcionais (`ClientId`, `UsernameParameter`,
      `ClientMetadata`, `SmsMessage`).
- [x] 9. Criar `CognitoCustomMessageJsonSerializerContext.cs`
      (`JsonSourceGenerationOptions(PropertyNamingPolicy = CamelCase)`,
      `[JsonSerializable(typeof(CognitoCustomMessageEvent))]`).
- [x] 10. Copiar `frontend/design-system/emails/01-confirmacao-cadastro.html`
      e `02-recuperacao-senha.html` para
      `GastosApp.CognitoTriggers.CustomMessage/Templates/`, corrigindo
      nos dois as URLs de `app.jrnexpenses.com.br` para `jrnexpenses.com`
      (requisito de negócio do `spec.md`), e marcá-los como
      `EmbeddedResource` no `.csproj`.
- [x] 11. Criar `EmailTemplateProvider.cs`: carrega os dois HTMLs
      embutidos uma vez (`static readonly string`, via
      `Assembly.GetManifestResourceStream`), expondo `SignUpTemplate` e
      `ForgotPasswordTemplate`.
- [x] 12. Criar `CustomMessageTriggerHandler.HandleAsync(...)`: mapeia
      `TriggerSource` → template/assunto (`CustomMessage_SignUp`/
      `CustomMessage_ResendCode` → template de cadastro,
      `CustomMessage_ForgotPassword` → template de recuperação, outros
      → não altera `Response`), substitui `{{codigo}}` (literal
      `Request.CodeParameter`, sem tocar no valor), `{{nome}}` (com
      fallback textual se ausente) e `{{email}}` no corpo e no assunto,
      envolto em `try/catch` que só loga e sempre retorna o evento
      (nunca propaga — ver decisão técnica 2 do `plan.md`).
- [x] 13. Criar `Function.cs`: composition root sem `ServiceCollection`
      (só `ILoggerFactory` avulso), chama
      `CustomMessageTriggerHandler.HandleAsync` e sobe via
      `LambdaBootstrapBuilder` com
      `SourceGeneratorLambdaJsonSerializer<CognitoCustomMessageJsonSerializerContext>`
      (naming policy camelCase explícita, mesmo gotcha da FEAT-19).

## Infraestrutura (Terraform) — hom e prod

- [x] 14. Criar `infra/lambda/Dockerfile.build-custom-message-trigger` +
      `build-custom-message-trigger.sh`, mesmo padrão AOT/Amazon Linux
      2023 do account-trigger, publicando
      `src/GastosApp.CognitoTriggers.CustomMessage/GastosApp.CognitoTriggers.CustomMessage.csproj`.
- [x] 15. Criar `environments/hom/lambda-custom-message-trigger.tf`: IAM
      Role `jrnexpenses-custom-message-trigger-lambda-exec-hom` (só
      `logs:CreateLogStream`/`PutLogEvents` no próprio log group — sem
      `dynamodb:*`/`cognito-idp:*`/`ses:*`), CloudWatch Log Group,
      `aws_lambda_function.custom_message_trigger`.
- [x] 16. Repetir a task 15 em
      `environments/prod/lambda-custom-message-trigger.tf`
      (`jrnexpenses-custom-message-trigger-lambda-exec`).
- [x] 17. Adicionar `aws_lambda_permission` (hom e prod) liberando
      `lambda:InvokeFunction` pro principal `cognito-idp.amazonaws.com`,
      `source_arn = aws_cognito_user_pool.main.arn`,
      `statement_id = "AllowCognitoInvokeCustomMessage"`.
- [x] 18. Adicionar `custom_message = aws_lambda_function.custom_message_trigger.arn`
      ao bloco `lambda_config` já existente em
      `aws_cognito_user_pool.main` (`cognito.tf`, hom e prod), ao lado
      de `post_confirmation`.
- [x] 19. Ampliar a política da IAM Role `gastosapp-backend-cicd`
      (`infra/terraform/cicd/`) para
      `lambda:UpdateFunctionCode`/`UpdateFunctionConfiguration` também
      nos dois `jrnexpenses-custom-message-trigger{-hom}`.

## CI/CD

- [x] 20. Criar `backend-deploy-custom-message-trigger-hom.yml`:
      path-filtrado só em
      `backend/src/GastosApp.CognitoTriggers.CustomMessage/**` +
      `backend/infra/lambda/Dockerfile.build-custom-message-trigger`/
      `build-custom-message-trigger.sh` + `backend/GastosApp.sln` (mais
      estreito que o do account-trigger — este projeto não referencia
      `Application`/`Domain`/`Infrastructure`); gate de qualidade +
      build do artefato + `aws lambda update-function-code`, mesmo
      padrão de `backend-deploy-account-trigger-hom.yml`.
- [x] 21. Criar `backend-deploy-custom-message-trigger-prod.yml`:
      disparado por Release `backend-v*`, mesmo padrão de
      `backend-deploy-account-trigger-prod.yml`.
- [x] 22. **(Ação manual, fora do código)** Adicionar a variável
      `CUSTOM_MESSAGE_TRIGGER_FUNCTION_NAME` nos GitHub Environments
      `backend-hom` e `backend-prod`, com o nome real da função Lambda
      de cada ambiente.

## Aplicação da infraestrutura (manual, mediante aprovação)

- [x] 23. **(Ação manual)** Rodar `terraform plan`/`apply` em
      `environments/hom/` **só após aprovação explícita do usuário**
      (`aws_lambda_function.custom_message_trigger`, IAM Role, log
      group, `aws_lambda_permission` e o novo `lambda_config` do User
      Pool de hom).
- [x] 24. **(Ação manual)** Repetir a task 23 em `environments/prod/`,
      também mediante aprovação explícita do usuário, depois de validar
      hom.

## Validação manual em hom (critérios de aceite do `spec.md`)

- [x] 25. Validar `CustomMessage_SignUp`: `POST /auth/register` real
      em hom, conferir e-mail recebido com HTML de
      `01-confirmacao-cadastro.html`, `{{codigo}}`/`{{nome}}`/`{{email}}`
      resolvidos corretamente e URLs já apontando pro domínio real.
      **Achado real (ao vivo em hom):** `emailSubject` não recebe a
      substituição de `{####}` do Cognito (só `emailMessage`) —
      divergência da decisão técnica 5 do `plan.md`; corrigido (assunto
      passou a ser texto fixo, sem `{{codigo}}`) e revalidado via task 26.
      `{{nome}}` ficou pendente nesta rodada porque a API de hom ainda
      roda o código de `develop` (sem a mudança desta branch que manda
      `name` pro Cognito) — o deploy da API só dispara em push pra
      `develop`. `{{codigo}}`/`{{email}}`/HTML/URLs validados OK.
      Validação end-to-end de `{{nome}}` fica pendente pra depois do
      merge+deploy (ver seção "Status" do `spec.md`).
- [x] 26. Validar `CustomMessage_ResendCode`: disparar reenvio via
      console/CLI do Cognito (`aws cognito-idp resend-confirmation-code`)
      contra o User Pool de hom, conferir mesmo template/variáveis.
      Validado após deploy manual do fix do assunto (task 25) — assunto,
      código e e-mail corretos; mesma pendência de `{{nome}}` pós-merge.
- [x] 27. Validar `CustomMessage_ForgotPassword`: disparar via
      console/CLI do Cognito (`aws cognito-idp forgot-password`) contra
      o User Pool de hom, conferir e-mail com HTML de
      `02-recuperacao-senha.html`. Validado — assunto, template, código
      e URL (`/recuperar`) corretos.
- [x] 28. Validar que um `TriggerSource` fora do escopo (ex.: criar um
      usuário via `AdminCreateUser` em hom, disparando
      `CustomMessage_AdminCreateUser`) continua com o texto padrão do
      Cognito, sem regressão. Validado — e-mail chegou no formato padrão
      do Cognito (usuário/senha temporária), sem o HTML customizado.
- [x] 29. Validar o fallback defensivo: simular falha no handler (ex.:
      publicar temporariamente uma build que force exceção na
      formatação) e confirmar que `SignUp`/reenvio/recuperação de senha
      completam normalmente, com o e-mail saindo no texto padrão do
      Cognito — depois reverter para a build correta. **Decisão do
      usuário:** aceitar a cobertura do teste automatizado
      `HandleAsync_ShouldNeverPropagateFailure_WhenFormattingThrows`
      (já passando) como evidência suficiente, sem repetir simulação
      contra hom real.

## Testes automatizados

- [x] 30. Adicionar `GastosApp.CognitoTriggers.CustomMessage` como
      `ProjectReference` em `GastosApp.UnitTests.csproj`.
- [x] 31. `UnitTests/CognitoTriggers/CustomMessageTriggerHandlerTests.cs`:
      `CustomMessage_SignUp` e `CustomMessage_ResendCode` resolvem
      `{{codigo}}`/`{{nome}}`/`{{email}}` corretamente no corpo e no
      assunto usando o template de cadastro; `CustomMessage_ForgotPassword`
      usa o template de recuperação; `{{nome}}` ausente aplica o
      fallback textual; `TriggerSource` fora dos 3 cobertos não altera
      `Response`; exceção simulada durante a formatação é capturada,
      logada, e o evento é retornado sem alteração (nunca propaga).

## Fechamento

- [ ] 32. Rodar `./scripts/export-openapi.sh` e confirmar que
      `backend/docs/openapi.json` não sofre diff de contrato (nenhum
      endpoint novo/alterado nesta feature).
- [ ] 33. Rodar a suíte completa (`dotnet test GastosApp.sln --filter
      "Category!=Integration"`) e confirmar 100% dos testes passando.
- [ ] 34. Rodar localmente os testes integrados relevantes de Auth
      (`backend/infra/lambda/run-local.sh` + `GastosApp.IntegrationTests`,
      filtro do módulo Auth) para confirmar que a mudança em
      `RegisterAsync` (novo atributo `name`) não quebra o fluxo real de
      cadastro.
- [ ] 35. Atualizar `spec.md`: marcar os critérios de aceite concluídos
      (`- [x]`) e adicionar a seção "Status" (mesmo padrão de
      `backend/specs/FEAT-19-conta-multi-tenant/spec.md`) resumindo o
      que foi implementado.
