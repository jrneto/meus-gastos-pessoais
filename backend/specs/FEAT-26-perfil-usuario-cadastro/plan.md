# Plan: FEAT-26 — Perfil do usuário no cadastro (nome, telefone, CPF) — Plano Técnico

## Contexto técnico

`spec.md` fecha: `POST /auth/register` passa a exigir `name`,
`phoneNumber` e `cpf` além de `email`/`password`; `GET /auth/me` passa
a retorná-los. Perfil vive inteiramente no DynamoDB (novo item por
usuário) — **nenhuma mudança no Cognito User Pool** (schema/atributos),
decisão já fechada no `/specify` pra não exigir recriar o pool de
produção. CPF único entre usuários, validado por dígito verificador;
telefone só dígitos (10 ou 11). Falha ao gravar o perfil depois do
`SignUp` no Cognito já ter sido concluído reverte o cadastro por
completo (usuário removido do Cognito).

**Decisões técnicas não óbvias a partir do `spec.md`:**

1. **Novo item DynamoDB `UserProfile`** (`PK=USER#<userId>`,
   `SK=PROFILE#`) — mesma tabela `GastosApp`, sem GSI novo (acesso
   sempre por `GetItem`/`PutItem` direto pela chave, nunca `Query`).
2. **Unicidade de CPF via item-sentinela `CpfPointer`**
   (`PK=CPF#<cpf>`, `SK=CPF#`), no mesmo espírito do `AccountPointer`
   já usado por `DynamoDbAccountRepository` (FEAT-19): `PutItem` com
   `ConditionExpression: attribute_not_exists(PK)` dentro de um
   `TransactWriteItems` junto com o `UserProfile`, sem precisar de
   `Scan` nem de GSI dedicado. Diferente do `AccountPointer`, aqui não
   há corrida a reconciliar (cada CPF só é gravado uma vez, no
   registro) — um `ConditionalCheckFailed` é sempre um conflito real
   (409), nunca um "vencedor" a recuperar.
