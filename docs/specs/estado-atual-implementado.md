# Estado Atual Implementado — GastosApp

Este documento registra o estado atual do desenvolvimento do backend do **GastosApp**, mapeando a arquitetura, projetos, endpoints, classes principais e cobertura de testes atuais.

---

## 1. Visão Geral da Solução
A solução está estruturada utilizando uma arquitetura em camadas inspirada em Clean Architecture e padrões CQS (Command Query Separation). O backend está atualizado para o **.NET 10** e utiliza **ASP.NET Core Minimal APIs**.

A autenticação e gestão de usuários é delegada diretamente ao **AWS Cognito**, e as configurações da aplicação são integradas ao **AWS Systems Manager (Parameter Store)**.

---

## 2. Estrutura de Projetos e Camadas

A solução contém os seguintes projetos no diretório `backend`:

*   **[GastosApp.Api](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/GastosApp.Api.csproj)**: Ponto de entrada da aplicação. Expõe os endpoints HTTP utilizando Minimal APIs, gerencia a autenticação JWT, documentação interativa com Scalar e tratamento global de erros.
*   **[GastosApp.Application](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/GastosApp.Application.csproj)**: Contém os casos de uso (Commands/Handlers), as validações de input, as abstrações de CQS (`ICommand`, `IQuery`) e definições de interfaces de serviços compartilhadas.
*   **[GastosApp.Domain](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Domain/GastosApp.Domain.csproj)**: Camada de domínio. Atualmente vazia (estrutura pronta), destinada no futuro a conter entidades, agregados, value objects e regras de negócio de gastos.
*   **[GastosApp.Infrastructure](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/GastosApp.Infrastructure.csproj)**: Implementação concreta das integrações externas. Contém a integração com o AWS Cognito, configuração de autenticação JWT Bearer com a nuvem e extensão de leitura do AWS Parameter Store.

---

## 3. Detalhes de Implementação por Camada

### 3.1. API ([GastosApp.Api](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api))
*   **[Program.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Program.cs)**:
    *   Carrega variáveis de configuração da AWS usando `AddAwsParameterStore()`.
    *   Adiciona documentação OpenAPI com interface de referência Scalar em ambiente de desenvolvimento (`MapScalarApiReference()`).
    *   Registra e configura o middleware do Serilog para logs estruturados no console.
    *   Habilita autenticação e autorização nativa do ASP.NET Core (`UseAuthentication()`, `UseAuthorization()`).
*   **Endpoints ([AuthEndpoints.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Endpoints/AuthEndpoints.cs))**:
    *   `POST /auth/register`: Recebe `RegisterRequest` (Email e Password), envia o command `RegisterUserCommand` via `ISender` (Mediator) e cria o usuário no Cognito. Mapeia o `Result` para `201 Created` ou erro.
    *   `POST /auth/login`: Recebe `LoginRequest` (Email e Password), envia o command `LoginUserCommand` via `ISender` e realiza o login com o fluxo `USER_PASSWORD_AUTH`. Retorna o `AccessToken` (IdToken do Cognito) e o tempo de expiração.
    *   `GET /auth/me`: Endpoint protegido que lê as claims `sub` (userId), `email` e `name` do JWT do usuário autenticado.
*   **Mapeamento Result → HTTP ([ResultHttpExtensions.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Common/ResultHttpExtensions.cs))**:
    *   `Result`/`Result<T>.ToHttpResult(...)` converte sucesso no `IResult` do endpoint, e falha em `ProblemDetails` (RFC 9457) conforme o `ErrorType`:
        *   `Conflict` $\rightarrow$ `409`, `Unauthorized` $\rightarrow$ `401`, `Validation` $\rightarrow$ `400`, `NotFound` $\rightarrow$ `404`, `Failure` $\rightarrow$ `500`
*   **Tratamento Global de Erros ([GlobalExceptionHandler.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Api/Middlewares/GlobalExceptionHandler.cs))**:
    *   Implementa `IExceptionHandler` e trata apenas exceções não previstas (bug/infra), retornando sempre `500 InternalServerError` formatado como `ProblemDetails` (RFC 9457). Erros de negócio não passam mais por exceções — são tratados via `Result`.

