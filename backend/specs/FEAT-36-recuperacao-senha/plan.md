# Plan — FEAT-36: Recuperação de senha

## Camadas afetadas

- **Application** — duas novas pastas de comando, mesmo padrão de
  `Commands/Confirm`/`Commands/ResendConfirmation` (FEAT-35):
  - `Auth/Commands/ForgotPassword/` — `ForgotPasswordCommand` (+
    Handler + Validator)
  - `Auth/Commands/ResetPassword/` — `ResetPasswordCommand` (+ Handler
    + Validator) — único Handler desta feature com orquestração real
    (chama `IAuthService` e, só em caso de sucesso, dispara o email de
    aviso via `IPasswordChangedEmailSender`)
  - `AuthErrors.cs` ganha `InvalidResetCode` e `ExpiredResetCode`
  - `IAuthService.cs` ganha dois métodos novos
  - `Common/Interfaces/IEmailSender.cs` (novo) — abstração genérica de
    envio de email, pensada para ser reaproveitada pela FEAT-37 (a
    própria FEAT-33 já previa FEAT-36/FEAT-37 como os dois futuros
    consumidores de envio direto via SES)
  - `Common/Interfaces/IPasswordChangedEmailSender.cs` (novo) —
    abstração específica desta feature (monta o conteúdo do email de
    "senha alterada" a partir do template), composta sobre `IEmailSender`
- **Infrastructure**:
  - `Auth/CognitoAuthService.cs` implementa os dois métodos novos de
    `IAuthService`, chamando `ForgotPasswordAsync`/
    `ConfirmForgotPasswordAsync` do SDK do Cognito, mesmo padrão de
    mapeamento de exceção já usado pelos métodos existentes
  - `Email/SesEmailService.cs` (novo) — implementa `IEmailSender` via
    `AWSSDK.SimpleEmailV2` (`SendEmailAsync`)
  - `Email/SesPasswordChangedEmailSender.cs` (novo) — implementa
    `IPasswordChangedEmailSender`: carrega o template embarcado, faz a
    substituição de `{{email}}`/`{{data}}`/`{{dispositivo}}`, chama
    `IEmailSender.SendAsync`
  - `Email/Templates/03-senha-alterada.html` (novo, `EmbeddedResource`)
    — cópia de `frontend/design-system/emails/03-senha-alterada.html`
    (mesma decisão de duplicar em vez de referenciar por caminho
    relativo, já tomada na FEAT-34) já com o texto ajustado (decisão 4
    do spec.md)
  - `Email/PasswordChangedEmailTemplateProvider.cs` (novo) — carrega o
    template embarcado uma vez no cold start, mesmo padrão de
    `EmailTemplateProvider` (`GastosApp.CognitoTriggers.CustomMessage`)
  - `Configuration/SesOptions.cs` (novo) — POCO com `SenderEmail`
  - `Extensions/InfraEmailExtensions.cs` (novo) — `AddSesSdk`, mesmo
    padrão de `InfraAuthExtensions.AddCognitoSdk`: leitura manual de
    `IConfiguration` (sem `Configure<T>()`/reflection, AOT-safe),
    registra `IAmazonSimpleEmailServiceV2`, `IEmailSender` →
    `SesEmailService`, `IPasswordChangedEmailSender` →
    `SesPasswordChangedEmailSender`
  - `DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
    — `AddAwsInfrastructure` passa a chamar `services.AddSesSdk(configuration)`
  - `GastosApp.Infrastructure.csproj` — novo `PackageReference`
    `AWSSDK.SimpleEmailV2` + novo `EmbeddedResource` do template
- **Api**:
  - `AuthEndpoints.cs` ganha `POST /auth/forgot-password` e
    `POST /auth/reset-password` + os records `ForgotPasswordRequest`/
    `ResetPasswordRequest`
  - `AppJsonSerializerContext.cs` ganha `[JsonSerializable]` pros dois
    requests novos (nenhum DTO de resposta — sucesso é sempre 200 sem
    corpo, decisão 5 do spec.md)
  - `ApplicationServiceCollectionExtensions.cs` (Application, mas
    listado aqui por ser registro de DI) ganha
    `IValidator<ForgotPasswordCommand>`/`IValidator<ResetPasswordCommand>`
- **Domain** — sem mudança. Nenhuma entidade nova, nenhuma regra de
  negócio pura envolvida.

## Contratos técnicos

### `IAuthService.cs` (Application)

```csharp
public interface IAuthService
{
    // ...métodos existentes sem mudança...

