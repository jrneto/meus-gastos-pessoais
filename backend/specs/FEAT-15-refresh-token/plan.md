# FEAT-15: Refresh Token — Plano Técnico

Baseado em `spec.md` desta mesma pasta, `backend/docs/constitution.md`,
`backend/CLAUDE.md` e `backend/docs/architecture.md`.

## Camadas afetadas

### Infrastructure — `GastosApp.Infrastructure`
- `IAuthService.LoginResult` (`Common/Interfaces/IAuthService.cs`) ganha um
  4º campo: `record LoginResult(string AccessToken, int ExpiresIn, string UserId, string RefreshToken)`.
- `CognitoAuthService.LoginAsync`: passa a ler `result.RefreshToken` do
  `AuthenticationResult` retornado por `InitiateAuthAsync` (já vem hoje,
  só não era usado) e propagar no `LoginResult`.
- Novo método na interface: `Task<Result<RefreshResult>> RefreshAsync(string refreshToken, CancellationToken ct = default)`,
  com `record RefreshResult(string AccessToken, int ExpiresIn, string UserId)`
  — sem refresh token, pois não há rotação.
- `CognitoAuthService.RefreshAsync`: chama `InitiateAuthAsync` com
  `AuthFlowType.REFRESH_TOKEN_AUTH` e `AuthParameters["REFRESH_TOKEN"]`,
  depois `GetUserAsync` com o novo `AccessToken` para extrair `sub` (mesmo
  padrão do `LoginAsync` atual). Captura `NotAuthorizedException` →
  `Result.Failure<RefreshResult>(AuthErrors.InvalidRefreshToken)`.

### Application — `GastosApp.Application`
- `AuthErrors.cs`: adicionar
  - `RefreshTokenMissing => Error.Unauthorized("refresh-token-missing", "Refresh token ausente.")`
  - `InvalidRefreshToken => Error.Unauthorized("invalid-refresh-token", "Refresh token inválido ou expirado.")`
- `Auth/Commands/Login/LoginUserCommand.cs`: `LoginUserResult` ganha
  `[JsonIgnore] string RefreshToken` — nunca serializado no corpo, só lido
  pelo endpoint Api para setar o cookie — e um factory method
  `FromLoginResult(LoginResult r)`, seguindo a convenção de factory já
  usada em outras features. Handler passa a usar o factory em vez de
  montar o record campo a campo.
- Novo `Auth/Commands/Refresh/RefreshTokenCommand.cs`:
  ```csharp
  record RefreshTokenCommand(string RefreshToken) : ICommand<Result<RefreshTokenResult>>;

  class RefreshTokenCommandHandler : ICommandHandler<RefreshTokenCommand, Result<RefreshTokenResult>>
  {
      // command.RefreshToken vazio/whitespace → Result.Failure(AuthErrors.RefreshTokenMissing)
      // senão: _authService.RefreshAsync(...) e mapeia erro/sucesso
  }

  record RefreshTokenResult(string AccessToken, int ExpiresIn, string UserId)
  {
      static RefreshTokenResult FromRefreshResult(RefreshResult r) => new(r.AccessToken, r.ExpiresIn, r.UserId);
  }
  ```
  A checagem de token vazio fica manual no handler (não via
  `IValidator`/`ValidationBehavior`), porque o resultado é 401
  (Unauthorized), não 400 (Validation) — o pipeline de validação do
  projeto sempre mapeia para 400, então esse caso é regra de negócio, não
  validação de input.
- Novo `Auth/Commands/Logout/LogoutCommand.cs`:
  ```csharp
  record LogoutCommand : ICommand<Result>;
  class LogoutCommandHandler // sempre Result.Success()
  ```
  Existe só para respeitar a regra da constitution de que rotas apenas
  fazem `sender.Send(...)` — não há lógica de negócio hoje (sem revogação
  no Cognito, fora do escopo desta FEAT), mas deixa o ponto de extensão
  pronto para uma futura `RevokeToken`/`GlobalSignOut`.

### Api — `GastosApp.Api`
- Novo `Api/Common/RefreshTokenCookie.cs`: helper estático central para não
  duplicar as flags do cookie entre os 3 endpoints:
  ```csharp
  const string Name = "refreshToken";
  static CookieOptions ForSet() // HttpOnly=true, Secure=true, SameSite=Strict, Path="/auth", MaxAge=TimeSpan.FromDays(5)
  static CookieOptions ForClear() // mesmas flags + Expires=DateTimeOffset.UnixEpoch
  ```
