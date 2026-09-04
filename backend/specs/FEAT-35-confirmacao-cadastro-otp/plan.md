# Plan — FEAT-35: Confirmação de cadastro via código (OTP)

## Camadas afetadas

- **Application** — duas novas pastas de comando, seguindo o padrão já
  usado por `Commands/Login`, `Commands/Logout`, `Commands/Refresh`,
  `Commands/Register` (uma pasta por comando, `record` + `Handler` +
  `Validator` no mesmo namespace):
  - `Auth/Commands/Confirm/` — `ConfirmSignUpCommand` (+ Handler +
    Validator)
  - `Auth/Commands/ResendConfirmation/` — `ResendConfirmationCodeCommand`
    (+ Handler + Validator)
  - `AuthErrors.cs` ganha `InvalidConfirmationCode` e
    `ExpiredConfirmationCode`
  - `IAuthService.cs` ganha dois métodos novos
- **Infrastructure** — `CognitoAuthService.cs` implementa os dois
  métodos novos, chamando `ConfirmSignUpAsync`/`ResendConfirmationCodeAsync`
  do SDK do Cognito e mapeando exceções pra `Result`/`AuthErrors`, no
  mesmo padrão já usado por `RegisterAsync`/`LoginAsync`/`RefreshAsync`.
- **Api** — `AuthEndpoints.cs` ganha `POST /auth/confirm` e
  `POST /auth/resend-confirmation` + os records `ConfirmRequest`/
  `ResendConfirmationRequest`. `AppJsonSerializerContext.cs` ganha
  `[JsonSerializable]` pros dois requests (nenhum DTO de resposta é
  necessário — sucesso é sempre 200 sem corpo, decisão 4 do spec.md).
- **Domain** — sem mudança. Nenhuma entidade nova, nenhuma regra de
  negócio pura envolvida — é só orquestração de chamada ao Cognito.

## Contratos técnicos

### `IAuthService.cs` (Application)

```csharp
public interface IAuthService
{
    Task<Result<RegisterResult>> RegisterAsync(string email, string password, string name, CancellationToken cancellationToken = default);
    Task<Result<LoginResult>> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
    Task<Result<RefreshResult>> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task DeleteAsync(string email, CancellationToken cancellationToken = default);

    // NOVO (FEAT-35)
    Task<Result> ConfirmSignUpAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<Result> ResendConfirmationCodeAsync(string email, CancellationToken cancellationToken = default);
}
```

`Result` (não `Result<T>`) — mesmo padrão de `LogoutCommand`: nenhum
dos dois fluxos devolve dado algum em caso de sucesso (spec.md, decisão
4).

### `CognitoAuthService.cs` (Infrastructure)

```csharp
public async Task<Result> ConfirmSignUpAsync(
    string email, string code, CancellationToken cancellationToken = default)
{
    try
    {
        await _cognitoClient.ConfirmSignUpAsync(new ConfirmSignUpRequest
        {
            ClientId = _options.ClientId,
            Username = email,
            ConfirmationCode = code
        }, cancellationToken);

        return Result.Success();
    }
    catch (ExpiredCodeException)
    {
        return Result.Failure(AuthErrors.ExpiredConfirmationCode);
    }
    catch (CodeMismatchException)
    {
        return Result.Failure(AuthErrors.InvalidConfirmationCode);
    }
    catch (UserNotFoundException)
    {
        // Decisão 1 (spec.md): mesma resposta de código incorreto —
        // não revela se o email está cadastrado.
        return Result.Failure(AuthErrors.InvalidConfirmationCode);
    }
    catch (NotAuthorizedException)
    {
        // Cognito recusa ConfirmSignUp de usuário já confirmado com
        // "User cannot be confirmed. Current status is CONFIRMED" —
        // único cenário realista de NotAuthorizedException nesta
        // chamada (não há senha/token envolvido). Decisão 2 (spec.md):
        // idempotente, não é erro.
        return Result.Success();
    }
}

public async Task<Result> ResendConfirmationCodeAsync(
    string email, CancellationToken cancellationToken = default)
{
    try
    {
        await _cognitoClient.ResendConfirmationCodeAsync(new ResendConfirmationCodeRequest
        {
            ClientId = _options.ClientId,
            Username = email
        }, cancellationToken);
    }
    catch (UserNotFoundException)
    {
        // Decisão 3 (spec.md): não revela se o email existe.
    }
    catch (InvalidParameterException)
    {
        // Cognito recusa reenvio pra usuário já confirmado com esse
        // tipo de exceção — mesmo princípio de não-enumeração.
    }

    // Decisão 3: sempre 200. Qualquer exceção fora das duas acima
    // (ex.: LimitExceededException do throttling nativo do Cognito) é
    // verdadeiramente inesperada aqui e propaga pro GlobalExceptionHandler
    // (500), igual ao resto da API.
    return Result.Success();
}
```