    // NOVO (FEAT-36)
    Task<Result> ForgotPasswordAsync(string email, CancellationToken cancellationToken = default);
    Task<Result> ConfirmForgotPasswordAsync(string email, string code, string newPassword, CancellationToken cancellationToken = default);
}
```

`Result` não-genérico nos dois — nenhum dos dois fluxos devolve dado em
caso de sucesso (spec.md, decisão 5), mesmo padrão de
`ConfirmSignUpAsync`/`ResendConfirmationCodeAsync`.

### `CognitoAuthService.cs` (Infrastructure)

```csharp
public async Task<Result> ForgotPasswordAsync(
    string email, CancellationToken cancellationToken = default)
{
    try
    {
        await _cognitoClient.ForgotPasswordAsync(new ForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email
        }, cancellationToken);
    }
    catch (UserNotFoundException)
    {
        // Decisão 1 (spec.md): não revela se o email existe. O
        // prevent_user_existence_errors="ENABLED" do User Pool já cobre
        // isso no próprio Cognito; este catch é defensivo.
    }
    catch (InvalidParameterException)
    {
        // Usuário existe mas ainda não confirmado (sem atributo de
        // contato verificado) — mesmo princípio de não-enumeração.
    }

    // Decisão 1: sempre 200. Qualquer exceção fora das duas acima
    // (ex.: LimitExceededException do throttling nativo) é
    // verdadeiramente inesperada e propaga pro GlobalExceptionHandler.
    return Result.Success();
}

public async Task<Result> ConfirmForgotPasswordAsync(
    string email, string code, string newPassword,
    CancellationToken cancellationToken = default)
{
    try
    {
        await _cognitoClient.ConfirmForgotPasswordAsync(new ConfirmForgotPasswordRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            ConfirmationCode = code,
            Password = newPassword
        }, cancellationToken);

        return Result.Success();
    }
    catch (ExpiredCodeException)
    {
        return Result.Failure(AuthErrors.ExpiredResetCode);
    }
    catch (CodeMismatchException)
    {
        return Result.Failure(AuthErrors.InvalidResetCode);
    }
    catch (UserNotFoundException)
    {
        // Decisão 2 (spec.md): mesma resposta de código incorreto.
        return Result.Failure(AuthErrors.InvalidResetCode);
    }
    catch (InvalidPasswordException)
    {
        // Reaproveita AuthErrors.Validation (bad-request) — mesma
        // mensagem fixa do contrato (spec.md), independente do texto
        // que o Cognito devolveria em ex.Message.
        return Result.Failure(AuthErrors.Validation(
            "Senha deve ter no mínimo 8 caracteres, com letra maiúscula, minúscula, número e símbolo."));
    }
}
```

### `Auth/Commands/ForgotPassword/ForgotPasswordCommand.cs` (Application)

```csharp
public sealed record ForgotPasswordCommand(string Email) : ICommand<Result>;

public sealed class ForgotPasswordCommandHandler : ICommandHandler<ForgotPasswordCommand, Result>
{
    private readonly IAuthService _authService;

    public ForgotPasswordCommandHandler(IAuthService authService) => _authService = authService;

    public ValueTask<Result> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken) =>
        new(_authService.ForgotPasswordAsync(command.Email, cancellationToken));
}

public sealed class ForgotPasswordCommandValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
    }
}
```

### `Auth/Commands/ResetPassword/ResetPasswordCommand.cs` (Application)

```csharp
public sealed record ResetPasswordCommand(string Email, string Code, string NewPassword, string? UserAgent)
    : ICommand<Result>;

