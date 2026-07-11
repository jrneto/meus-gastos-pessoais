# FEAT-02: Padrão Mediator + Result Pattern

## Contexto
A `backend/docs/constitution.md` (seção "Padrões de Código e Arquitetura") exige:
- **Mediator Pattern**: usar uma biblioteca de Mediator para desacoplar
  os Handlers de comandos/queries das Minimal APIs. As rotas devem
  apenas enviar (`Send`) o request para o mediator.
- **Result Pattern**: nenhum Handler ou serviço deve lançar exceções
  para fluxo de negócio ou retornar `null`. Todos devem retornar um
  `Result`/`Result<T>` unificado, e a Minimal API mapeia esse `Result`
  para o HTTP status code correspondente.
- **Result Pattern Customizado**: proibido usar libs externas de Result
  (ex: FluentResults) — a implementação de `Result`/`Result<T>` deve ser
  própria e simples, dentro do projeto.

O código atual (módulo Auth, único implementado até o momento) não segue
nenhuma das duas regras:
- Usa abstrações CQS próprias (`ICommand<T>`/`ICommandHandler<T,R>`,
  `IQuery<T>`/`IQueryHandler<T,R>`) em vez de uma lib de Mediator.
- `RegisterUserCommandHandler`, `LoginUserCommandHandler` e
  `CognitoAuthService` lançam exceções de negócio
  (`ArgumentException`, `EmailAlreadyExistsException`,
  `InvalidCredentialsException`) capturadas centralmente pelo
  `GlobalExceptionHandler` e convertidas em `ProblemDetails`.

Isso já estava documentado como **débito técnico explícito** em
`backend/specs/FEAT-01-auth/spec.md` (seção Application). Esta spec define o
padrão arquitetural (válido para todas as features futuras) e aplica a
migração ao módulo Auth, que é o único código existente hoje.

## Objetivo
- Definir, como padrão arquitetural do projeto, o uso de uma biblioteca
  de Mediator e de um `Result`/`Result<T>` customizado, substituindo
  exceções de negócio e as abstrações CQS caseiras.
- Migrar o módulo Auth (único já implementado) para esse padrão, sem
  alterar nenhum contrato de API já publicado em `FEAT-01-auth.md`
  (mesmos paths, payloads, status codes e corpos de erro RFC 9457).

## Decisões técnicas

