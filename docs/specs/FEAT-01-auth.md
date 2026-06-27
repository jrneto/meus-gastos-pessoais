# FEAT-01: Autenticação

## Objetivo
Permitir que um usuário se registre e faça login na aplicação,
recebendo um JWT para autenticar as demais requisições.
Em ambiente Development, a assinatura do JWT não é validada.
Em Production, a validação ocorre contra o Cognito da AWS.

## Regras de negócio
- Email deve ser único por usuário
- Senha mínima: 8 caracteres, ao menos 1 maiúscula e 1 número
- O campo userId nunca vem do body — sempre do JWT (claim "sub")
- Tokens expiram em 1 hora
- Não há refresh token no MVP

## Contratos da API

### POST /auth/register
Request:
{
  "email": "neto@email.com",
  "password": "Senha123",
  "name": "Neto"
}

Response 201:
{
  "userId": "uuid-gerado-pelo-cognito",
  "email": "neto@email.com",
  "name": "Neto"
}

Response 409 (email já cadastrado):
{
  "type": "https://gastosapp.dev/errors/email-already-exists",
  "title": "Email já cadastrado",
  "status": 409
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
  "userId": "uuid-do-cognito",
  "name": "Neto"
}

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

Response 401 (token ausente ou inválido):
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Não autorizado",
  "status": 401
}

## Comportamento do JWT por ambiente

### Development
- Middleware de autenticação configurado com
  RequireHttpsMetadata=false
- Validação de assinatura DESABILITADA
  (ValidateIssuerSigningKey=false, ValidateIssuer=false)
- Para testar endpoints protegidos, gere um JWT fake com
  qualquer ferramenta (ex: jwt.io) com o claim "sub" preenchido
- O Cognito LocalStack é usado apenas para simular
  register/login — não para validar tokens

### Production
- Validação contra JWKS do Cognito:
  https://cognito-idp.{region}.amazonaws.com/{userPoolId}
  /.well-known/jwks.json
- ValidateIssuerSigningKey=true
- ValidateIssuer=true
- ValidateAudience=true

## Mapeamento de camadas

### Domain
- Nenhuma entidade de domínio — auth é responsabilidade
  do Cognito, não do domínio da aplicação

### Application
- RegisterUserCommand { Email, Password, Name }
- RegisterUserCommandHandler
- LoginUserCommand { Email, Password }
- LoginUserCommandHandler
- Interfaces: IAuthService

### Infrastructure
- CognitoAuthService implementa IAuthService
- Usa AWSSDK.CognitoIdentityProvider
- Em Development aponta para http://localstack:4566
- Em Production aponta para endpoint real da AWS

### Api
- AuthEndpoints.cs com os 3 endpoints mapeados
- Middleware JWT configurado no Program.cs
- Em Development: ValidateIssuerSigningKey=false
- Em Production: lê UserPoolId e ClientId do
  AWS Secrets Manager

## Casos de erro mapeados
- Email já cadastrado → 409
- Credenciais inválidas → 401
- Token ausente → 401
- Token expirado → 401
- Erro interno do Cognito → 500 com log estruturado

## Critérios de aceite
- [ ] POST /auth/register cria usuário no Cognito LocalStack
      e retorna 201
- [ ] POST /auth/register com email duplicado retorna 409
- [ ] POST /auth/login com credenciais válidas retorna
      accessToken
- [ ] POST /auth/login com senha errada retorna 401
- [ ] GET /auth/me com JWT fake (Development) retorna dados
      do usuário extraídos do token
- [ ] GET /auth/me sem token retorna 401
- [ ] Todos os erros seguem RFC 9457 (ProblemDetails)
- [ ] Testes de integração cobrem register e login contra
      LocalStack real via Testcontainers

## Fora do escopo deste FEAT
- Refresh token
- Logout / revogação de token
- Recuperação de senha
- MFA