### `Auth/Commands/Confirm/ConfirmSignUpCommand.cs` (Application)

```csharp
public sealed record ConfirmSignUpCommand(string Email, string Code) : ICommand<Result>;

public sealed class ConfirmSignUpCommandHandler : ICommandHandler<ConfirmSignUpCommand, Result>
{
    private readonly IAuthService _authService;

    public ConfirmSignUpCommandHandler(IAuthService authService) => _authService = authService;

    public ValueTask<Result> Handle(ConfirmSignUpCommand command, CancellationToken cancellationToken) =>
        new(_authService.ConfirmSignUpAsync(command.Email, command.Code, cancellationToken));
}

public sealed class ConfirmSignUpCommandValidator : AbstractValidator<ConfirmSignUpCommand>
{
    public ConfirmSignUpCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
        RuleFor(c => c.Code).NotEmpty().WithMessage("Código de confirmação é obrigatório.");
    }
}
```

### `Auth/Commands/ResendConfirmation/ResendConfirmationCodeCommand.cs` (Application)

```csharp
public sealed record ResendConfirmationCodeCommand(string Email) : ICommand<Result>;

public sealed class ResendConfirmationCodeCommandHandler : ICommandHandler<ResendConfirmationCodeCommand, Result>
{
    private readonly IAuthService _authService;

    public ResendConfirmationCodeCommandHandler(IAuthService authService) => _authService = authService;

    public ValueTask<Result> Handle(ResendConfirmationCodeCommand command, CancellationToken cancellationToken) =>
        new(_authService.ResendConfirmationCodeAsync(command.Email, cancellationToken));
}

public sealed class ResendConfirmationCodeCommandValidator : AbstractValidator<ResendConfirmationCodeCommand>
{
    public ResendConfirmationCodeCommandValidator()
    {
        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");
    }
}
```

Os dois handlers são só um repasse (`Handle` chama direto o
`IAuthService` e devolve o `Result`) — não há orquestração adicional,
diferente de `RegisterUserCommandHandler`/`LoginUserCommandHandler`.
Toda a lógica de negócio (mapeamento de exceção, idempotência,
não-enumeração) fica na Infrastructure, e a validação de
presença/formato fica no `Validator` — o Handler não tem nenhum `if`
(constitution: "Handlers não devem conter validação manual").

### `AuthErrors.cs` (Application)

```csharp
public static Error InvalidConfirmationCode => Error.Validation(
    "invalid-confirmation-code", "Código de confirmação inválido.");

public static Error ExpiredConfirmationCode => Error.Validation(
    "expired-confirmation-code", "Código de confirmação expirado.");
```

`ErrorType.Validation` (não um tipo novo) — `ResultHttpExtensions.BuildProblem`
já mapeia `Validation` pra 400 com `title` fixo "Parâmetros inválidos",
mesmo status usado pelo `validation-error` do `ValidationBehavior`
(spec.md já documenta os dois com título idêntico, `type` diferente).

### `AuthEndpoints.cs` (Api)

```csharp
group.MapPost("/confirm", ConfirmSignUp)
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest);

group.MapPost("/resend-confirmation", ResendConfirmation)
    .Produces(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest);

private static async Task<IResult> ConfirmSignUp(
    ConfirmRequest request, ISender sender, CancellationToken cancellationToken)
{
    var command = new ConfirmSignUpCommand(request.Email, request.Code);
    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(Results.Ok);
}

private static async Task<IResult> ResendConfirmation(
    ResendConfirmationRequest request, ISender sender, CancellationToken cancellationToken)
{
    var command = new ResendConfirmationCodeCommand(request.Email);
    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(Results.Ok);
}

public record ConfirmRequest(string Email, string Code);
public record ResendConfirmationRequest(string Email);
```

`result.ToHttpResult(Results.Ok)` usa o overload não-genérico já
existente em `ResultHttpExtensions` (mesmo usado por `Logout`), que
chama `Results.Ok()` sem corpo em caso de sucesso.

### `AppJsonSerializerContext.cs` (Api)

```csharp
[JsonSerializable(typeof(ConfirmRequest))]
[JsonSerializable(typeof(ResendConfirmationRequest))]
```

## Recursos AWS