public sealed class ResetPasswordCommandHandler : ICommandHandler<ResetPasswordCommand, Result>
{
    private readonly IAuthService _authService;
    private readonly IPasswordChangedEmailSender _emailSender;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IAuthService authService,
        IPasswordChangedEmailSender emailSender,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _authService = authService;
        _emailSender = emailSender;
        _logger = logger;
    }

    public async ValueTask<Result> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var result = await _authService.ConfirmForgotPasswordAsync(
            command.Email, command.Code, command.NewPassword, cancellationToken);

        if (result.IsFailure)
            return result;

        try
        {
            await _emailSender.SendAsync(command.Email, command.UserAgent, cancellationToken);
        }
        catch (Exception ex)
        {
            // Nunca propaga: a senha já foi trocada de fato no Cognito
            // (requisitos de negócio do spec.md) — falha no envio deste
            // email de aviso não pode derrubar a resposta de sucesso.
            // Mesma filosofia defensiva do AccountTriggerHandler (FEAT-19).
            _logger.LogError(ex, "Falha ao enviar email de senha alterada para {Email}.", command.Email);
        }

        return Result.Success();
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
        RuleFor(c => c.Code).NotEmpty().WithMessage("Código de recuperação é obrigatório.");
        RuleFor(c => c.NewPassword).NotEmpty().WithMessage("Nova senha é obrigatória.");
    }
}
```

Sem `MinimumLength`/regra de política no `NewPassword` — a política
completa (maiúscula+minúscula+número+símbolo) só é verificada de fato
pelo Cognito (`InvalidPasswordException` → `AuthErrors.Validation` em
`CognitoAuthService`, ver acima). Adicionar uma checagem parcial aqui
(ex.: só `MinimumLength(8)`, como o `RegisterUserCommandValidator` já
faz e cujo gap virou débito técnico) criaria duas fontes de verdade
divergentes da política real — decisão deliberada de não repetir esse
padrão nesta feature nova.

### `AuthErrors.cs` (Application)

```csharp
public static Error InvalidResetCode => Error.Validation(
    "invalid-reset-code", "Código de recuperação inválido.");

public static Error ExpiredResetCode => Error.Validation(
    "expired-reset-code", "Código de recuperação expirado.");
```

### `Common/Interfaces/IEmailSender.cs` (Application, novo)

```csharp
public interface IEmailSender
{
    Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default);
}
```

### `Common/Interfaces/IPasswordChangedEmailSender.cs` (Application, novo)

```csharp
public interface IPasswordChangedEmailSender
{
    Task SendAsync(string email, string? userAgent, CancellationToken cancellationToken = default);
}
```

### `Email/SesEmailService.cs` (Infrastructure, novo)

```csharp
public sealed class SesEmailService : IEmailSender
{
    private readonly IAmazonSimpleEmailServiceV2 _sesClient;
    private readonly SesOptions _options;

    public SesEmailService(IAmazonSimpleEmailServiceV2 sesClient, IOptions<SesOptions> options)
    {
        _sesClient = sesClient;
        _options = options.Value;
    }

    public Task SendAsync(string toEmail, string subject, string htmlBody, CancellationToken cancellationToken = default) =>
        _sesClient.SendEmailAsync(new SendEmailRequest
        {
            FromEmailAddress = _options.SenderEmail,
            Destination = new Destination { ToAddresses = [toEmail] },
            Content = new EmailContent
            {
                Simple = new Message
                {
                    Subject = new Content { Data = subject },
                    Body = new Body { Html = new Content { Data = htmlBody } }
                }
            }
        }, cancellationToken);
}
```

### `Email/SesPasswordChangedEmailSender.cs` (Infrastructure, novo)

```csharp
public sealed class SesPasswordChangedEmailSender : IPasswordChangedEmailSender
{
    private const string Subject = "Sua senha foi alterada — jrn.expenses"; // igual ao <title> do template

    private readonly IEmailSender _emailSender;

    public SesPasswordChangedEmailSender(IEmailSender emailSender) => _emailSender = emailSender;

