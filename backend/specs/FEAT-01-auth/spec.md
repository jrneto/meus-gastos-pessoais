# FEAT-01: Autenticação

## Objetivo
Permitir que um usuário se registre e faça login na aplicação,
recebendo um JWT (IdToken do Cognito) para autenticar as demais requisições.
A aplicação conecta diretamente aos serviços reais da AWS (Cognito) em
todos os ambientes — não há LocalStack nem simulação local.
A validação do JWT é sempre feita contra o JWKS do Cognito.

## Regras de negócio
- Email deve ser único por usuário (garantido pelo Cognito)
- Senha mínima: 8 caracteres (validação local no handler)
- O campo userId nunca vem do body — sempre extraído das claims do JWT
  (`sub`, com fallback para `ClaimTypes.NameIdentifier`)
- Tokens expiram conforme configuração do Cognito App Client (retornado em `expiresIn`)
- Não há refresh token no MVP

## Contratos da API

### POST /auth/register
Request:
{
  "email": "neto@email.com",
  "password": "Senha123"
}

Response 201 (Location: /auth/me):
{
  "userId": "uuid-gerado-pelo-cognito",
  "email": "neto@email.com"
}

Response 409 (email já cadastrado):
{
  "type": "https://gastosapp.dev/errors/email-already-exists",
  "title": "Email já cadastrado",
  "status": 409
}

Response 400 (parâmetros inválidos — email/senha ausentes ou senha curta):
{
  "type": "https://gastosapp.dev/errors/bad-request",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Senha deve ter no mínimo 8 caracteres."
}

### POST /auth/login
Request:
{
  "email": "neto@email.com",
  "password": "Senha123"
}

Response 200:
{
  "accessToken": "eyJ...",
  "expiresIn": 3600,
  "userId": "uuid-do-cognito"
}

Observação: `accessToken` retorna o **IdToken** do Cognito (não o AccessToken
do fluxo OAuth), pois é ele que carrega as claims de email/name usadas por
`GET /auth/me`.

Response 401 (credenciais inválidas):
{
  "type": "https://gastosapp.dev/errors/invalid-credentials",
  "title": "Email ou senha inválidos",
  "status": 401
}

### GET /auth/me
Header: Authorization: Bearer <token>

Response 200:
{
  "userId": "uuid-do-cognito",
  "email": "neto@email.com",
  "name": "Neto"
}

Response 401 (token ausente, inválido ou expirado):
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Não autorizado",
  "status": 401
}

## Comportamento do JWT

- Middleware `AddJwtBearer` sempre valida contra o JWKS real do Cognito:
  `https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}`
- `RequireHttpsMetadata = true`
- `ValidateIssuerSigningKey = true`
- `ValidateIssuer = true`
- `ValidateAudience = true` (audience = ClientId do Cognito App Client)
- `ValidateLifetime = true`
- Não existe modo de desenvolvimento com validação de assinatura
  desabilitada — todos os ambientes (local incluído) apontam para o
  User Pool real na AWS, configurado via `CognitoOptions`.
- Falha de autenticação (`OnChallenge`) retorna 401 já formatado como
  ProblemDetails.

## Mapeamento de camadas

### Domain
- Nenhuma entidade de domínio — auth é responsabilidade
  do Cognito, não do domínio da aplicação

### Application
- Mediator: biblioteca `Mediator` (martinothamar), conforme
  `docs/specs/FEAT-02-mediator-result-pattern.md`. `ICommand<TResponse>`/
  `ICommandHandler<TCommand,TResponse>` usados aqui são da lib (namespace
  `Mediator`), não mais abstrações próprias.
- `RegisterUserCommand(Email, Password)` / `RegisterUserCommandHandler`
  → valida email/senha obrigatórios e senha mínima de 8 caracteres,
  retorna `Result<RegisterUserResult>`
- `LoginUserCommand(Email, Password)` / `LoginUserCommandHandler`
  → valida email/senha obrigatórios,
  retorna `Result<LoginUserResult>`
- Interface: `IAuthService` (`RegisterAsync`, `LoginAsync`) em
  `GastosApp.Application/Common/Interfaces`, retornando
  `Result<RegisterResult>`/`Result<LoginResult>`
