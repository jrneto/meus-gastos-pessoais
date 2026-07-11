# FEAT-03: Testes de Componente

## Contexto
Hoje existem dois projetos de teste:
- `GastosApp.UnitTests`: cobre Handlers, `Result`/`Result<T>`,
  `ResultHttpExtensions` e `CognitoAuthService`, sempre com as
  dependências diretas (ex.: `IAuthService`) substituídas por dublês via
  **NSubstitute**. Não exercitam o pipeline HTTP real (routing,
  middlewares, autenticação JWT, `GlobalExceptionHandler`).
- `GastosApp.IntegrationTests`: existe apenas como esqueleto —
  referencia só `GastosApp.Api`, não tem `Microsoft.AspNetCore.Mvc.Testing`,
  não tem NSubstitute/FluentAssertions e não contém nenhum teste real
  (`UnitTest1.cs` vazio).

Isso deixa uma lacuna: nenhum teste hoje valida que um endpoint, quando
chamado via HTTP real (Minimal API → Mediator → Handler →
`ResultHttpExtensions` → resposta), se comporta como documentado nas
specs (`FEAT-01-auth.md`), incluindo o comportamento de middlewares
(`GlobalExceptionHandler`, autenticação JWT) e o roteamento em si.

`FEAT-01-auth.md` já registra esse débito como critério de aceite em
aberto: *"Testes de integração cobrem register e login contra o Cognito
real (hoje: `GastosApp.IntegrationTests` está sem testes implementados)"*.
Esta spec não fecha esse débito (testes contra o Cognito real
permanecem fora de escopo — ver "Fora do escopo"), mas define e
implementa uma camada intermediária: **testes de componente**, que
validam o comportamento observável da API via HTTP, com os repositórios
(quando existirem) e as dependências externas (Cognito, e futuras
integrações) substituídos por dublês controlados pelo teste.

`GastosApp.IntegrationTests` (existente, hoje vazio) **não é alterado
por esta spec** — o destino desse projeto (remoção, reaproveitamento
para testes de integração reais contra a AWS, etc.) é uma decisão
futura do time, fora de escopo aqui. Esta spec cria um projeto novo e
independente, dedicado exclusivamente a testes de componente.

## Objetivo
Definir e implementar um padrão de **testes de componente** para a API
(`GastosApp.Api`), que:
- Sobem o pipeline ASP.NET Core real em memória (`WebApplicationFactory`),
  exercitando routing, DI, Mediator, middlewares e serialização JSON tal
  como em produção.
- Substituem por mocks (**NSubstitute**) apenas as dependências que
  cruzam a fronteira do processo: serviços de infraestrutura externa
  (`IAuthService`/Cognito hoje; gateways de pagamento, filas, etc. no
  futuro) e repositórios (DynamoDB — ainda não implementados, mas o
  padrão já cobre quando existirem).
- Validam os contratos HTTP documentados nas specs (`FEAT-01-auth.md`
  e futuras): status code, corpo de sucesso e corpo de erro RFC 9457,
  para os fluxos felizes e de erro de cada endpoint.

## Escopo do termo "teste de componente" neste projeto
- **Testes de componente** (este FEAT): usam `WebApplicationFactory<Program>`
  para subir a API inteira em memória; tudo dentro do processo
  (`GastosApp.Api` + `GastosApp.Application` + `GastosApp.Domain` +
  `GastosApp.Infrastructure`) é real, **exceto** as implementações que
  falam com sistemas externos (AWS Cognito, DynamoDB, etc.), que são
  substituídas por dublês registrados via `IServiceCollection` na
  factory de teste.
- **Testes de integração** (fora de escopo aqui, mantidos como débito
  documentado em `FEAT-01-auth.md`): os mesmos fluxos, mas contra os
  serviços reais da AWS (Cognito/DynamoDB), sem nenhum mock.
- **Testes unitários** (já existentes em `GastosApp.UnitTests`,
  inalterados por este FEAT): testam uma classe isolada (Handler,
  `Result`, `CognitoAuthService`) sem subir o pipeline HTTP.

## Decisões técnicas

### Projeto de testes
- Criar um projeto **novo**, `tests/GastosApp.ComponentTests`, incluído
  em `backend/GastosApp.sln`. Não reaproveita nem altera
  `GastosApp.IntegrationTests` (mantido como está; sua destinação futura
  é decisão do time, fora de escopo desta spec).
