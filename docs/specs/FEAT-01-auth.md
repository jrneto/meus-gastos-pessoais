# FEAT-01: Autenticação (AWS Cognito) — Especificação Técnica (SDD)

Esta especificação técnica documenta a funcionalidade de Autenticação implementada no backend do **GastosApp**, refletindo exatamente o estado atual do código-fonte, contratos de API e regras de negócio codificadas.

---

## 1. Objetivo
Permitir que usuários se cadastrem, façam login e obtenham informações do próprio perfil autenticado a partir do **AWS Cognito**, validando as requisições subsequentes via JWT Bearer em ambientes locais e de produção.

---

## 2. Regras de Negócio Implementadas
*   **Identificação Única:** O e-mail do usuário é utilizado como identificador exclusivo de login. Tentativas de cadastro com e-mails duplicados são rejeitadas pelo Cognito (`409 Conflict`).
*   **Requisitos de Senha:** A validação na camada de aplicação exige que a senha tenha no mínimo 8 caracteres (retorna `400 Bad Request` caso não cumpra).
*   **Identificação Segura:** O identificador exclusivo do usuário (`userId`/`sub`) é extraído diretamente das claims do JWT (`sub` ou `NameIdentifier`), mitigando vulnerabilidades de injeção de parâmetros no corpo da requisição.
*   **Tempo de Vida do Token:** O token padrão emitido pelo Cognito expira em 1 hora (3600 segundos).

---

## 3. Contratos da API & Rotas

As Minimal APIs de autenticação estão mapeadas no arquivo **[AuthEndpoints.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs)** sob o prefixo `/auth`.

### 3.1. POST /auth/register
Cadastra um novo usuário diretamente no Pool de Usuários do AWS Cognito.

*   **Endpoint:** `POST /auth/register`
*   **Content-Type:** `application/json`
*   **Payload de Entrada (`RegisterRequest`):**
    ```json
    {
      "email": "usuario@email.com",
      "password": "SenhaSegura123"
    }
    ```
*   **Resposta de Sucesso (`201 Created`):**
    *   *Header:* `Location: /auth/me`
    *   *Body (`RegisterUserResult`):*
        ```json
        {
          "userId": "uuid-gerado-pelo-cognito",
          "email": "usuario@email.com"
        }
        ```
*   **Validações e Casos de Erro:**
    *   `email` ou `password` nulos/vazios $\rightarrow$ `400 Bad Request`
    *   `password` menor que 8 caracteres $\rightarrow$ `400 Bad Request`
    *   E-mail já existente no Cognito $\rightarrow$ `409 Conflict`

### 3.2. POST /auth/login
Autentica o usuário com email e senha no Cognito utilizando o fluxo `USER_PASSWORD_AUTH`.

*   **Endpoint:** `POST /auth/login`
*   **Content-Type:** `application/json`
*   **Payload de Entrada (`LoginRequest`):**
    ```json
    {
      "email": "usuario@email.com",
      "password": "SenhaSegura123"
    }
    ```
*   **Resposta de Sucesso (`200 OK`):**
    *   *Body (`LoginUserResult`):*
        *   *Nota:* O `accessToken` retornado é o **IdToken** gerado pelo Cognito, necessário para extrair as informações de perfil.
        ```json
        {
          "accessToken": "eyJhbGciOi...",
          "expiresIn": 3600,
          "userId": "uuid-do-usuario-no-cognito"
        }
        ```
*   **Validações e Casos de Erro:**
    *   `email` ou `password` nulos/vazios $\rightarrow$ `400 Bad Request`
    *   Credenciais inválidas (usuário não encontrado ou senha errada) $\rightarrow$ `401 Unauthorized`

### 3.3. GET /auth/me
Retorna os dados do usuário autenticado a partir das claims extraídas do token JWT Bearer.

*   **Endpoint:** `GET /auth/me`
*   **Requer Autorização:** Sim (`RequireAuthorization()`)
*   **Headers:** `Authorization: Bearer <JWT_ID_TOKEN>`
*   **Mapeamento de Claims:**
    *   `userId` $\leftarrow$ Claims `"sub"` ou `ClaimTypes.NameIdentifier`
    *   `email` $\leftarrow$ Claims `"email"` ou `ClaimTypes.Email`
    *   `name` $\leftarrow$ Claims `"name"` ou `ClaimTypes.Name`
*   **Resposta de Sucesso (`200 OK`):**
    ```json
    {
      "userId": "uuid-do-usuario-no-cognito",
      "email": "usuario@email.com",
      "name": "Nome do Usuário"
    }
    ```
*   **Casos de Erro:**
    *   Token ausente, inválido, expirado ou claim `sub` ausente $\rightarrow$ `401 Unauthorized`

---

## 4. Tratamento Global de Erros (RFC 9457)
Todas as falhas e erros de validação da API são interceptados pelo **[GlobalExceptionHandler.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Middlewares/GlobalExceptionHandler.cs)** e retornados em formato `ProblemDetails` (`application/problem+json`):

*   **ArgumentException (400 Bad Request):**
    ```json
    {
      "type": "https://gastosapp.dev/errors/bad-request",
      "title": "Parâmetros inválidos",
      "status": 400,
      "detail": "Mensagem detalhada do argumento (ex: Senha é obrigatória.)"
    }
    ```
*   **InvalidCredentialsException (401 Unauthorized):**
    ```json
    {
      "type": "https://gastosapp.dev/errors/invalid-credentials",
      "title": "Email ou senha inválidos",
      "status": 401
    }
    ```
*   **EmailAlreadyExistsException (409 Conflict):**
    ```json
    {
      "type": "https://gastosapp.dev/errors/email-already-exists",
      "title": "Email já cadastrado",
      "status": 409
    }
    ```
*   **Token Inválido / Ausente (401 Unauthorized):**
    ```json
    {
      "type": "https://gastosapp.dev/errors/unauthorized",
      "title": "Não autorizado",
      "status": 401
    }
    ```
*   **Erro Inesperado (500 Internal Server Error):**
    ```json
    {
      "type": "https://gastosapp.dev/errors/internal-server-error",
      "title": "Erro interno do servidor",
      "status": 500
    }
    ```

---

## 5. Mapeamento de Camadas e Componentes C#

*   **API ([GastosApp.Api](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api))**:
    *   [Program.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Program.cs): Habilita JwtBearer, Parameter Store e middlewares.
    *   [AuthEndpoints.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs): Rotas HTTP.
    *   [GlobalExceptionHandler.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Middlewares/GlobalExceptionHandler.cs): Tratamento global de exceções.
*   **Application ([GastosApp.Application](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application))**:
    *   [RegisterUserCommand.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommand.cs): Validações e payload de cadastro.
    *   [LoginUserCommand.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Auth/Commands/Login/LoginUserCommand.cs): Validações e payload de login.
    *   [IAuthService.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Common/Interfaces/IAuthService.cs): Abstração do provedor de identidade.
*   **Infrastructure ([GastosApp.Infrastructure](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure))**:
    *   [CognitoAuthService.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs): Implementa a integração concreta via AWS SDK.
    *   [AddCognitoSdk.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Extensions/AddCognitoSdk.cs): Configuração do contêiner DI para Cognito e injeção do JwtBearer configurando a autoridade de domínio real do pool Cognito.