- Erros de negócio modelados como `Error`/`ErrorType` (não mais exceções):
  `AuthErrors.EmailAlreadyExists` (`ErrorType.Conflict`),
  `AuthErrors.InvalidCredentials` (`ErrorType.Unauthorized`),
  `AuthErrors.Validation(message)` (`ErrorType.Validation`) — em
  `GastosApp.Application/Auth/AuthErrors.cs`
- Handlers não lançam mais exceções para fluxo de negócio: seguem o
  Result Pattern customizado definido na constituição
  (`GastosApp.Application/Common/Results`). Débito técnico anterior
  resolvido.

### Infrastructure
- `CognitoAuthService` implementa `IAuthService`, usando
  `AWSSDK.CognitoIdentityProvider`
- `RegisterAsync` → `SignUpAsync`, captura `UsernameExistsException` e
  converte para `Result.Failure(AuthErrors.EmailAlreadyExists)`
- `LoginAsync` → `InitiateAuthAsync` com `USER_PASSWORD_AUTH`, seguido de
  `GetUserAsync` para extrair `sub` e `name`; captura
  `NotAuthorizedException`/`UserNotFoundException` e converte para
  `Result.Failure(AuthErrors.InvalidCredentials)`
- Conexão sempre com a AWS real (sem LocalStack): usa o `RegionEndpoint`
  configurado e credenciais via IAM Role/ambiente por padrão; suporta
  `AccessKey`/`SecretKey` explícitos como alternativa (não recomendado
  em produção)
- `CognitoOptions` (`Region`, `UserPoolId`, `ClientId`, `ServiceURL`
  opcional, `AccessKey`/`SecretKey` opcionais) é lida da seção `Cognito`
  da configuração, alimentada pelo AWS Parameter Store (`/GastosApp/`)

### Api
- `AuthEndpoints.cs` com os 3 endpoints mapeados via `MapGroup("/auth")`
- `POST /register` e `POST /login` injetam `ISender` (Mediator) e chamam
  apenas `sender.Send(command, ct)`; o `Result` retornado é mapeado para
  `IResult` via `ResultHttpExtensions.ToHttpResult` (`GastosApp.Api/Common`)
- `GlobalExceptionHandler` (`IExceptionHandler`) trata apenas exceções não
  previstas (bug/infra) → sempre 500 com `ProblemDetails` (RFC 9457)

## Casos de erro mapeados
- Email já cadastrado → 409 (`ErrorType.Conflict`)
- Credenciais inválidas → 401 (`ErrorType.Unauthorized`)
- Parâmetros inválidos → 400 (`ErrorType.Validation`)
- Token ausente/inválido/expirado → 401 (validação JWT / claims ausentes)
- Erro interno do Cognito ou exceção não prevista → 500 com log
  estruturado (Serilog), via `GlobalExceptionHandler`

## Critérios de aceite
- [x] POST /auth/register cria usuário no Cognito (AWS real) e retorna 201
- [x] POST /auth/register com email duplicado retorna 409
- [x] POST /auth/login com credenciais válidas retorna accessToken
- [x] POST /auth/login com senha errada retorna 401
- [x] GET /auth/me com JWT válido retorna dados do usuário extraídos do token
- [x] GET /auth/me sem token retorna 401
- [x] Todos os erros seguem RFC 9457 (ProblemDetails)
- [ ] Testes de integração cobrem register e login contra o Cognito real
      (hoje: `GastosApp.IntegrationTests` está sem testes implementados)
- [x] Testes unitários cobrem os handlers de Register e Login
      (`RegisterUserCommandHandlerTests`, `LoginUserCommandHandlerTests`),
      o `CognitoAuthService` (`CognitoAuthServiceTests`), o `Result`
      Pattern (`ResultTests`) e o mapeamento Result→HTTP
      (`ResultHttpExtensionsTests`)

## Fora do escopo deste FEAT
- Refresh token
- Logout / revogação de token
- Recuperação de senha
- MFA
- Validação de senha forte (maiúscula/número) — hoje só o tamanho mínimo
  é validado localmente; regras adicionais de senha ficam a cargo da
  política do Cognito User Pool, não do código da aplicação
