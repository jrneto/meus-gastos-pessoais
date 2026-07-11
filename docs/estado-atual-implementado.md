# Especificação Técnica de Implementação (SDD) — Autenticação & AWS Cognito

Esta especificação reflete com exatidão o comportamento, contratos e regras técnicas da funcionalidade de Autenticação implementada no backend do **GastosApp**, com base na análise do código-fonte atual da solução.

---

## 1. Escopo Técnico e Arquitetura

### 1.1. Stack Tecnológica
*   **Runtime:** .NET 10 (ASP.NET Core Minimal APIs).
*   **Integração de Identidade:** AWS Cognito (User Pools).
*   **Provedor de Configuração:** AWS Systems Manager (Parameter Store) sob o path `/GastosApp/`.
*   **Autenticação de API:** JWT Bearer (validação contra as chaves JWKS públicas da AWS).

### 1.2. Mapeamento de Camadas (CQS & DI)
*   **API ([AuthEndpoints.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs))**: Expõe os endpoints `/auth/register`, `/auth/login` e `/auth/me`. Responsável por receber o payload HTTP, orquestrar com os Handlers apropriados e tratar respostas de erro.
*   **Application ([GastosApp.Application](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application))**: Contém os commands, validações prévias e a administração de `IAuthService`.
*   **Infrastructure ([CognitoAuthService.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs))**: Implementa `IAuthService` realizando chamadas SDK para a API do AWS Cognito utilizando credenciais do ambiente ou perfil configurado.

---

## 2. Contratos da API & Rotas

### 2.1. Criar Usuário (`POST /auth/register`)
Realiza o cadastro de uma nova conta de usuário no AWS Cognito.

*   **Endpoint:** `POST /auth/register`
*   **Content-Type:** `application/json`
*   **Payload de Entrada (`RegisterRequest`):**
    ```json
    {
      "email": "usuario@exemplo.com",
      "password": "SenhaSegura123"
    }
    ```
