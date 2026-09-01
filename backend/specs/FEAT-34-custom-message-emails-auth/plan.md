# Plan — FEAT-34: Custom Message trigger do Cognito (e-mails de auth com HTML)

Decisões de arquitetura já fechadas em conversa (registradas aqui para
não se perderem): **novo projeto Lambda** `GastosApp.CognitoTriggers.CustomMessage`,
irmão de `GastosApp.CognitoTriggers` (não o mesmo — um executável Lambda
só tem um `Main`/handler, e os dois eventos têm formato diferente), **sem
referenciar `Application`/`Infrastructure`** (este trigger só formata
texto a partir do que o próprio evento do Cognito já traz — não lê
DynamoDB, não chama a API do Cognito, não usa Mediator); templates HTML
carregados como `EmbeddedResource` referenciando diretamente os arquivos
de `frontend/design-system/emails/` (sem duplicar conteúdo — ver ponto a
confirmar 1); `IAuthService.RegisterAsync` ganha o parâmetro `name`.

## 1. Camadas afetadas

### Novo projeto — `GastosApp.CognitoTriggers.CustomMessage`
- `GastosApp.CognitoTriggers.CustomMessage.csproj`: `net10.0`,
  `PublishAot=true`, `InvariantGlobalization=true`,
  `OutputType=Exe`, `AWSProjectType=Lambda`. Pacotes: só
  `Amazon.Lambda.Core`, `Amazon.Lambda.RuntimeSupport`,
  `Amazon.Lambda.Serialization.SystemTextJson`,
  `Microsoft.Extensions.Logging.Console` — **sem** `ProjectReference`
  para `Application`/`Infrastructure` (diferente de
  `GastosApp.CognitoTriggers`, que precisa de `Mediator`/DynamoDB; este
  não). Reduz superfície de IAM e o binário Native AOT.
- `CognitoCustomMessageEvent.cs` — mesmo raciocínio de
  `CognitoPostConfirmationEvent.cs` (não existe pacote oficial da AWS
  pra Lambda triggers de User Pool em .NET). Formato documentado em
  `docs.aws.amazon.com/cognito/latest/developerguide/user-pool-lambda-custom-message.html`:
  ```csharp
  public sealed class CognitoCustomMessageEvent
  {
      public string Version { get; set; } = "";
      public string Region { get; set; } = "";
      public string UserPoolId { get; set; } = "";
      public string UserName { get; set; } = "";
      public CognitoCustomMessageCallerContext CallerContext { get; set; } = new();
      public string TriggerSource { get; set; } = "";
      public CognitoCustomMessageRequest Request { get; set; } = new();
      public CognitoCustomMessageResponse Response { get; set; } = new();
  }

  public sealed class CognitoCustomMessageCallerContext
  {
      public string AwsSdkVersion { get; set; } = "";

      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
      public string? ClientId { get; set; }
  }

  public sealed class CognitoCustomMessageRequest
  {
      public Dictionary<string, string> UserAttributes { get; set; } = new();

      // Literal "{####}" — NÃO é o código real (ver decisão técnica 1).
      public string CodeParameter { get; set; } = "";

      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
      public string? UsernameParameter { get; set; }

      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
      public Dictionary<string, string>? ClientMetadata { get; set; }
  }

  public sealed class CognitoCustomMessageResponse
  {
      [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
      public string? SmsMessage { get; set; }
      public string? EmailMessage { get; set; }
      public string? EmailSubject { get; set; }
  }
  ```
  Mesmos gotchas já resolvidos na FEAT-19 valem aqui (ver
  `backend/infra/CLAUDE.md`): campos opcionais ausentes no payload
  original (`usernameParameter`, `clientMetadata`, `clientId`,
  `smsMessage`) precisam de `[JsonIgnore(WhenWritingNull)]`, senão o
  round-trip devolve `null` explícito e o Cognito rejeita com
  `InvalidLambdaResponseException` — **validar isso de novo aqui**
  (o formato não é idêntico ao de `PostConfirmation`, não dá pra
  assumir que os mesmos campos exatos se aplicam sem checar contra um
  payload real de `CustomMessage` em hom).