    public Task SendAsync(string email, string? userAgent, CancellationToken cancellationToken = default)
    {
        var html = PasswordChangedEmailTemplateProvider.Template
            .Replace("{{email}}", email)
            .Replace("{{data}}", $"{DateTime.UtcNow:dd/MM/yyyy HH:mm} UTC")
            .Replace("{{dispositivo}}", string.IsNullOrWhiteSpace(userAgent) ? "Desconhecido" : userAgent);

        return _emailSender.SendAsync(email, Subject, html, cancellationToken);
    }
}
```

`{{data}}` em `dd/MM/yyyy HH:mm` — seguro sob `InvariantGlobalization`
(já ativo em `GastosApp.Api.csproj`) por não usar nome de mês/cultura,
só separadores literais. `{{dispositivo}}` = `User-Agent` cru da
request (sem parsing, spec.md) — `AuthEndpoints.ResetPassword` extrai o
header e passa pro Command.

### `Configuration/SesOptions.cs` (Infrastructure, novo)

```csharp
public sealed class SesOptions
{
    public const string SectionName = "Ses";
    public string SenderEmail { get; init; } = default!;
}
```

### `Extensions/InfraEmailExtensions.cs` (Infrastructure, novo)

Mesmo padrão de `InfraAuthExtensions.AddCognitoSdk` (leitura manual de
`IConfiguration`, sem `Configure<T>()`/reflection — AOT-safe):

```csharp
public static class InfraEmailExtensions
{
    public static IServiceCollection AddSesSdk(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ =>
        {
            var section = configuration.GetSection(SesOptions.SectionName);
            return Options.Create(new SesOptions { SenderEmail = section["SenderEmail"]! });
        });

        // Região: reaproveita CognitoOptions.Region (mesma região AWS
        // de todo o projeto) — sem seção própria só pra isso.
        services.AddSingleton<IAmazonSimpleEmailServiceV2>(sp =>
        {
            var region = sp.GetRequiredService<IOptions<CognitoOptions>>().Value.Region;
            return new AmazonSimpleEmailServiceV2Client(Amazon.RegionEndpoint.GetBySystemName(region));
        });

        services.AddScoped<IEmailSender, SesEmailService>();
        services.AddScoped<IPasswordChangedEmailSender, SesPasswordChangedEmailSender>();

        return services;
    }
}
```

Sem suporte a `ServiceURL`/credenciais locais (diferente de
`AddCognitoSdk`/`AddAwsInfrastructure`): LocalStack Community (usado no
ambiente local, FEAT-18) **não emula SES** — mesma limitação já
documentada em `backend/infra/CLAUDE.md` pra outros recursos fora do
free tier do LocalStack. Em dev local, `POST /auth/reset-password`
bem-sucedido loga a falha de envio (catch defensivo do Handler) e
segue normalmente — ver "Pontos que precisam de confirmação".

### `AuthEndpoints.cs` (Api)

```csharp
group.MapPost("/forgot-password", ForgotPassword)
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest);

group.MapPost("/reset-password", ResetPassword)
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest);

private static async Task<IResult> ForgotPassword(
    ForgotPasswordRequest request, ISender sender, CancellationToken cancellationToken)
{
    var command = new ForgotPasswordCommand(request.Email);
    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(Results.Ok);
}

private static async Task<IResult> ResetPassword(
    ResetPasswordRequest request, ISender sender, HttpContext httpContext, CancellationToken cancellationToken)
{
    var userAgent = httpContext.Request.Headers.UserAgent.ToString();
    var command = new ResetPasswordCommand(request.Email, request.Code, request.NewPassword, userAgent);
    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(Results.Ok);
}