**Nenhum recurso AWS novo ou alterado.** `ConfirmSignUp` e
`ResendConfirmationCode` já são APIs padrão do App Client do User Pool
existente (mesmo `ClientId` já usado por `SignUp`/`InitiateAuth`) — sem
mudança em `backend/infra/terraform/`, sem novo parâmetro no Parameter
Store, sem mudança de TTL/política do User Pool (spec.md, "Fora do
escopo").

## Mapeamento de erro

| Cenário | Exceção do SDK Cognito | `Error` | `ErrorType` | HTTP | `type` |
|---|---|---|---|---|---|
| `email`/`code` ausente | — (FluentValidation, `ValidationBehavior`) | `validation-error` | `Validation` | 400 | `validation-error` |
| Código incorreto | `CodeMismatchException` | `AuthErrors.InvalidConfirmationCode` | `Validation` | 400 | `invalid-confirmation-code` |
| Email inexistente (`/confirm`) | `UserNotFoundException` | `AuthErrors.InvalidConfirmationCode` | `Validation` | 400 | `invalid-confirmation-code` |
| Código expirado | `ExpiredCodeException` | `AuthErrors.ExpiredConfirmationCode` | `Validation` | 400 | `expired-confirmation-code` |
| Usuário já confirmado (`/confirm`) | `NotAuthorizedException` | — (`Result.Success()`) | — | 200 | — |
| Email inexistente/já confirmado (`/resend-confirmation`) | `UserNotFoundException` / `InvalidParameterException` | — (`Result.Success()`) | — | 200 | — |
| Exceção verdadeiramente inesperada (qualquer endpoint) | outras (ex.: `LimitExceededException`) | não tratada, propaga | — | 500 | `internal-server-error` (`GlobalExceptionHandler`) |

`POST /auth/login` não muda: `user-not-confirmed` (401) continua
exatamente como hoje.

## Decisões técnicas relevantes

1. **Onde vive o mapeamento de exceção → `Result`**: na Infrastructure
   (`CognitoAuthService`), não na Application — mesmo padrão já usado
   por `RegisterAsync`/`LoginAsync`/`RefreshAsync`. Os Handlers de
   `ConfirmSignUpCommand`/`ResendConfirmationCodeCommand` ficam como um
   repasse direto ao `IAuthService`, sem lógica própria.
2. **`Result` não-genérico nos dois novos métodos de `IAuthService`** —
   nenhum dos dois fluxos tem dado a devolver em caso de sucesso
   (spec.md, decisão 4), mesmo padrão de `LogoutCommand`.
3. **`NotAuthorizedException` em `ConfirmSignUpAsync`** é tratada como
   "já confirmado" sem checar o texto da mensagem — é a única causa
   realista dessa exceção nesta chamada específica (não há senha nem
   token envolvidos em `ConfirmSignUp`, diferente de `InitiateAuth`
   onde o mesmo tipo de exceção também cobre credenciais inválidas).
4. **Suposição a validar na implementação**: que `ResendConfirmationCode`
   para um usuário já confirmado lança `InvalidParameterException`
   (comportamento documentado informalmente pela comunidade/SDK, não
   encontrado em teste automatizado próprio até agora). Validar contra
   `cognito-local` e, pelo menos uma vez, contra Cognito real em
   homologação durante a implementação — se o tipo de exceção real for
   outro, ajustar o `catch` de acordo (o efeito observável, 200 sempre,
   não muda; só o `catch` específico).
5. **Cobertura de teste integrado do fluxo de sucesso real**: a suíte
   de integração não tem acesso ao código de 6 dígitos de fato enviado
   por email (`TestAccountFixture` já confirma usuários via
   `AdminConfirmSignUpAsync`, que não passa pelo código real — ver
   plan.md da FEAT-29/32). Path de sucesso 200 sem código real:
   - `POST /auth/confirm` para uma conta **já confirmada** (criada via
     `TestAccountFixture`, com qualquer `code`) — exercita de fato o
     branch de idempotência (`NotAuthorizedException` → 200) contra
     Cognito real, sem precisar do código verdadeiro.
   - `POST /auth/resend-confirmation` para uma conta ainda não
     confirmada (registrada sem passar por `AdminConfirmSignUpAsync`)
     — exercita `ResendConfirmationCode` de verdade contra Cognito
     real, sem precisar ler o email resultante.
   - Código incorreto/email inexistente em `/auth/confirm` (400) também
     são plenamente testáveis sem o código real.
   Cobrir "código correto → 200" de ponta a ponta (exigiria capturar o
   código do email de fato) fica fora do escopo desta feature, mesmo
   espírito de limitação já aceito na FEAT-31 (plan.md, "Testes
   integrado: nenhuma mudança necessária").

## Testes a criar

**Unit (`GastosApp.UnitTests/Infrastructure/CognitoAuthServiceTests.cs`)** —
onde vive o mapeamento de exceção, mesmo padrão dos testes já
existentes de `RegisterAsync`/`LoginAsync` (mock de
`IAmazonCognitoIdentityProvider` via NSubstitute):
- `ConfirmSignUpAsync_ShouldSucceed_WhenCognitoCallSucceeds`
- `ConfirmSignUpAsync_ShouldReturnExpiredConfirmationCode_WhenCognitoThrowsExpiredCodeException`
- `ConfirmSignUpAsync_ShouldReturnInvalidConfirmationCode_WhenCognitoThrowsCodeMismatchException`
- `ConfirmSignUpAsync_ShouldReturnInvalidConfirmationCode_WhenCognitoThrowsUserNotFoundException`
- `ConfirmSignUpAsync_ShouldSucceed_WhenCognitoThrowsNotAuthorizedException` (idempotência)
- `ResendConfirmationCodeAsync_ShouldSucceed_WhenCognitoCallSucceeds`
- `ResendConfirmationCodeAsync_ShouldSucceed_WhenCognitoThrowsUserNotFoundException`
- `ResendConfirmationCodeAsync_ShouldSucceed_WhenCognitoThrowsInvalidParameterException`

**Componente (`GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs`)** —
mock de `IAuthService` direto, mesmo padrão de `Register_*`/`Login_*`:
- `Confirm_ComCodigoCorreto_Retorna200SemCorpo`
- `Confirm_ComParametrosInvalidos_Retorna400SemChamarAuthService` (Theory:
  email vazio, code vazio)
- `Confirm_QuandoAuthServiceRetornaErro_PropagaProblemDetails` (Theory:
  `InvalidConfirmationCode` → 400, `ExpiredConfirmationCode` → 400)
- `ResendConfirmation_ComEmailValido_Retorna200SemCorpo`
- `ResendConfirmation_ComEmailVazio_Retorna400SemChamarAuthService`

**Integrado (`GastosApp.IntegrationTests/Auth/AuthFlowTests.cs`)** —
ver decisão técnica 5:
- `Confirm_UsuarioJaConfirmado_Retorna200Idempotente` — reusa
  `TestAccountFixture.CreateAsync()` (já confirmado), chama
  `POST /auth/confirm` com um código qualquer, espera 200.
- `Confirm_CodigoIncorreto_Retorna400` — mesma fixture, código
  claramente inválido (ex.: `"000000"`), espera 400
  `invalid-confirmation-code`.
- `Confirm_EmailInexistente_Retorna400` — sem fixture, email
  inexistente, espera 400 `invalid-confirmation-code` (mesma resposta).
- `ResendConfirmation_UsuarioNaoConfirmado_Retorna200` — registra uma
  conta nova via `POST /auth/register` **sem** confirmar (não usa
  `TestAccountFixture` inteira, só o registro), chama
  `POST /auth/resend-confirmation`, espera 200; limpeza manual do
  usuário Cognito criado (`AdminDeleteUserAsync`) no `finally`, já que
  não há login pra abrir uma `TestAccountFixture` completa.

## Documentação a atualizar

- `backend/docs/openapi.json` — regenerar via
  `backend/scripts/export-openapi.sh` refletindo os dois novos
  endpoints (constitution: toda mudança de contrato exige isso como
  critério de aceite).
- `backend/docs/backlog.md` — item da FEAT-35 já será movido/atualizado
  conforme convenção do backlog quando a feature for dada por
  concluída; nenhuma ação adicional aqui além do débito já anotado
  sobre a FEAT-36 (ver histórico do `/specify`).
- `backend/docs/data-model.md` — sem mudança (nenhum item novo no
  DynamoDB).

## Pontos que precisam de confirmação antes do `/tasks`

1. **Suposição não validada** (decisão técnica 4): que
   `ResendConfirmationCode` para usuário já confirmado lança
   `InvalidParameterException` no Cognito real — só será confirmado
   durante a implementação/testes. Se o comportamento real for
   diferente, o `catch` correspondente muda, mas o efeito observável
   (200 sempre) e o restante do plano não são afetados.
2. **Escopo de teste integrado do fluxo de sucesso** (decisão técnica
   5): sem cobrir "código correto → 200" de ponta a ponta (não há como
   capturar o código real enviado por email na suíte atual) — cobertura
   fica no idempotente-200 (`NotAuthorizedException`) e no reenvio
   bem-sucedido contra Cognito real. Confirmar que isso satisfaz "pelo
   menos o fluxo de sucesso" exigido pela constitution.
3. Nomes dos dois novos códigos de erro (`invalid-confirmation-code`,
   `expired-confirmation-code`) e dos métodos de `IAuthService`
   (`ConfirmSignUpAsync`, `ResendConfirmationCodeAsync`) — sem
   ocorrência prévia no código, mas vale uma checada rápida antes do
   `/tasks`.