- Referências do `GastosApp.ComponentTests.csproj`:
  - `GastosApp.Api` (para `WebApplicationFactory<Program>`)
  - `xunit` + `Microsoft.NET.Test.Sdk` + `xunit.runner.visualstudio` +
    `coverlet.collector` (mesmas versões usadas nos demais projetos de
    teste)
  - `Microsoft.AspNetCore.Mvc.Testing` (mesma versão major do .NET 10
    usado no projeto)
  - `NSubstitute` (mesma versão usada em `GastosApp.UnitTests`, 5.3.0)
  - `FluentAssertions` (mesma versão usada em `GastosApp.UnitTests`,
    8.10.0)

### `Program` acessível para `WebApplicationFactory<T>`
- `GastosApp.Api` usa top-level statements; adicionar
  `public partial class Program { }` ao final de `Program.cs` para que
  `WebApplicationFactory<Program>` consiga referenciá-la a partir do
  projeto de testes.

### Substituição de dependências externas e repositórios
- Criar `ComponentTestWebApplicationFactory : WebApplicationFactory<Program>`
  em `GastosApp.IntegrationTests/Support/`.
- Via `ConfigureWebHost` → `ConfigureTestServices`, remover o registro
  real de cada dependência externa/repositório e registrar em seu lugar
  um dublê NSubstitute:
  - `IAuthService` → `Substitute.For<IAuthService>()`
  - Futuros: interfaces de repositório (ex.: `IExpenseRepository`) e
    outras integrações externas (gateways, filas, storage), seguindo o
    mesmo padrão assim que forem criadas.
- Os dublês são expostos como propriedades públicas na factory (ex.:
  `factory.AuthServiceMock`), para que cada teste configure o
  comportamento esperado (`Returns`, `Throws`) antes de disparar a
  requisição HTTP.
- Cada teste (ou classe de teste via `IClassFixture`/`ICollectionFixture`)
  usa `factory.CreateClient()` para obter um `HttpClient` que fala com a
  API em memória.

### Autenticação nos testes de componente
- Endpoints protegidos (ex.: `GET /auth/me`) exigem hoje validação real
  de JWT contra o JWKS do Cognito (`FEAT-01-auth.md`), o que não é
  viável nem desejável em testes de componente (dependeria de rede e de
  um usuário real no Cognito).
- Decisão: registrar, apenas no host de teste
  (`ConfigureTestServices`), um esquema de autenticação de teste
  (`AddAuthentication` com um `AuthenticationHandler` customizado,
  ex. `TestAuthHandler`) que:
  - É ativado somente dentro do `ComponentTestWebApplicationFactory`
    (nunca em `Program.cs`/produção).
  - Autentica automaticamente as requisições que enviarem um header
    convencionado (ex.: `Authorization: TestScheme <userId>|<email>|<name>`),
    populando as claims (`sub`, `email`, `name`) exatamente como o JWT
    real faria, para exercitar o restante do pipeline (extração de
    `userId`, `GET /auth/me`) sem depender do Cognito.
  - Requisições sem esse header seguem o fluxo normal de
    "não autenticado" → 401, permitindo testar o caso de token
    ausente/inválido também no nível de componente.

### Estrutura de suporte
- `GastosApp.ComponentTests/Support/ComponentTestWebApplicationFactory.cs`
- `GastosApp.ComponentTests/Support/TestAuthHandler.cs`
- `GastosApp.ComponentTests/Auth/AuthEndpointsTests.cs` (primeiro
  módulo coberto)
- Convenção de nome de arquivo/classe para próximos módulos:
  `<Modulo>/<Grupo>EndpointsTests.cs`

## Plano de Testes
Cobertura inicial (módulo Auth, único implementado hoje), via
`ComponentTestWebApplicationFactory` + `HttpClient`:
- `POST /auth/register`
  - Sucesso: `AuthServiceMock.RegisterAsync(...)` configurado para
    retornar `Result.Success`; espera 201, `Location: /auth/me`, corpo
    com `userId`/`email`.
  - Email duplicado: mock retorna `Result.Failure(AuthErrors.EmailAlreadyExists)`;
    espera 409 com `ProblemDetails` (`type` = `.../email-already-exists`).
  - Parâmetros inválidos (email/senha ausentes, senha curta): espera
    400 com `ProblemDetails` (`type` = `.../bad-request`), sem sequer
    chamar o mock (validação no Handler).
- `POST /auth/login`
  - Sucesso: mock retorna `Result.Success` com `accessToken`/`expiresIn`/`userId`;
    espera 200.
  - Credenciais inválidas: mock retorna `Result.Failure(AuthErrors.InvalidCredentials)`;
    espera 401 (`type` = `.../invalid-credentials`).