public record ForgotPasswordRequest(string Email);
public record ResetPasswordRequest(string Email, string Code, string NewPassword);
```

### `AppJsonSerializerContext.cs` (Api)

```csharp
[JsonSerializable(typeof(ForgotPasswordRequest))]
[JsonSerializable(typeof(ResetPasswordRequest))]
```

## Recursos AWS

- **Nenhuma mudança de IAM** — reaproveita a statement `SesSendEmail`
  já concedida à Lambda da API principal na FEAT-33 (`lambda.tf`),
  escopada à identidade de domínio do próprio ambiente. `ForgotPassword`/
  `ConfirmForgotPassword` não exigem policy IAM própria (mesma categoria
  de `SignUp`/`ConfirmSignUp`, chamadas só com `ClientId`).
- **2 novos parâmetros no Parameter Store** (decisão tomada durante
  este `/plan`, ver "Pontos que precisam de confirmação" — spec.md
  original previa zero mudança de Terraform, mas não havia como o app
  saber o remetente SES sem isso):
  - Prod (`environments/prod/parameter-store.tf`):
    ```hcl
    resource "aws_ssm_parameter" "ses_sender_email" {
      name  = "/GastosApp/Ses/SenderEmail"
      type  = "String"
      value = aws_cognito_user_pool.main.email_configuration[0].from_email_address
    }
    ```
  - Hom (`environments/hom/parameter-store.tf`):
    ```hcl
    resource "aws_ssm_parameter" "ses_sender_email" {
      name  = "/GastosApp/Hom/Ses/SenderEmail"
      type  = "String"
      value = aws_cognito_user_pool.main.email_configuration[0].from_email_address
    }
    ```
  Recurso `String` padrão (não `SecureString`) — não é segredo, já é
  público no HTML do próprio email enviado. Custo zero (Parameter
  Store Standard, free tier), sem impacto de segurança. Reaproveita o
  valor já calculado pelo output `ses_sender_email` existente, só
  espelhando-o também como parâmetro (o output em si continua existindo,
  sem mudança).
- **Nenhuma outra mudança de Terraform** — sem novo domínio/identidade
  SES, sem mudança em `cognito.tf`, sem novo `lambda.tf`/IAM.
- **1 nova dependência de código** (não é recurso AWS): pacote NuGet
  `AWSSDK.SimpleEmailV2` em `GastosApp.Infrastructure.csproj` — mesma
  família de pacotes AWSSDK.* já usados no projeto (Cognito, DynamoDB,
  SSM), versão mais recente estável no momento da implementação.

## Mapeamento de erro

| Cenário | Exceção do SDK Cognito | `Error` | `ErrorType` | HTTP | `type` |
|---|---|---|---|---|---|
| `email` ausente (`/forgot-password`) | — (FluentValidation) | `validation-error` | `Validation` | 400 | `validation-error` |
| `email`/`code`/`newPassword` ausente (`/reset-password`) | — (FluentValidation) | `validation-error` | `Validation` | 400 | `validation-error` |
| Email inexistente/não confirmado (`/forgot-password`) | `UserNotFoundException` / `InvalidParameterException` | — (`Result.Success()`) | — | 200 | — |
| Código incorreto (`/reset-password`) | `CodeMismatchException` | `AuthErrors.InvalidResetCode` | `Validation` | 400 | `invalid-reset-code` |
| Email inexistente (`/reset-password`) | `UserNotFoundException` | `AuthErrors.InvalidResetCode` | `Validation` | 400 | `invalid-reset-code` |
| Código expirado (`/reset-password`) | `ExpiredCodeException` | `AuthErrors.ExpiredResetCode` | `Validation` | 400 | `expired-reset-code` |
| Senha fora da política (`/reset-password`) | `InvalidPasswordException` | `AuthErrors.Validation(msg)` | `Validation` | 400 | `bad-request` |
| Exceção verdadeiramente inesperada (qualquer endpoint) | outras | não tratada, propaga | — | 500 | `internal-server-error` |

`POST /auth/login` não muda.

## Decisões técnicas relevantes

1. **Orquestração no Handler, não na Infrastructure**: diferente da
   FEAT-35 (onde os dois Handlers eram repasse puro), o
   `ResetPasswordCommandHandler` orquestra duas chamadas — `IAuthService`
   (crítica, propaga falha) e `IPasswordChangedEmailSender` (best-effort,
   nunca propaga). `CognitoAuthService` continua sem saber nada sobre
   email — mantém a mesma responsabilidade única de sempre (só Cognito).
2. **`IEmailSender` genérico + `IPasswordChangedEmailSender` específico**:
   duas camadas em vez de uma só, mesmo já sabendo que só esta feature
   usa isso hoje — decisão justificada porque a FEAT-33 (já implementada)
   nomeia explicitamente FEAT-36 e FEAT-37 como os dois consumidores
   previstos do envio direto via SES; `SesEmailService` fica genérico o
   bastante pra FEAT-37 reaproveitar sem mudança, só implementando seu
   próprio `I<Algo>EmailSender` específico por cima.
3. **Mensagem de erro de senha fraca fixa no código**, não repassando
   `ex.Message` do SDK do Cognito — mesmo padrão de
   `AuthErrors.EmailAlreadyExists` (não vaza detalhe interno do SDK),
   e bate exatamente com o exemplo já fechado no contrato do spec.md.
4. **Sem `MinimumLength`/regra de política no `ResetPasswordCommandValidator`**
   — decisão técnica 3 acima; evita duplicar/divergir da política real
   do Cognito, mesmo problema que gerou o débito técnico já registrado
   pro `RegisterUserCommandValidator`.
5. **`{{dispositivo}}` = `User-Agent` cru**, sem parsing de
   navegador/SO — fora do escopo (spec.md), débito já registrado no
   backlog pra refinar se o usuário quiser no futuro.
6. **2 novos parâmetros SSM `String`** (não `SecureString`) — decisão
   tomada e confirmada com o usuário durante este `/plan` (ver acima,
   "Recursos AWS"), pequena exceção à linha "nenhuma mudança de
   Terraform" do spec.md original (que foi escrita pensando só em IAM).

## Testes a criar

**Unit (`GastosApp.UnitTests/Infrastructure/CognitoAuthServiceTests.cs`)** —
mesmo padrão dos testes já existentes (mock de
`IAmazonCognitoIdentityProvider` via NSubstitute):
- `ForgotPasswordAsync_ShouldSucceed_WhenCognitoCallSucceeds`
- `ForgotPasswordAsync_ShouldSucceed_WhenCognitoThrowsUserNotFoundException`
- `ForgotPasswordAsync_ShouldSucceed_WhenCognitoThrowsInvalidParameterException`
- `ConfirmForgotPasswordAsync_ShouldSucceed_WhenCognitoCallSucceeds`
- `ConfirmForgotPasswordAsync_ShouldReturnExpiredResetCode_WhenCognitoThrowsExpiredCodeException`
- `ConfirmForgotPasswordAsync_ShouldReturnInvalidResetCode_WhenCognitoThrowsCodeMismatchException`
- `ConfirmForgotPasswordAsync_ShouldReturnInvalidResetCode_WhenCognitoThrowsUserNotFoundException`
- `ConfirmForgotPasswordAsync_ShouldReturnValidationError_WhenCognitoThrowsInvalidPasswordException`

**Unit novo (`GastosApp.UnitTests/Infrastructure/SesPasswordChangedEmailSenderTests.cs`)** —
mock de `IEmailSender` via NSubstitute:
- `SendAsync_ShouldCallEmailSender_WithSubjectAndFilledTemplate`
- `SendAsync_ShouldUseFallbackDevice_WhenUserAgentIsNullOrEmpty` (Theory:
  `null`, `""`)

**Componente (`GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs`)** —
mock de `IAuthService`/`IPasswordChangedEmailSender` direto, mesmo
padrão de `Confirm_*`/`ResendConfirmation_*`:
- `ForgotPassword_ComEmailValido_Retorna200SemCorpo`
- `ForgotPassword_ComEmailVazio_Retorna400SemChamarAuthService`
- `ResetPassword_ComParametrosCorretos_Retorna200EEnviaEmail`
- `ResetPassword_ComParametrosInvalidos_Retorna400SemChamarAuthService`
  (Theory: email vazio, code vazio, newPassword vazio)
- `ResetPassword_QuandoAuthServiceRetornaErro_PropagaProblemDetails`
  (Theory: `InvalidResetCode` → 400, `ExpiredResetCode` → 400,
  `Validation` → 400)
- `ResetPassword_QuandoEmailFalha_AindaAssimRetorna200` — mock de
  `IPasswordChangedEmailSender.SendAsync` lançando exceção, espera 200
  mesmo assim (cobre o critério de aceite "falha no envio não impede
  sucesso")

**Integrado (`GastosApp.IntegrationTests/Auth/AuthFlowTests.cs`)** —
mesma limitação já aceita na FEAT-35 (sem acesso ao código real
enviado por email):
- `ForgotPassword_EmailDeContaExistente_Retorna200` — reusa
  `TestAccountFixture.CreateAsync()`, chama
  `POST /auth/forgot-password`, espera 200 (não valida o email
  recebido, só o contrato HTTP).
- `ForgotPassword_EmailInexistente_Retorna200` — email inexistente,
  espera 200 igualmente.
- `ResetPassword_CodigoIncorreto_Retorna400` — conta existente
  (`TestAccountFixture`), código claramente inválido, espera 400
  `invalid-reset-code`.
- `ResetPassword_EmailInexistente_Retorna400` — sem fixture, espera 400
  `invalid-reset-code` (mesma resposta).
- `ResetPassword_SenhaForaDaPolitica_Retorna400` — conta existente,
  código incorreto propositalmente **não** é o obstáculo aqui: como não
  há como obter o código real, este teste precisa de outra estratégia —
  ver "Pontos que precisam de confirmação", item 2.
  Cobrir "código correto → senha trocada → 200 → login com nova senha"
  de ponta a ponta fica fora do escopo (mesma limitação da FEAT-35).

## Documentação a atualizar

- `backend/docs/openapi.json` — regenerar via
  `backend/scripts/export-openapi.sh` refletindo os dois novos
  endpoints.
- `frontend/design-system/emails/03-senha-alterada.html` — remover a
  dependência de `{{nome}}` (decisão 4 do spec.md): trocar
  `"Olá, {{nome}}. A senha da conta {{email}} foi redefinida com sucesso."`
  por `"A senha da conta {{email}} foi redefinida com sucesso."`.
- `backend/src/GastosApp.Infrastructure/Email/Templates/03-senha-alterada.html`
  — cópia já com o texto ajustado (criada nesta feature, não existe
  hoje).
- `backend/infra/CLAUDE.md` — pequena nota sobre os 2 novos parâmetros
  `Ses/SenderEmail` (mesmo padrão das seções já existentes de
  Cognito/CORS no Parameter Store).
- `backend/docs/data-model.md` — sem mudança (nenhum item novo no
  DynamoDB).

## Pontos que precisam de confirmação antes do `/tasks`

1. **2 novos parâmetros SSM `String`** (`Ses/SenderEmail` por
   ambiente) — já perguntado e confirmado durante este `/plan`
   (opção recomendada aceita). Sinalizado aqui só pra registro formal
   antes do `/tasks`, já que ajusta uma linha do spec.md aprovado.
2. **Cobertura de teste integrado para "senha fora da política"**: como
   não há acesso ao código real de recuperação na suíte de integração,
   o teste `ResetPassword_SenhaForaDaPolitica_Retorna400` só é viável
   se o Cognito validar a senha **antes** de checar o código (ordem de
   validação interna do `ConfirmForgotPassword` não documentada
   oficialmente pela AWS). Se o Cognito validar o código primeiro, esse
   teste específico não é viável na suíte de integração (fica só como
   teste unitário/componente) — confirmar contra Cognito real de
   homologação durante a implementação; não bloqueia o restante do
   plano.
3. **Nomes dos dois novos códigos de erro** (`invalid-reset-code`,
   `expired-reset-code`), dos métodos de `IAuthService`
   (`ForgotPasswordAsync`, `ConfirmForgotPasswordAsync`) e das novas
   interfaces (`IEmailSender`, `IPasswordChangedEmailSender`) — sem
   ocorrência prévia no código, vale uma checada rápida antes do
   `/tasks`.
4. **Formato de `{{data}}`** (`dd/MM/yyyy HH:mm` + sufixo `"UTC"`
   literal) — não fechado com o usuário, é só uma proposta razoável;
   confirmar se o sufixo "UTC" é desejado ou se deve ficar sem indicação
   de fuso.