- `CognitoCustomMessageJsonSerializerContext.cs` — mesmo padrão de
  `CognitoTriggerJsonSerializerContext` (camelCase obrigatório,
  `[JsonSerializable(typeof(CognitoCustomMessageEvent))]`).
- `EmailTemplateProvider.cs` — classe estática, carrega os dois HTMLs
  como `EmbeddedResource` uma vez (campos `static readonly string`,
  lidos via `Assembly.GetManifestResourceStream` + `StreamReader` no
  cold start; não precisa de reflection nem de JSON, seguro sob Native
  AOT). Expõe `SignUpTemplate` e `ForgotPasswordTemplate`.
- `CustomMessageTriggerHandler.cs` — mesmo formato estático de
  `AccountTriggerHandler`, sem `ISender` (não há Application aqui):
  ```csharp
  public static class CustomMessageTriggerHandler
  {
      public static Task<CognitoCustomMessageEvent> HandleAsync(
          CognitoCustomMessageEvent evt, ILogger logger, CancellationToken cancellationToken)
      {
          try
          {
              var (template, subject) = evt.TriggerSource switch
              {
                  "CustomMessage_SignUp" or "CustomMessage_ResendCode"
                      => (EmailTemplateProvider.SignUpTemplate, "Seu código de confirmação: {{codigo}}"),
                  "CustomMessage_ForgotPassword"
                      => (EmailTemplateProvider.ForgotPasswordTemplate, "Código para redefinir sua senha: {{codigo}}"),
                  _ => (null, null) // fora do escopo — Cognito usa o texto padrão dele
              };

              if (template is not null)
              {
                  var nome = evt.Request.UserAttributes.GetValueOrDefault("name");
                  var email = evt.Request.UserAttributes.GetValueOrDefault("email", "");
                  var saudacao = string.IsNullOrWhiteSpace(nome) ? "Olá" : nome; // fallback defensivo (US4/Requisitos)

                  string Fill(string texto) => texto
                      .Replace("{{codigo}}", evt.Request.CodeParameter) // literal "{####}" — Cognito substitui depois
                      .Replace("{{nome}}", saudacao)
                      .Replace("{{email}}", email);

                  evt.Response.EmailMessage = Fill(template);
                  evt.Response.EmailSubject = Fill(subject!);
              }
          }
          catch (Exception ex)
          {
              // Nunca propaga — CustomMessage também é síncrono dentro de
              // SignUpAsync/ResendConfirmationCodeAsync/ForgotPasswordAsync
              // (ver decisão técnica 1). Falha aqui devolve o evento sem
              // alterar Response — Cognito usa o texto padrão dele (US4).
              logger.LogError(ex, "Falha ao formatar CustomMessage para TriggerSource {TriggerSource}.", evt.TriggerSource);
          }

          return Task.FromResult(evt);
      }
  }
  ```
- `Function.cs` — composition root bem mais simples que o de
  `GastosApp.CognitoTriggers` (sem `ServiceCollection`/DI, já que não
  há repositório/Mediator para resolver):
  ```csharp
  using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
  var logger = loggerFactory.CreateLogger("GastosApp.CognitoTriggers.CustomMessage");

  var handler = (CognitoCustomMessageEvent evt, ILambdaContext context) =>
      CustomMessageTriggerHandler.HandleAsync(evt, logger, CancellationToken.None);

  await LambdaBootstrapBuilder.Create(
          handler,
          new SourceGeneratorLambdaJsonSerializer<CognitoCustomMessageJsonSerializerContext>(
              options => options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase))
      .Build()
      .RunAsync();
  ```
- Adicionar o novo projeto a `GastosApp.sln`.