### Biblioteca de Mediator
- Escolhida: **[`Mediator`](https://github.com/martinothamar/Mediator)**
  (autor martinothamar) — implementação via source generator (sem
  reflection), gratuita e open-source. Evita depender do MediatR, que
  passou a exigir licença comercial paga para uso empresarial.
- Pacotes NuGet a adicionar (nomes a confirmar na implementação, versão
  compatível com `net10.0`): `Mediator.SourceGenerator` e
  `Mediator.Abstractions`, referenciados em
  `GastosApp.Application.csproj`.

### Result Pattern customizado
Novo namespace `GastosApp.Application.Common.Results`, sem dependências
externas:
- `Result`: `IsSuccess` / `IsFailure`, `Error?`
  - `Result.Success()`
  - `Result.Failure(Error error)`
- `Result<T>` (herda/compõe `Result`): adiciona `Value` (só acessível
  quando `IsSuccess`)
  - `Result<T>.Success(T value)`
  - `Result<T>.Failure(Error error)`
- `Error`: record `(string Code, string Message, ErrorType Type)`
- `ErrorType` (enum): `Validation`, `Conflict`, `Unauthorized`, `NotFound`, `Failure`
  - Mapeamento fixo `ErrorType → HTTP status`:
    `Validation → 400`, `Unauthorized → 401`, `Conflict → 409`,
    `NotFound → 404`, `Failure → 500`

### Mapeamento Result → HTTP (Minimal API)
- Método de extensão (ex.: `Result.ToProblemHttpResult()` /
  `Result<T>.ToProblemHttpResult()`) em `GastosApp.Api`, responsável por:
  - Em sucesso: devolver o `IResult` apropriado (`Results.Ok(value)`,
    `Results.Created(...)`, conforme o endpoint).
  - Em falha: montar um `ProblemDetails` com `Status` derivado do
    `ErrorType`, `Title` = `Error.Message`, e `Type` seguindo o padrão já
    usado (`https://gastosapp.dev/errors/{slug}`), preservando os slugs
    existentes: `email-already-exists`, `invalid-credentials`,
    `bad-request`, `unauthorized`.
- `GlobalExceptionHandler` passa a tratar **apenas** exceções não
  previstas (bugs, falhas de infraestrutura não mapeadas) → sempre 500.
  Deixa de existir o `switch` para `EmailAlreadyExistsException`,
  `InvalidCredentialsException` e `ArgumentException`, pois essas deixam
  de ser lançadas no fluxo de negócio.

### Mediator: substituição das abstrações
- `ICommand<TResult>`/`ICommandHandler<TCommand,TResult>` e
  `IQuery<TResult>`/`IQueryHandler<TQuery,TResult>` (hoje em
  `GastosApp.Application/Abstractions`) são removidos e substituídos
  pelas interfaces da lib `Mediator` (`IRequest<TResponse>` /
  `IRequestHandler<TRequest,TResponse>`, e equivalentes de query se/quando
  necessário).
- Os nomes dos Commands existentes são preservados
  (`RegisterUserCommand`, `LoginUserCommand`), apenas trocando a
  interface base e o tipo de retorno do handler, que passa a ser
  `Task<Result<TResult>>` em vez de `Task<TResult>`.
- Registro do Mediator via `services.AddMediator(...)`, centralizado em
  `ApplicationServiceCollectionExtensions` — mesmo local onde hoje estão
  os `AddScoped<ICommandHandler...>`.
- Minimal API (`AuthEndpoints.cs`) passa a injetar `ISender` em vez dos
  handlers concretos, chamando apenas `sender.Send(command, ct)` e
  mapeando o `Result` retornado para `IResult`.

## Migração do módulo Auth (aplicação prática)

| Componente | Antes | Depois |
|---|---|---|
| `RegisterUserCommandHandler` | `Task<RegisterUserResult>`, lança `ArgumentException` | `Task<Result<RegisterUserResult>>`, retorna `Result.Failure` com `ErrorType.Validation` |
| `LoginUserCommandHandler` | `Task<LoginUserResult>`, lança `ArgumentException` | `Task<Result<LoginUserResult>>`, idem |
| `IAuthService.RegisterAsync` | lança `EmailAlreadyExistsException` | retorna `Result<RegisterResult>` com `ErrorType.Conflict` |
| `IAuthService.LoginAsync` | lança `InvalidCredentialsException` | retorna `Result<LoginResult>` com `ErrorType.Unauthorized` |
| `CognitoAuthService` | captura exceções do SDK AWS e relança exceções de domínio | captura exceções do SDK AWS e converte para `Result.Failure(Error...)` na fronteira Infrastructure → Application |
| `AuthEndpoints.cs` | injeta `ICommandHandler<T,R>`, chama `HandleAsync` | injeta `ISender`, chama `Send`, mapeia `Result` → `IResult` |
| `GET /auth/me` | monta `ProblemDetails` manualmente para 401 | passa a usar o mesmo mapeamento `Result → ProblemDetails` (a definir se via Result ou mantendo tratamento local, já que não passa por um Command) |

Os contratos HTTP documentados em `FEAT-01-auth.md` (paths, request/response
JSON, status codes, `Type` URIs de erro) **não mudam**.

Após a implementação, `backend/specs/FEAT-01-auth/spec.md` deve ser atualizado
para remover a nota de débito técnico ("Os handlers lançam exceções em
vez de usar o Result Pattern...") e refletir o novo fluxo baseado em
`ISender`/`Result`.

## Plano de Testes
- `RegisterUserCommandHandlerTests` / `LoginUserCommandHandlerTests`:
  trocar asserts de `ThrowAsync<...>` por `result.IsFailure` e
  `result.Error.Type`/`result.Error.Code` esperados.
- `CognitoAuthServiceTests`: idem, verificar `Result.Failure` com o
  `ErrorType` esperado (`Conflict` para email duplicado, `Unauthorized`
  para credenciais inválidas) em vez de `ThrowAsync`.
- `GlobalExceptionHandlerTests`: reduzir para cobrir apenas o caso 500
  (exceção genérica não mapeada); remover os casos 409/401/400 que hoje
  dependem de exceções de negócio.
- Novos testes unitários para `Result`/`Result<T>`: construção via
  `Success`/`Failure`, acesso a `Value` só quando `IsSuccess`,
  comportamento de `Error`.
- Novo teste para o mapeamento `Result → HTTP` (`ToProblemHttpResult`):
  cada `ErrorType` produz o status/Type esperado.
- Testes de integração de Auth (ainda não implementados) continuam fora
  do escopo desta spec — serão cobertos futuramente.

## Critérios de aceite
- [x] `Mediator` (martinothamar) referenciado nos `.csproj` e registrado
      via DI (`AddMediator`)
- [x] `Result`/`Result<T>` customizado criado em
      `GastosApp.Application.Common.Results`, sem dependência de libs
      externas de Result
- [x] `ICommand`/`ICommandHandler`/`IQuery`/`IQueryHandler` próprios
      removidos de `GastosApp.Application/Abstractions`
- [x] Handlers de Auth não lançam mais exceções para fluxo de negócio
- [x] `IAuthService`/`CognitoAuthService` retornam `Result`/`Result<T>`
      em vez de lançar `EmailAlreadyExistsException`/`InvalidCredentialsException`
- [x] Endpoints de Auth usam `ISender` + mapeamento `Result → HTTP`
- [x] Contratos HTTP inalterados (mesmos paths/payloads/status codes/Type URIs)
- [x] Testes existentes migrados para o novo padrão (sem `ThrowAsync` de
      exceções de negócio)
- [x] `backend/specs/FEAT-01-auth/spec.md` atualizado, removendo a nota de
      débito técnico

## Fora do escopo
- Novas features ou mudanças de contrato de API
- Migração de `IQuery`/`IQueryHandler` para casos de uso reais — ainda
  não há nenhuma Query implementada; a spec só define o padrão para
  quando existirem
- Testes de integração de Auth contra o Cognito real (ver `FEAT-01-auth.md`)

## Status
Implementado. Módulo Auth migrado para `Mediator` (martinothamar) +
`Result`/`Result<T>` customizado. Build e suíte de testes
(`dotnet test`) passam (32/32).