3. **`RegisterUserCommand` migra a validação manual (`if`) já existente
   pra um `RegisterUserCommandValidator` (FluentValidation) dedicado.**
   Não é escopo estrito do pedido do usuário, mas é exigido pela
   constitution ("Validação via Pipeline Behavior... Handlers não devem
   conter validação manual de entrada") — como o Handler está sendo
   tocado pra adicionar validação de `name`/`phoneNumber`/`cpf`, as
   regras de `email`/`password` já existentes migram junto pro mesmo
   validator, em vez de acumular mais `if` ao lado das novas regras.
   `LoginUserCommand`/`RefreshTokenCommand` (que têm o mesmo débito) não
   são tocados — fora do escopo desta feature.
4. **`Cpf.IsValid` (algoritmo de dígito verificador) vive no Domain**
   (`GastosApp.Domain.Users.Cpf`), não no validator — é regra de
   negócio pura (sem I/O), mesmo padrão já usado por `CategorySlug`
   (`GastosApp.Domain.Categories`), reaproveitável fora do contexto de
   FluentValidation se um dia for preciso.
5. **Rollback via `IAuthService.DeleteAsync` (novo método,
   `AdminDeleteUser` do Cognito) — exige nova permissão IAM no papel da
   Lambda.** Ver seção "Recursos AWS": esse é o único ponto desta
   feature que toca infraestrutura, e precisa de aprovação explícita
   antes do `/tasks` (regra do projeto pra qualquer mudança de
   segurança em recurso AWS).
6. **`GET /auth/me` deixa de ler `name` só das claims do JWT e passa a
   consultar o `UserProfile` via Mediator** (`GetCurrentUserQuery`,
   novo) — hoje o endpoint não usa `ISender` (lê `ClaimsPrincipal`
   direto), o que já era uma exceção ao padrão do resto do projeto
   ("rotas só fazem `sender.Send`"); como agora precisa de acesso a
   dado (DynamoDB), a Api não pode chamar o repositório diretamente
   (Api → Application → Infrastructure), então o endpoint passa a
   seguir o mesmo padrão de todas as outras rotas autenticadas.
   `userId`/`email` continuam extraídos das claims (nunca do body),
   só passam a ser parâmetros da query em vez de montarem a resposta
   direto no endpoint.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Novo `GastosApp.Domain.Users.Cpf` (validador de dígito verificador, estático, sem estado) e `GastosApp.Domain.Users.UserProfile` (entidade — `Create`/`Restore`, mesmo padrão de `Account`) |
| Application | `RegisterUserCommand`/`Handler` ganham `Name`/`PhoneNumber`/`Cpf` e passam a usar `IUserProfileRepository`; novo `RegisterUserCommandValidator`; novo `GetCurrentUserQuery`/`Handler` (`Auth/Queries/GetCurrentUser/`); nova interface `IUserProfileRepository`; `IAuthService` ganha `DeleteAsync`; `AuthErrors` ganha `CpfAlreadyExists` |
| Infrastructure | Novo `DynamoDbUserProfileRepository` (implementa `IUserProfileRepository`); `CognitoAuthService` implementa `DeleteAsync` (`AdminDeleteUserAsync`) |
| Api | `AuthEndpoints.cs`: `RegisterRequest`/`UserInfoResponse` ganham os 3 campos; `RegisterUser` repassa os novos campos ao command; `UserData` (`GET /me`) passa a chamar `sender.Send(GetCurrentUserQuery)` em vez de ler só claims |
| AWS/Terraform | **Nova permissão IAM** (`cognito-idp:AdminDeleteUser`) no papel da Lambda, em `lambda.tf` de `prod` e `hom` — único recurso/infra afetado, precisa de aprovação explícita (ver "Recursos AWS") |

## Domain-layer

### `Cpf` (`GastosApp.Domain/Users/Cpf.cs`, novo)

```csharp
namespace GastosApp.Domain.Users;

// Validação por dígito verificador (algoritmo oficial) — regra de negócio
// pura, sem I/O. A checagem de unicidade entre usuários não é
// responsabilidade deste tipo (fica no IUserProfileRepository/Infrastructure).
public static class Cpf
{
    public static bool IsValid(string digits)
    {
        if (digits.Length != 11 || !digits.All(char.IsDigit))
            return false;

        // Sequências com todos os dígitos iguais "fecham" a conta do dígito
        // verificador mas nunca são CPFs reais — regra padrão de validação no Brasil.
        if (digits.Distinct().Count() == 1)
            return false;

        var numbers = digits.Select(c => c - '0').ToArray();

        return numbers[9] == CalculateCheckDigit(numbers, 9)
            && numbers[10] == CalculateCheckDigit(numbers, 10);
    }

    private static int CalculateCheckDigit(int[] numbers, int length)
    {
        var sum = 0;
        for (var i = 0; i < length; i++)
            sum += numbers[i] * (length + 1 - i);

        var remainder = sum % 11;
        return remainder < 2 ? 0 : 11 - remainder;
    }
}
```

### `UserProfile` (`GastosApp.Domain/Users/UserProfile.cs`, novo)

```csharp
namespace GastosApp.Domain.Users;

public sealed class UserProfile
{
    public string UserId { get; }
    public string Name { get; }
    public string PhoneNumber { get; }
    public string Cpf { get; }
    public DateTimeOffset CreatedAt { get; }

    private UserProfile(string userId, string name, string phoneNumber, string cpf, DateTimeOffset createdAt)
    {
        UserId = userId;
        Name = name;
        PhoneNumber = phoneNumber;
        Cpf = cpf;
        CreatedAt = createdAt;
    }

    public static UserProfile Create(string userId, string name, string phoneNumber, string cpf) =>
        new(userId, name, phoneNumber, cpf, DateTimeOffset.UtcNow);

    public static UserProfile Restore(string userId, string name, string phoneNumber, string cpf, DateTimeOffset createdAt) =>
        new(userId, name, phoneNumber, cpf, createdAt);
}
```

## Application-layer

### `IUserProfileRepository` (`Common/Interfaces/IUserProfileRepository.cs`, novo)

```csharp
using GastosApp.Domain.Users;

namespace GastosApp.Application.Common.Interfaces;

public sealed record CreateUserProfileResult(bool CpfAlreadyExists);

public interface IUserProfileRepository
{
    Task<CreateUserProfileResult> CreateAsync(UserProfile profile, CancellationToken cancellationToken = default);
    Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
```

### `IAuthService` — novo método (`Common/Interfaces/IAuthService.cs`)

```csharp
Task DeleteAsync(string email, CancellationToken cancellationToken = default); // rollback de SignUp (spec.md, US8)
```

### `AuthErrors` — novo erro

```csharp
public static Error CpfAlreadyExists => Error.Conflict("cpf-already-exists", "CPF já cadastrado");
```

### `RegisterUserCommand` (`Auth/Commands/Register/RegisterUserCommand.cs`, reescrito)

```csharp
public sealed record RegisterUserCommand(string Email, string Password, string Name, string PhoneNumber, string Cpf)
    : ICommand<Result<RegisterUserResult>>;

public sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand, Result<RegisterUserResult>>
{
    private readonly IAuthService _authService;
    private readonly IUserProfileRepository _userProfileRepository;

    public RegisterUserCommandHandler(IAuthService authService, IUserProfileRepository userProfileRepository)
    {
        _authService = authService;
        _userProfileRepository = userProfileRepository;
    }

    public async ValueTask<Result<RegisterUserResult>> Handle(RegisterUserCommand command, CancellationToken cancellationToken)
    {
        // Validação de formato (email/password/name/phoneNumber/cpf) já rodou no
        // ValidationBehavior via RegisterUserCommandValidator — Handle fica só com
        // orquestração (constitution: "Handlers não devem conter validação manual").
        var authResult = await _authService.RegisterAsync(command.Email, command.Password, cancellationToken);
        if (authResult.IsFailure)
            return Result.Failure<RegisterUserResult>(authResult.Error!);

        var profile = UserProfile.Create(authResult.Value.UserId, command.Name.Trim(), command.PhoneNumber, command.Cpf);

        CreateUserProfileResult profileResult;
        try
        {
            profileResult = await _userProfileRepository.CreateAsync(profile, cancellationToken);
        }
        catch
        {
            // Falha inesperada (ex.: throttling) gravando o perfil — desfaz o SignUp
            // pra não deixar conta "pela metade" (spec.md, US8). Não vira
            // Result.Failure: não é um outcome de negócio esperado, segue pro
            // GlobalExceptionHandler (500) depois do rollback.
            await _authService.DeleteAsync(command.Email, cancellationToken);
            throw;
        }

        if (profileResult.CpfAlreadyExists)
        {
            // CPF em conflito É um outcome de negócio esperado (409), mas o SignUp já
            // aconteceu — desfaz do mesmo jeito, senão o e-mail fica "queimado" no
            // Cognito e uma nova tentativa (CPF corrigido) esbarra em
            // email-already-exists em vez do erro real.
            await _authService.DeleteAsync(command.Email, cancellationToken);
            return Result.Failure<RegisterUserResult>(AuthErrors.CpfAlreadyExists);
        }

        return Result.Success(RegisterUserResult.FromEntity(authResult.Value, profile));
    }
}

public sealed record RegisterUserResult(string UserId, string Email, string Name, string PhoneNumber, string Cpf)
{
    public static RegisterUserResult FromEntity(RegisterResult authResult, UserProfile profile) =>
        new(authResult.UserId, authResult.Email, profile.Name, profile.PhoneNumber, profile.Cpf);
}
```

### `RegisterUserCommandValidator` (`Auth/Commands/Register/RegisterUserCommandValidator.cs`, novo)

```csharp
using FluentValidation;
using GastosApp.Domain.Users;

namespace GastosApp.Application.Auth.Commands.Register;

public sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    private const int MinNameLength = 2;
    private const int MaxNameLength = 150;

    public RegisterUserCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop;

        RuleFor(c => c.Email).NotEmpty().WithMessage("Email é obrigatório.");

        RuleFor(c => c.Password)
            .NotEmpty().WithMessage("Senha é obrigatória.")
            .MinimumLength(8).WithMessage("Senha deve ter no mínimo 8 caracteres.");

        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Nome é obrigatório.")
            .Must(name => name.Trim().Length is >= MinNameLength and <= MaxNameLength)
                .WithMessage($"Nome deve ter entre {MinNameLength} e {MaxNameLength} caracteres.");

        RuleFor(c => c.PhoneNumber)
            .NotEmpty().WithMessage("Telefone é obrigatório.")
            .Must(phone => phone.Length is 10 or 11 && phone.All(char.IsDigit))
                .WithMessage("Telefone deve conter 10 ou 11 dígitos numéricos.");

        RuleFor(c => c.Cpf)
            .NotEmpty().WithMessage("CPF é obrigatório.")
            .Must(cpf => cpf.Length == 11 && cpf.All(char.IsDigit))
                .WithMessage("CPF deve conter 11 dígitos numéricos.")
            .Must(Cpf.IsValid).WithMessage("CPF inválido.");
    }
}
```
`ClassLevelCascadeMode = CascadeMode.Stop` (mesmo padrão de
`CreateCategoryCommandValidator`) já garante que `Cpf.IsValid` só roda
se o `Must` anterior (11 dígitos numéricos) passou — sem precisar de
`.When()` redundante.

### `GetCurrentUserQuery` (`Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs`, novo)

```csharp
namespace GastosApp.Application.Auth.Queries.GetCurrentUser;

public sealed record GetCurrentUserQuery(string UserId, string? Email) : IQuery<Result<UserInfoResult>>;

public sealed class GetCurrentUserQueryHandler : IQueryHandler<GetCurrentUserQuery, Result<UserInfoResult>>
{
    private readonly IUserProfileRepository _userProfileRepository;

    public GetCurrentUserQueryHandler(IUserProfileRepository userProfileRepository)
    {
        _userProfileRepository = userProfileRepository;
    }

    public async ValueTask<Result<UserInfoResult>> Handle(GetCurrentUserQuery query, CancellationToken cancellationToken)
    {
        // Sem migração de dados (roadmap.md): usuário cadastrado antes desta feature
        // não tem UserProfile — campos voltam null, sem erro (spec.md não define esse
        // caso como falha).
        var profile = await _userProfileRepository.FindByUserIdAsync(query.UserId, cancellationToken);
        return Result.Success(UserInfoResult.FromEntity(query.UserId, query.Email, profile));
    }
}

public sealed record UserInfoResult(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf)
{
    public static UserInfoResult FromEntity(string userId, string? email, UserProfile? profile) =>
        new(userId, email, profile?.Name, profile?.PhoneNumber, profile?.Cpf);
}
```

### `ApplicationServiceCollectionExtensions` — registro

```csharp
services.AddScoped<IValidator<RegisterUserCommand>, RegisterUserCommandValidator>(); // novo
```
(mantém os registros já existentes — nada é removido; `GetCurrentUserQuery`
não tem validator, mesmo caso de outras queries sem regras de entrada.)

## Infrastructure-layer

### `DynamoDbUserProfileRepository` (`Users/DynamoDbUserProfileRepository.cs`, novo)

```csharp
namespace GastosApp.Infrastructure.Users;

public sealed class DynamoDbUserProfileRepository : IUserProfileRepository
{
    private const string ProfileSk = "PROFILE#";
    private const string CpfPointerSk = "CPF#";

    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly DynamoDbOptions _options;

    public DynamoDbUserProfileRepository(IAmazonDynamoDB dynamoDbClient, IOptions<DynamoDbOptions> options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options.Value;
    }

    public async Task<CreateUserProfileResult> CreateAsync(UserProfile profile, CancellationToken cancellationToken = default)
    {
        try
        {
            await _dynamoDbClient.TransactWriteItemsAsync(new TransactWriteItemsRequest
            {
                TransactItems =
                [
                    new TransactWriteItem // índice 0: CpfPointer — barra CPF duplicado
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = $"CPF#{profile.Cpf}" },
                                ["SK"] = new AttributeValue { S = CpfPointerSk },
                                ["UserId"] = new AttributeValue { S = profile.UserId }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    },
                    new TransactWriteItem // índice 1: UserProfile
                    {
                        Put = new Put
                        {
                            TableName = _options.TableName,
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["PK"] = new AttributeValue { S = $"USER#{profile.UserId}" },
                                ["SK"] = new AttributeValue { S = ProfileSk },
                                ["Name"] = new AttributeValue { S = profile.Name },
                                ["PhoneNumber"] = new AttributeValue { S = profile.PhoneNumber },
                                ["Cpf"] = new AttributeValue { S = profile.Cpf },
                                ["CreatedAt"] = new AttributeValue { S = profile.CreatedAt.ToString("O") }
                            },
                            ConditionExpression = "attribute_not_exists(PK)"
                        }
                    }
                ]
            }, cancellationToken);

            return new CreateUserProfileResult(CpfAlreadyExists: false);
        }
        catch (TransactionCanceledException ex)
        {
            var cpfPointerFailed = ex.CancellationReasons is { Count: > 0 } reasons
                && reasons[0].Code == "ConditionalCheckFailed";

            if (!cpfPointerFailed)
                throw; // índice 1 falhou (userId colidindo) — praticamente impossível, propaga

            return new CreateUserProfileResult(CpfAlreadyExists: true);
        }
    }

    public async Task<UserProfile?> FindByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        var response = await _dynamoDbClient.GetItemAsync(new GetItemRequest
        {
            TableName = _options.TableName,
            Key = new Dictionary<string, AttributeValue>
            {
                ["PK"] = new AttributeValue { S = $"USER#{userId}" },
                ["SK"] = new AttributeValue { S = ProfileSk }
            }
        }, cancellationToken);

        if (!response.IsItemSet)
            return null;

        return UserProfile.Restore(
            userId,
            response.Item["Name"].S,
            response.Item["PhoneNumber"].S,
            response.Item["Cpf"].S,
            DateTimeOffset.Parse(response.Item["CreatedAt"].S));
    }
}
```

### `CognitoAuthService.DeleteAsync` (novo método)

```csharp
public async Task DeleteAsync(string email, CancellationToken cancellationToken = default)
{
    // Username = email porque o User Pool usa username_attributes=["email"]
    // (cognito.tf) — não é um alias, é o próprio Username.
    await _cognitoClient.AdminDeleteUserAsync(new AdminDeleteUserRequest
    {
        UserPoolId = _options.UserPoolId,
        Username = email
    }, cancellationToken);
}
```

### `InfrastructureServiceCollectionExtensions` — registro

```csharp
services.AddScoped<IUserProfileRepository, DynamoDbUserProfileRepository>(); // novo
```

## Api-layer

### `AuthEndpoints.cs`

```csharp
public record RegisterRequest(string Email, string Password, string Name, string PhoneNumber, string Cpf);
public record UserInfoResponse(string UserId, string? Email, string? Name, string? PhoneNumber, string? Cpf);
```

```csharp
private static async Task<IResult> RegisterUser(RegisterRequest request, ISender sender, CancellationToken cancellationToken)
{
    var command = new RegisterUserCommand(request.Email, request.Password, request.Name, request.PhoneNumber, request.Cpf);
    var result = await sender.Send(command, cancellationToken);
    return result.ToHttpResult(value => Results.Created("/auth/me", value));
}
```

```csharp
private static async Task<IResult> UserData(ClaimsPrincipal user, ISender sender, CancellationToken cancellationToken)
{
    var userId = user.FindFirst("sub")?.Value ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    var email = user.FindFirst("email")?.Value ?? user.FindFirst(ClaimTypes.Email)?.Value;

    if (string.IsNullOrEmpty(userId))
    {
        return Results.Json(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Não autorizado",
            Type = "https://gastosapp.dev/errors/unauthorized"
        }, AppJsonSerializerContext.Default.ProblemDetails, statusCode: StatusCodes.Status401Unauthorized, contentType: "application/problem+json");
    }

    var query = new GetCurrentUserQuery(userId, email);
    var result = await sender.Send(query, cancellationToken);
    return result.ToHttpResult(value => Results.Ok(new UserInfoResponse(value.UserId, value.Email, value.Name, value.PhoneNumber, value.Cpf)));
}
```
`name` deixa de vir da claim do JWT — a checagem de `userId`/401 continua
igual (claims), só a montagem da resposta passa a vir do `Mediator`.

`RegisterUserResult`/`UserInfoResponse` já estão em
`AppJsonSerializerContext` ([JsonSerializable(typeof(RegisterUserResult))]`/
`[JsonSerializable(typeof(UserInfoResponse))]`, ambos já registrados hoje) —
como só ganham propriedades novas (mesmo tipo), **nenhuma entrada nova é
necessária** no source generator.

`Program.cs`: nenhuma mudança — `GetCurrentUserQueryHandler` é descoberto
pelo `AddMediator()` já configurado, sem novo `Map*Endpoints()`.

## Mapeamento de erros

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `email`/`password` ausente ou inválido (já existente) | `bad-request` | `Validation` | 400 |
| `name` ausente ou fora de 2-150 caracteres | `bad-request` | `Validation` | 400 |
| `phoneNumber` ausente, não numérico ou fora de 10/11 dígitos | `bad-request` | `Validation` | 400 |
| `cpf` ausente, não numérico, fora de 11 dígitos, ou dígito verificador inválido | `bad-request` | `Validation` | 400 |
| `email` já cadastrado (já existente) | `email-already-exists` | `Conflict` | 409 |
| `cpf` já cadastrado por outro usuário | `cpf-already-exists` (novo) | `Conflict` | 409 |
| Falha inesperada gravando o perfil (ex.: throttling do DynamoDB) | — (exceção, não `Result`) | — | 500 via `GlobalExceptionHandler` |

`401` de `GET /auth/me` continua vindo da checagem de claims no próprio
endpoint (antes de qualquer `sender.Send`), sem mudança.

## Recursos AWS

**Nenhum recurso novo** — reaproveita a tabela `GastosApp` (mesmo
formato de chave já usado por `AccountPointer`/`Account`/`Membership`,
sem GSI adicional) e o Cognito User Pool existente, sem tocar seu
schema/atributos.

**Uma permissão IAM precisa ser adicionada** ao papel da Lambda, em
`backend/infra/terraform/environments/{prod,hom}/lambda.tf`, statement
`CognitoAccess`:

```diff
   "cognito-idp:SignUp",
   "cognito-idp:InitiateAuth",
   "cognito-idp:GetUser"
+  "cognito-idp:AdminDeleteUser"
```

`AdminDeleteUser` é uma API "Admin" do Cognito (exige credenciais IAM,
diferente de `SignUp`/`InitiateAuth`/`GetUser`, que são client-side) —
necessária pro rollback descrito em `spec.md` (US8): sem ela, uma falha
gravando o perfil deixaria um usuário órfão no Cognito (SignUp
concluído, sem perfil), impossibilitando nova tentativa de registro com
o mesmo email.

**Isso é uma mudança de segurança (amplia o que a Lambda de produção
pode fazer no Cognito) e precisa de aprovação explícita antes do
`/tasks`**, conforme já é praxe neste projeto pra qualquer mudança de
IAM/infra. Alternativa, caso não aprovado: não fazer rollback
automático — uma falha ao gravar o perfil deixaria mesmo uma conta
órfã no Cognito, exigindo limpeza manual (pior UX, sem mudança de IAM).

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Domain/Users/CpfTests.cs` (novo, sem mock — lógica pura):
  - CPF válido conhecido (ex.: `11144477735`) → `true`
  - CPF com dígito verificador alterado → `false`
  - CPF com todos os dígitos iguais (ex.: `11111111111`) → `false`
    mesmo quando o cálculo "fecha"
  - CPF com menos/mais de 11 caracteres, ou com caractere não numérico
    → `false`
- `Application/Auth/RegisterUserCommandValidatorTests.cs` (novo):
  casos de `email`/`password` já cobertos hoje em
  `RegisterUserCommandHandlerTests` migram pra cá; mais `name` vazio/
  curto/longo, `phoneNumber` não numérico ou fora de 10/11 dígitos,
  `cpf` fora de 11 dígitos e `cpf` matematicamente inválido
- `Application/RegisterUserCommandHandlerTests.cs` (reescrito — mock
  `IAuthService` + `IUserProfileRepository`):
  - sucesso: `RegisterUserResult` reflete `RegisterResult` (Cognito) +
    `UserProfile` (nome com `Trim()` aplicado)
  - `IAuthService.RegisterAsync` falha (email duplicado) → propaga o
    `Error` sem chamar `IUserProfileRepository`
  - `IUserProfileRepository.CreateAsync` retorna `CpfAlreadyExists: true`
    → `Result.Failure(AuthErrors.CpfAlreadyExists)` **e**
    `IAuthService.DeleteAsync` chamado com o email do comando
  - `IUserProfileRepository.CreateAsync` lança exceção → exceção
    relançada (não vira `Result.Failure`) **e** `DeleteAsync` chamado
    antes de relançar
  - sucesso: `IAuthService.DeleteAsync` **nunca** chamado
- `Infrastructure/CognitoAuthServiceTests.cs` — novo caso pra
  `DeleteAsync`: chama `AdminDeleteUserAsync` com `UserPoolId` e
  `Username=email` corretos
- `Application/Auth/GetCurrentUserQueryHandlerTests.cs` (novo — mock
  `IUserProfileRepository`):
  - perfil encontrado → `UserInfoResult` com `Name`/`PhoneNumber`/`Cpf`
    do perfil
  - perfil não encontrado (`null`) → `UserInfoResult` com esses 3
    campos `null`, sem erro
- `Infrastructure/DynamoDbUserProfileRepositoryTests.cs` (se o projeto
  tiver precedente de testar repositórios DynamoDB diretamente — senão,
  cobertura fica só via ComponentTests, mesmo critério já usado por
  `DynamoDbAccountRepository`)

### Component tests (`backend/tests/GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs`, estendido)

`ComponentTestWebApplicationFactory` ganha `IUserProfileRepository
UserProfileRepositoryMock` + `ResetUserProfileRepositoryMock()`, mesmo
padrão de `AccountRepositoryMock`/`MembershipRepositoryMock` (registrado
em `ConfigureTestServices` com `RemoveAll<IUserProfileRepository>()` +
`AddScoped(_ => UserProfileRepositoryMock)`).

Casos novos/atualizados:
- `Register_ComDadosValidos_Retorna201ComLocationEBody` — request
  ganha `name`/`phoneNumber`/`cpf`; body da resposta passa a incluir
  os 3 campos
- `Register_ComCpfJaCadastrado_Retorna409` (novo) —
  `UserProfileRepositoryMock.CreateAsync(...)` retorna
  `CpfAlreadyExists: true`; assert 409 +
  `type=.../errors/cpf-already-exists` + `AuthServiceMock.Received(1).DeleteAsync(...)`
- `Register_ComParametrosInvalidos_Retorna400SemChamarAuthService` —
  `[Theory]` ganha casos de `name`/`phoneNumber`/`cpf` ausentes/inválidos
- `Me_ComPerfilCadastrado_Retorna200ComNomeTelefoneCpf` (novo) —
  `UserProfileRepositoryMock.FindByUserIdAsync(...)` retorna um
  `UserProfile`; assert 200 com os 3 campos no corpo
- `Me_SemPerfilCadastrado_Retorna200ComCamposNulos` (novo) —
  `FindByUserIdAsync` retorna `null`; assert 200 com
  `name`/`phoneNumber`/`cpf` ausentes/`null` no JSON

### Teste de regressão já existente

`ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownEleven`
precisa ser atualizado pra `...BeyondTheKnownTwelve`, incluindo
`RegisterUserCommandValidator` na lista fechada — mesma manutenção já
feita nas features anteriores que adicionaram validator.

## Critical Files

- `backend/src/GastosApp.Domain/Users/Cpf.cs` (novo)
- `backend/src/GastosApp.Domain/Users/UserProfile.cs` (novo)
- `backend/src/GastosApp.Application/Common/Interfaces/IUserProfileRepository.cs` (novo)
- `backend/src/GastosApp.Application/Common/Interfaces/IAuthService.cs`
- `backend/src/GastosApp.Application/Auth/AuthErrors.cs`
- `backend/src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommand.cs`
- `backend/src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommandValidator.cs` (novo)
- `backend/src/GastosApp.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs` (novo)
- `backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`
- `backend/src/GastosApp.Infrastructure/Users/DynamoDbUserProfileRepository.cs` (novo)
- `backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs`
- `backend/src/GastosApp.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
- `backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs`
- `backend/infra/terraform/environments/prod/lambda.tf` e `environments/hom/lambda.tf` (permissão IAM — só após aprovação)
- `backend/tests/GastosApp.ComponentTests/Support/ComponentTestWebApplicationFactory.cs`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln`
- `dotnet test backend/GastosApp.sln` — suíte completa, sem regressão
  em `Auth`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution) — `git diff` deve mostrar só a
  mudança em `POST /auth/register` (request/response) e
  `GET /auth/me` (response), sem tocar as demais rotas
- Se a permissão IAM for aprovada: `terraform plan` em
  `environments/hom` (e depois `prod`) mostrando só a adição de
  `cognito-idp:AdminDeleteUser` na policy existente — sem novo recurso
- Smoke manual (opcional, contra ambiente local
  `infra/README.md`/cognito-local): registrar um usuário com
  `name`/`phoneNumber`/`cpf` válidos, conferir 201 e depois `GET
  /auth/me` retornando os 3 campos; repetir registro com o mesmo CPF
  (409); confirmar se `cognito-local` suporta `AdminDeleteUser` — se
  não suportar, o rollback fica sem cobertura de smoke manual local
  (ComponentTests continuam cobrindo via mock, não são afetados)

## Decisões técnicas

1. **Perfil 100% no DynamoDB, nenhuma mudança no Cognito** — ver
   `spec.md`, decisão 1.
2. **`CpfPointer` como item-sentinela em `TransactWriteItems`**, mesmo
   padrão do `AccountPointer` (FEAT-19) — sem `Scan`, sem GSI novo.
3. **Validação de `RegisterUserCommand` migrada pra
   `RegisterUserCommandValidator`**, incluindo as regras de
   `email`/`password` já existentes — exigido pela constitution ao
   tocar o Handler; `LoginUserCommand`/`RefreshTokenCommand` (mesmo
   débito) ficam fora do escopo desta feature.
4. **`Cpf.IsValid` no Domain**, não no validator — regra de negócio
   pura, mesmo padrão de `CategorySlug`.
5. **Rollback do Cognito (`AdminDeleteUser`) tanto pra CPF duplicado
   quanto pra falha inesperada** — em ambos os casos o `SignUp` já
   aconteceu; sem rollback, o email fica "queimado" impedindo nova
   tentativa.
6. **`GET /auth/me` passa a usar `Mediator`** (`GetCurrentUserQuery`),
   abandonando a leitura direta de claims pra montar a resposta — Api
   não pode chamar `Infrastructure` diretamente.
7. **Perfil ausente em `GET /auth/me` retorna 200 com campos `null`**,
   não erro — cobre contas anteriores a esta feature sem exigir
   migração de dados (decisão já registrada em `roadmap.md`).

## Pontos que precisam de confirmação antes do `/tasks`

1. **Aprovar a nova permissão IAM `cognito-idp:AdminDeleteUser`** no
   papel da Lambda (prod e hom) — é o único ponto desta feature que
   toca infraestrutura/segurança. Sem essa aprovação, a alternativa é
   remover o rollback automático (US8 do `spec.md` deixaria de ser
   atendida tal como especificada).
2. Confirmar se o nome dos novos campos no contrato (`name`,
   `phoneNumber`, `cpf`) está adequado — segue o padrão em inglês já
   usado por `email`/`userId`/`name` (existente), diferente de campos
   de domínio como `tipo`/`categoria`/`descricao` que são em português.