### Infrastructure — `GastosApp.Infrastructure`
- `Auth/CognitoAuthService.cs` (`RegisterAsync`): assinatura ganha
  `string name`; `SignUpRequest.UserAttributes` passa a incluir também
  `new AttributeType { Name = "name", Value = name }`, ao lado do
  `email` já existente. Sem mudança de comportamento fora isso (mesmo
  `try/catch` de `UsernameExistsException`).

### Application — `GastosApp.Application`
- `Common/Interfaces/IAuthService.cs`: `RegisterAsync` ganha o
  parâmetro `string name` (entre `password` e `cancellationToken`).
- `Auth/Commands/Register/RegisterUserCommand.cs`
  (`RegisterUserCommandHandler.Handle`): chamada a
  `_authService.RegisterAsync(...)` passa também `command.Name.Trim()`
  (mesmo `.Trim()` já aplicado logo abaixo, em `UserProfile.Create`, por
  consistência). Nenhuma outra linha muda — `_userProfileRepository.CreateAsync`
  continua exatamente como está hoje (ver Contexto do spec.md: a
  alternativa de mover a criação do perfil pra dentro do trigger foi
  descartada).

### Api / Domain
Sem mudança. `POST /auth/register` mantém request/response idênticos —
`name` já é um campo existente do `RegisterUserCommand`/request, só
passa a ser propagado também para o Cognito.

## 2. Modelo de dados (DynamoDB)

Nenhuma tabela, índice ou item novo — este trigger não lê nem escreve
no DynamoDB (diferente do `PostConfirmation`/`AccountTriggerHandler`).
`UserProfile` continua sendo gravado exatamente como hoje.

## 3. Decisões técnicas

**1. `request.codeParameter` é o *placeholder* literal `"{####}"`, não
o código real — confirmado na documentação da AWS do trigger
`CustomMessage`.** O handler nunca vê o código de verdade; ele só
precisa colocar esse token literal na posição de `{{codigo}}` (tanto no
corpo quanto no assunto) e devolver. O Cognito substitui `{####}` pelo
código real **depois** que o Lambda retorna, em qualquer um dos dois
campos (`emailMessage`/`emailSubject`) onde o token aparecer. Ponto
crítico de implementação — testar contra um evento real de hom antes de
considerar concluído (não só com mock).

**2. `CustomMessage_*` também é invocado de forma síncrona, dentro da
própria chamada `SignUpAsync`/`ResendConfirmationCodeAsync`/
`ForgotPasswordAsync`** — mesmo raciocínio já usado para
`PostConfirmation` na FEAT-19 (`AccountTriggerHandler`). Por isso
`CustomMessageTriggerHandler` nunca propaga exceção (US4 do spec.md):
diferente do trigger de conta, aqui uma falha bloqueada significaria o
próprio `SignUp`/reenvio/recuperação de senha falhar pro usuário só
porque o e-mail não pôde ser formatado — sempre preferível cair no
texto padrão do Cognito.

**3. Templates carregados como `EmbeddedResource` apontando direto
para `frontend/design-system/emails/*.html` (caminho relativo no
`.csproj`), não copiados para dentro de `backend/`.** Fonte única de
verdade, sem risco de os dois arquivos divergirem com o tempo — mas é
uma referência de build cruzando a fronteira `backend`/`frontend` (ver
ponto a confirmar 1, porque o `/CLAUDE.md` raiz diz "não existe
infraestrutura compartilhada entre contextos"; entendo que um asset de
design referenciado no build não é a mesma coisa que infraestrutura
compartilhada, mas é uma leitura, não um fato já decidido).

**4. Nenhum motor de template (Scriban/Razor/Handlebars) — só
`string.Replace` para as 3 variáveis usadas por estes dois templates
(`{{nome}}`, `{{email}}`, `{{codigo}}`).** `{{data}}`/`{{dispositivo}}`
existem no vocabulário do design system (README), mas não aparecem em
`01-confirmacao-cadastro.html` nem `02-recuperacao-senha.html` — fora
de escopo aqui.