- `GET /auth/me`
  - Requisição com header de autenticação de teste válido: espera 200
    com `userId`/`email`/`name` extraídos das claims.
  - Requisição sem header de autenticação: espera 401
    (`type` = `.../unauthorized`).
- Teste de "smoke" do `GlobalExceptionHandler`: um endpoint/mock
  configurado para lançar uma exceção não mapeada retorna 500 com
  `ProblemDetails` genérico.

## Requisito para novos endpoints
A partir desta spec, testes de componente passam a ser parte da
**definição de pronto** de qualquer endpoint (regra registrada em
`docs/constitution.md`, seção "Regras imutáveis"):
- Nenhum endpoint novo é considerado implementado sem teste de
  componente cobrindo seu(s) fluxo(s) de sucesso e cada erro mapeado na
  spec correspondente.
- O padrão a seguir é o definido nesta spec:
  `ComponentTestWebApplicationFactory` para subir a API em memória,
  mocks via NSubstitute para repositórios e dependências externas
  registrados em `ConfigureTestServices`, e `TestAuthHandler` quando o
  endpoint exigir autenticação.
- Vale tanto para PRs que adicionam endpoints quanto para specs futuras
  (`FEAT-04` em diante), que devem incluir uma seção "Plano de Testes"
  (ou "Plano de Testes de Componente") cobrindo os novos endpoints, nos
  mesmos moldes da seção acima para o módulo Auth.

## Critérios de aceite
- [x] Projeto `tests/GastosApp.ComponentTests` criado e incluído em
      `backend/GastosApp.sln`, referenciando `Microsoft.AspNetCore.Mvc.Testing`,
      `NSubstitute` e `FluentAssertions`
- [x] `GastosApp.IntegrationTests` permanece inalterado
- [x] `Program.cs` de `GastosApp.Api` expõe `public partial class Program { }`
- [x] `ComponentTestWebApplicationFactory` sobe a API em memória e
      substitui `IAuthService` por um dublê NSubstitute configurável por
      teste
- [x] `TestAuthHandler` permite simular usuário autenticado sem depender
      do Cognito real
- [x] Testes de componente cobrem os 3 endpoints de Auth (sucesso e
      cada erro mapeado em `FEAT-01-auth.md`)
- [x] `dotnet test` roda os testes de componente junto com os demais,
      sem exigir rede ou credenciais AWS
- [x] Nenhum contrato de API existente foi alterado
- [x] `docs/constitution.md` atualizado com a regra de testes de
      componente obrigatórios para novos endpoints

## Detalhes de implementação
- `builder.Configuration.AddAwsParameterStore()` (Parameter Store real,
  `Optional = false`) é pulado em `Program.cs` quando
  `builder.Environment.IsEnvironment("Testing")`, condição ativada pela
  factory via `UseEnvironment("Testing")`. Sem esse guard, o host de
  teste tentaria uma chamada real ao AWS SSM ao subir.
- `ComponentTestWebApplicationFactory` injeta valores fake de
  `Cognito:Region`/`UserPoolId`/`ClientId` via configuração em memória,
  necessários para o binding de `CognitoOptions` e a construção do
  `IAmazonCognitoIdentityProvider` não falharem, mesmo sem `IAuthService`
  real sendo usado.
- O default authentication scheme é sobrescrito para `TestScheme` via
  `PostConfigure<AuthenticationOptions>` (roda depois de qualquer
  `Configure` feito em `Program.cs`), garantindo que `RequireAuthorization()`
  em `GET /auth/me` use o `TestAuthHandler` em vez do `JwtBearer` real.

## Status
Implementado. `GastosApp.ComponentTests` criado com
`ComponentTestWebApplicationFactory` + `TestAuthHandler`, cobrindo os 3
endpoints de Auth (sucesso e cada erro mapeado) e um smoke test do
`GlobalExceptionHandler`. Suíte completa (`dotnet test` na solução)
passa: 43/43 (1 IntegrationTests placeholder + 10 ComponentTests + 32
UnitTests).

## Fora do escopo
- Testes de integração contra o Cognito real ou contra DynamoDB real
  (permanecem como débito documentado em `FEAT-01-auth.md`)
- Testes de carga/performance
- Cobertura de módulos além de Auth (serão adicionados nos FEATs que os
  implementarem, seguindo o padrão aqui definido)
- Testes de componente para o front-end