- `AuthEndpoints.cs`:
  - `POST /login`: endpoint passa a receber `HttpContext`; no sucesso,
    `httpContext.Response.Cookies.Append(RefreshTokenCookie.Name, value.RefreshToken, RefreshTokenCookie.ForSet())`
    antes de `Results.Ok(value)`. Corpo da resposta continua idêntico
    (`RefreshToken` tem `[JsonIgnore]`).
  - Novo `POST /refresh`: lê `httpContext.Request.Cookies[RefreshTokenCookie.Name]`
    (pode ser `null`), monta `RefreshTokenCommand(refreshToken ?? "")`,
    `sender.Send`. **Em qualquer falha** (ausente ou inválido/expirado),
    limpa o cookie via `Response.Cookies.Append(Name, "", ForClear())`
    antes de devolver o problem — simplificação deliberada: limpar um
    cookie ausente é inofensivo, e evita duplicar a lógica por tipo de
    erro. Em sucesso, **não** reescreve o cookie (sem rotação — a sessão
    expira 5 dias após o login original, não é renovada a cada refresh).
  - Novo `POST /logout`: `sender.Send(new LogoutCommand())`, depois sempre
    limpa o cookie, retorna `Results.Ok()` (200 sem corpo, idempotente).
  - `.Produces`/`.ProducesProblem` em cada rota nova (`/refresh` → 200
    `RefreshTokenResult` + `ProducesProblem(401)`; `/logout` → 200 sem
    tipo, sem problem específico).
- `AppJsonSerializerContext.cs`: adicionar
  `[JsonSerializable(typeof(RefreshTokenResult))]` (Native AOT exige tipo
  conhecido em tempo de build — já é o padrão do projeto).

### Domain
Nenhuma mudança — auth continua sem entidade de domínio (igual FEAT-01).

## Recursos AWS
Nenhum recurso novo. Reutiliza o App Client do Cognito já provisionado com
`ALLOW_REFRESH_TOKEN_AUTH` e `refresh_token_validity=5` dias (Terraform da
FEAT-09). Este plano não altera nada em `backend/infra/terraform/`.

## Mapeamento de erros → HTTP

| Erro | ErrorType | HTTP | Endpoint |
|---|---|---|---|
| `AuthErrors.RefreshTokenMissing` | Unauthorized | 401 | `POST /auth/refresh` |
| `AuthErrors.InvalidRefreshToken` | Unauthorized | 401 | `POST /auth/refresh` |
| Exceção não prevista do Cognito | — | 500 (`GlobalExceptionHandler`) | qualquer |

`POST /auth/logout` não tem caminho de erro de negócio (sempre 200).

## Testes previstos
- **Unitário** — `CognitoAuthServiceTests`: novo caso para `RefreshAsync`
  (sucesso e `NotAuthorizedException` → `InvalidRefreshToken`); atualizar
  casos de `LoginAsync` para cobrir `RefreshToken` no `LoginResult`.
- **Unitário** — novo `RefreshTokenCommandHandlerTests` (token vazio → 401
  sem chamar `IAuthService`; sucesso; falha propagada do service) e novo
  `LogoutCommandHandlerTests` (sempre sucesso).
- **Componente** (`AuthEndpointsTests.cs`):
  - login válido → resposta 200 tem `Set-Cookie` com as flags esperadas e
    corpo sem `refreshToken`
  - `/auth/refresh` com cookie mockado válido → 200 com novo `accessToken`
  - `/auth/refresh` sem cookie → 401, sem chamar `IAuthService.RefreshAsync`
  - `/auth/refresh` com cookie mas service retornando `InvalidRefreshToken`
    → 401 e resposta limpa o cookie
  - `/auth/logout` com e sem cookie → sempre 200, sempre limpa o cookie

## Atualização de contrato
Ao final da implementação: rodar `backend/scripts/export-openapi.sh` e
revisar manualmente `backend/docs/openapi.json` — o header `Set-Cookie` não
é documentado automaticamente pelas anotações `.Produces` do Minimal API,
precisa ser adicionado à mão nas 3 rotas (`/login`, `/refresh`, `/logout`),
conforme já exigido pela constitution.

## Decisões confirmadas com o usuário
- Sem rotação em `/refresh`: sessão expira 5 dias após o login original,
  sem estender o `Max-Age` a cada refresh.
- `/refresh` limpa o cookie em qualquer falha (ausente ou inválido).
- `LogoutCommand` mantido via Mediator mesmo sem lógica real hoje, para
  seguir o padrão "rotas só chamam o mediator" e já deixar o ponto de
  extensão pronto.