**5. Assunto (`emailSubject`) usa o texto sugerido no
`frontend/design-system/emails/README.md`** ("Seu código de
confirmação: {{codigo}}" / "Código para redefinir sua senha:
{{codigo}}"), passando pelo mesmo `Fill(...)` do corpo — o token
`{####}` funciona igual em `emailSubject` (confirmado na documentação
da AWS, decisão técnica 1).

**6. Sem `ISender`/Mediator/DI container neste projeto** — diferente de
`GastosApp.CognitoTriggers`, que precisa montar `Application`+
`Infrastructure` pra chamar `EnsureAccountCommand`. Aqui não há caso de
uso a orquestrar, só transformação de string a partir do próprio
evento — um `ILoggerFactory` avulso é suficiente, sem
`ServiceCollection`.

## 4. Recursos AWS usados ou afetados

**Recursos novos** (sujeitos a aprovação explícita do usuário antes do
`terraform apply`, hom primeiro e depois prod — nenhum `apply` roda
durante a implementação sem essa aprovação):
- 1 função Lambda por ambiente:
  `jrnexpenses-custom-message-trigger-{hom|}`, `provided.al2023`,
  artefato próprio (`infra/lambda/custom-message-trigger-function.zip`
  — novo `Dockerfile.build-custom-message-trigger`/
  `build-custom-message-trigger.sh`, mesmo padrão dos scripts do
  account-trigger, publicando o novo projeto).
- 1 IAM Role de execução por ambiente
  (`jrnexpenses-custom-message-trigger-lambda-exec-{hom|}`), com a
  **menor permissão de todos os Lambdas do projeto até agora**: só
  `logs:CreateLogStream`/`logs:PutLogEvents` no próprio log group —
  sem `dynamodb:*`, sem `cognito-idp:*`, sem `ses:*` (este trigger
  nunca chama SES nem a API do Cognito, só recebe/devolve o evento).
- 1 `aws_lambda_permission` por ambiente, `lambda:InvokeFunction` para
  `cognito-idp.amazonaws.com`, `source_arn = aws_cognito_user_pool.main.arn`
  (`statement_id` distinto do já existente, ex.:
  `AllowCognitoInvokeCustomMessage`).

**Recursos existentes modificados:**
- `aws_cognito_user_pool.main` (`cognito.tf`, hom e prod): bloco
  `lambda_config` ganha uma segunda entrada, ao lado da já existente:
  ```hcl
  lambda_config {
    post_confirmation = aws_lambda_function.account_trigger.arn
    custom_message    = aws_lambda_function.custom_message_trigger.arn
  }
  ```
- IAM Role `gastosapp-backend-cicd`
  (`backend/infra/terraform/cicd/`): política ampliada para
  `lambda:UpdateFunctionCode`/`UpdateFunctionConfiguration` também no
  novo Lambda (hoje cobre `gastos-app-api{-hom}` e
  `jrnexpenses-account-trigger{-hom}`).
- Dois novos workflows de deploy (`.github/workflows/`), espelhando
  `backend-deploy-account-trigger-{hom,prod}.yml`, com path filter
  **mais estreito** que o do account-trigger (só o próprio projeto —
  sem `Application`/`Domain`/`Infrastructure`, já que este Lambda não
  os referencia):
  ```
  backend/src/GastosApp.CognitoTriggers.CustomMessage/**
  backend/infra/lambda/Dockerfile.build-custom-message-trigger
  backend/infra/lambda/build-custom-message-trigger.sh
  backend/GastosApp.sln
  ```
  mais uma variável nova no GitHub Environment (`backend-hom`/
  `backend-prod`): `CUSTOM_MESSAGE_TRIGGER_FUNCTION_NAME`.

**Sem mudança:** tabela `GastosApp`, SES (`ses.tf`, FEAT-33 —
`email_configuration` do User Pool já aponta pra lá, este trigger não
precisa de permissão própria), nenhum novo App Client, nenhum schema
novo no User Pool (`name` já é atributo padrão do Cognito, habilitado
por padrão quando não há `schema` block restringindo — confirmado:
`cognito.tf` só declara `schema` explícito para `email`).

## 5. Erros de negócio → `ErrorType`/HTTP

Nenhum. Esta feature não introduz nem altera contrato HTTP, e o
handler nunca retorna erro pro Cognito (US4 do spec.md — falha vira log
+ fallback pro texto padrão, nunca uma exceção observável). `AuthErrors`
não muda.

## 6. Testes (visão geral — detalhamento fica pro `tasks.md`)

- Novo `UnitTests`/`ComponentTests` para `CustomMessageTriggerHandler`
  (invocado diretamente, evento construído em memória — sem precisar
  do runtime do Lambda), cobrindo: os 3 `TriggerSource` com
  `{{codigo}}`/`{{nome}}`/`{{email}}` resolvidos; `{{nome}}` ausente
  (fallback textual); `TriggerSource` fora de escopo (Response não
  alterado); exceção simulada durante a formatação (evento devolvido
  sem alteração, sem propagar) — mesmo padrão de
  `AccountTriggerHandlerTests`.
- `CognitoAuthServiceTests.cs` (`GastosApp.UnitTests`): teste de
  `RegisterAsync` precisa cobrir que `UserAttributes` agora inclui
  `name` além de `email`.
- `RegisterUserCommandHandlerTests.cs`: mock de `IAuthService.RegisterAsync`
  ganha o parâmetro `name` na assinatura — ajustar chamadas existentes
  do mock para não quebrar de compilação.
- `AuthEndpointsTests.cs` (`ComponentTests`): mesma atualização de
  assinatura do mock de `IAuthService`, sem mudança de comportamento
  esperado (contrato HTTP de `/auth/register` não muda).
- Validação manual em hom (critérios de aceite do spec.md): os 3
  `TriggerSource` só são exercitáveis de fato depois do `terraform
  apply` do `lambda_config.custom_message` — sem isso, o Cognito nem
  invoca este Lambda. `CustomMessage_SignUp` valida via `POST
  /auth/register` real; `ResendCode`/`ForgotPassword` (sem endpoint
  ainda — FEAT-35/36) validam via console/CLI do Cognito
  (`aws cognito-idp resend-confirmation-code`/`forgot-password`,
  contra o User Pool de hom).
- `backend/docs/openapi.json`: regenerar só para confirmar ausência de
  diff (constitution) — não é esperada mudança de contrato.

## Pontos a confirmar antes do `/tasks`

1. **Templates via `EmbeddedResource` apontando pra
   `frontend/design-system/emails/*.html` (caminho relativo,
   cross-context) vs. copiar os 2 HTMLs para dentro do
   `GastosApp.CognitoTriggers.CustomMessage` e manter em sync
   manualmente.** Recomendo a primeira opção (fonte única de verdade,
   sem risco de divergência), mas é uma leitura de "referência de
   design ≠ infraestrutura compartilhada" que vale confirmar
   explicitamente, já que o `/CLAUDE.md` raiz é enfático sobre não
   misturar os dois contextos.
2. **Nome dos recursos Terraform novos** — segue o mesmo prefixo
   `jrnexpenses-` já usado pelo account-trigger (não `gastos-app`)?
   Assumido que sim por consistência, mas nunca foi decidido
   explicitamente para este recurso específico.
3. **Timing do `terraform apply`** — como o e-mail de cadastro (`POST
   /auth/register`) já funciona hoje em hom (sem HTML de marca própria,
   texto padrão do Cognito), o `apply` desta feature pode esperar todo
   o código estar pronto e testado via ComponentTest antes de tocar
   infraestrutura real, ou você prefere aplicar em hom mais cedo (Lambda
   "vazio"/sem `lambda_config` ligado ainda) para reduzir o tamanho do
   `apply` final? Sem urgência — só documentando que a ordem não foi
   combinada.