*   **Regras de Validação (C#):**
    *   `email` não pode ser nulo ou vazio (retorna `400 Bad Request`).
    *   `password` não pode ser nula ou vazia (retorna `400 Bad Request`).
    *   `password` deve conter no mínimo 8 caracteres (retorna `400 Bad Request`).
*   **Fluxo de Sucesso:**
    1.  Cria o usuário no pool do AWS Cognito.
    2.  O atributo `email` é propagado para o atributo padrão de email do Cognito.
*   **Resposta de Sucesso (`201 Created`):**
    *   *Header:* `Location: /auth/me`
    *   *Body (`RegisterUserResult`):*
        ```json
        {
          "userId": "uuid-gerado-pelo-cognito",
          "email": "usuario@exemplo.com"
        }
        ```
*   **Respostas de Erro Mapeadas:**
    *   **`400 Bad Request`** (Parâmetros inválidos):
        ```json
        {
          "type": "https://gastosapp.dev/errors/bad-request",
          "title": "Parâmetros inválidos",
          "status": 400,
          "detail": "Mensagem detalhada do erro de validação (ex: Senha deve ter no mínimo 8 caracteres.)"
        }
        ```
    *   **`409 Conflict`** (E-mail duplicado/já cadastrado):
        ```json
        {
          "type": "https://gastosapp.dev/errors/email-already-exists",
          "title": "Email já cadastrado",
          "status": 409
        }
        ```

### 2.2. Login (`POST /auth/login`)
Realiza a autenticação do usuário contra o Cognito via fluxo `USER_PASSWORD_AUTH`.

*   **Endpoint:** `POST /auth/login`
*   **Content-Type:** `application/json`
*   **Payload de Entrada (`LoginRequest`):**
    ```json
    {
      "email": "usuario@exemplo.com",
      "password": "SenhaSegura123"
    }
    ```
*   **Regras de Validação (C#):**
    *   `email` não pode ser nulo ou vazio (retorna `400 Bad Request`).
    *   `password` não pode ser nula ou vazia (retorna `400 Bad Request`).
*   **Fluxo de Sucesso:**
    1.  Efetua autenticação no Cognito.
    2.  Recupera os detalhes adicionais do usuário logado via endpoint `GetUserAsync` do Cognito SDK.
*   **Resposta de Sucesso (`200 OK`):**
    *   *Body (`LoginUserResult`):*
        *   *Nota:* O `accessToken` retornado no corpo da resposta contém o **IdToken** emitido pelo Cognito.
        ```json
        {
          "accessToken": "eyJhbGciOi...",
          "expiresIn": 3600,
          "userId": "uuid-do-usuario-no-cognito"
        }
        ```
*   **Respostas de Erro Mapeadas:**
    *   **`400 Bad Request`** (Campos obrigatórios ausentes):
        ```json
        {
          "type": "https://gastosapp.dev/errors/bad-request",
          "title": "Parâmetros inválidos",
          "status": 400,
          "detail": "Email é obrigatório. / Senha é obrigatória."
        }
        ```
    *   **`401 Unauthorized`** (Credenciais inválidas / Usuário não encontrado):
        ```json
        {
          "type": "https://gastosapp.dev/errors/invalid-credentials",
          "title": "Email ou senha inválidos",
          "status": 401
        }
        ```

### 2.3. Obter Perfil do Usuário Autenticado (`GET /auth/me`)
Obtém informações do usuário extraídas diretamente das claims do token JWT.

*   **Endpoint:** `GET /auth/me`
*   **Cabeçalho Requerido:** `Authorization: Bearer <JWT_ID_TOKEN>`
*   **Regras de Validação:**
    *   A rota exige autenticação via middleware do ASP.NET Core (`RequireAuthorization`).
    *   A validação do token JWT realiza a verificação de assinatura contra o JWKS público da AWS, expiração e correspondência do client ID (Audience).
*   **Mapeamento de Claims:**
    O código realiza a leitura de claims de forma flexível suportando formatos padrão do Cognito e schemas do WS-Security:
    *   **User ID / Sub:** Lê do claim `"sub"` ou `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier`.
    *   **Email:** Lê do claim `"email"` ou `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress`.
    *   **Name:** Lê do claim `"name"` ou `http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name`.
*   **Resposta de Sucesso (`200 OK`):**
    ```json
    {
      "userId": "uuid-do-usuario-no-cognito",
      "email": "usuario@exemplo.com",
      "name": "Nome do Usuário (se disponível)"
    }
    ```
*   **Respostas de Erro Mapeadas:**
    *   **`401 Unauthorized`** (Token inválido, expirado ou claims críticas ausentes):
        ```json
        {
          "type": "https://gastosapp.dev/errors/unauthorized",
          "title": "Não autorizado",
          "status": 401
        }
        ```

---

## 3. Tratamento Global de Exceções & ProblemDetails

Todas as respostas de erro HTTP são unificadas através da classe [GlobalExceptionHandler.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Middlewares/GlobalExceptionHandler.cs). O Content-Type de retorno de erro é obrigatoriamente `application/problem+json`.

| Exceção de Negócio / Origem | Código HTTP | Título do Erro | Campo `type` do ProblemDetails |
| :--- | :--- | :--- | :--- |
| `ArgumentException` | `400 Bad Request` | "Parâmetros inválidos" | `https://gastosapp.dev/errors/bad-request` |
| `InvalidCredentialsException` (Originada por `NotAuthorizedException` ou `UserNotFoundException` do Cognito) | `401 Unauthorized` | "Email ou senha inválidos" | `https://gastosapp.dev/errors/invalid-credentials` |
| Falha na validação do JWT Bearer ou claim `sub` ausente | `401 Unauthorized` | "Não autorizado" | `https://gastosapp.dev/errors/unauthorized` |
| `EmailAlreadyExistsException` (Originada por `UsernameExistsException` do Cognito) | `409 Conflict` | "Email já cadastrado" | `https://gastosapp.dev/errors/email-already-exists` |
| Qualquer outra exceção não tratada | `500 Internal Server Error` | "Erro interno do servidor" | `https://gastosapp.dev/errors/internal-server-error` |

---

## 4. Integração do SDK AWS & Autenticação JWT

*   **Serviço Cognito:** O cliente SDK `IAmazonCognitoIdentityProvider` é injetado como Singleton em [AddCognitoSdk.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Extensions/AddCognitoSdk.cs).
*   **Autenticação de Middleware:** O pipeline de autenticação é adicionado via `AddJwtBearer` configurando:
    *   `Authority`: `https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}`
    *   `TokenValidationParameters`:
        *   `ValidateIssuerSigningKey = true`
        *   `ValidateIssuer = true`
        *   `ValidateAudience = true`
        *   `ValidAudience = {ClientId}`
        *   `ValidateLifetime = true`