### 3.2. Application ([GastosApp.Application](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application))
*   **Mediator**: biblioteca [`Mediator`](https://github.com/martinothamar/Mediator) (martinothamar), via `Mediator.Abstractions` + `Mediator.SourceGenerator`. Substituiu as antigas abstrações CQS próprias (`ICommand`/`ICommandHandler`/`IQuery`/`IQueryHandler`), removidas de `Abstractions/`. Registrada via `AddMediator` em [ApplicationServiceCollectionExtensions.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs).
*   **Result Pattern**: implementação própria em [Common/Results](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Common/Results) (`Result`, `Result<T>`, `Error`, `ErrorType`). Handlers e serviços não lançam mais exceções para fluxo de negócio.
*   **Casos de Uso (Autenticação)**:
    *   **[RegisterUserCommand](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommand.cs)**: Realiza validações locais (se email e senha foram informados; se a senha tem pelo menos 8 caracteres) e dispara a criação através da abstração `IAuthService`, retornando `Result<RegisterUserResult>`.
    *   **[LoginUserCommand](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Auth/Commands/Login/LoginUserCommand.cs)**: Valida parâmetros obrigatórios e dispara o login no `IAuthService`, retornando `Result<LoginUserResult>`.
*   **Interfaces**:
    *   **[IAuthService](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Common/Interfaces/IAuthService.cs)**: Interface agnóstica de tecnologia para autenticação, retornando `Result<RegisterResult>`/`Result<LoginResult>`.
*   **Erros de negócio**: [AuthErrors.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Application/Auth/AuthErrors.cs) centraliza os `Error` de Auth (`EmailAlreadyExists`, `InvalidCredentials`, `Validation`).

### 3.3. Infrastructure ([GastosApp.Infrastructure](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure))
*   **Integração com AWS Cognito ([CognitoAuthService.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs))**:
    *   Utiliza o SDK `AWSSDK.CognitoIdentityProvider`.
    *   No cadastro (`RegisterAsync`), invoca `SignUpAsync` enviando o email como atributo. Trata duplicidades capturando `UsernameExistsException` e convertendo para `Result.Failure(AuthErrors.EmailAlreadyExists)`.
    *   No login (`LoginAsync`), inicia o fluxo com `InitiateAuthAsync` (usando `USER_PASSWORD_AUTH`), recupera o token, consulta os atributos do usuário via `GetUserAsync` e retorna `Result.Success` com o IdToken, a expiração e o identificador do usuário.
*   **Configuração de Infraestrutura e JWT Bearer ([AddCognitoSdk.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Extensions/AddCognitoSdk.cs))**:
    *   Carrega as opções fortemente tipadas `CognitoOptions` da seção `"Cognito"`.
    *   Registra a instância singleton de `IAmazonCognitoIdentityProvider`, configurada para usar credenciais locais via perfil AWS (`default`) ou chaves explícitas se configuradas.
    *   Adiciona a autenticação JwtBearer apontando para a autoridade real do pool do Cognito (`https://cognito-idp.{Region}.amazonaws.com/{UserPoolId}`).
    *   Customiza o evento `OnChallenge` para formatar a resposta 401 como `ProblemDetails`.
*   **AWS Systems Manager ([AwsParameterStoreExtensions.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/src/GastosApp.Infrastructure/Configuration/AwsParameterStoreExtensions.cs))**:
    *   Configura a leitura do Parameter Store apontando para o caminho `/GastosApp/`, configurado na região `us-east-1` usando o profile `default`.

---

## 4. Testes Automatizados (`tests/`)

A solução dispõe de projetos de teste em `backend/tests/`:

*   **[GastosApp.UnitTests](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.UnitTests/GastosApp.UnitTests.csproj)**:
    *   **[RegisterUserCommandHandlerTests.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.UnitTests/Application/RegisterUserCommandHandlerTests.cs)**: Valida as regras do handler de cadastro (campos nulos, tamanho mínimo da senha) e a chamada mockada para o `IAuthService`, checando o `Result` retornado.
    *   **[LoginUserCommandHandlerTests.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.UnitTests/Application/LoginUserCommandHandlerTests.cs)**: Valida as regras do handler de login (campos obrigatórios) e o retorno do serviço de auth via `Result`.
    *   **[ResultTests.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.UnitTests/Application/ResultTests.cs)**: Cobre o `Result`/`Result<T>` customizado (sucesso, falha, conversão implícita, acesso a `Value`).
    *   **[ResultHttpExtensionsTests.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.UnitTests/Api/ResultHttpExtensionsTests.cs)**: Cobre o mapeamento de cada `ErrorType` para o status HTTP/`ProblemDetails` esperado.
    *   **[GlobalExceptionHandlerTests.cs](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.UnitTests/Api/GlobalExceptionHandlerTests.cs)**: Cobre apenas o caso de exceção genérica não mapeada → 500.
*   **[GastosApp.IntegrationTests](file:///D:/git_jrneto/meus-gastos-pessoais/backend/tests/GastosApp.IntegrationTests/GastosApp.IntegrationTests.csproj)**:
    *   Estrutura inicializada com um arquivo vazio (`UnitTest1.cs`). Sem testes reais implementados até o momento.
