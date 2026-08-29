# Plan — FEAT-20: Membros da conta, convites e permissões

Decisão central desta feature (justificada na seção "Decisões
técnicas"): o `Membership` da FEAT-19 é **reformado** — ganha `Id`
próprio (gerado uma vez, estável para sempre, usado como sufixo do `SK`
e como o `id` público de `/members/{id}`) e `Email`, inclusive o item já
existente do `Titular`. Como o roadmap já decidiu que a tabela pode ser
recriada do zero (sem migração de dado real), essa reforma não exige
script de migração — só coordenar os pontos da FEAT-19 que hoje criam/
leem `Membership` (`DynamoDbAccountRepository.CreateAsync`,
`EnsureAccountCommand`, `AccountTriggerHandler`,
`LoginUserCommandHandler`) com a nova forma do item.

## 1. Camadas afetadas

### Domain — `GastosApp.Domain`

- `Accounts/Membership.cs` (reformado):
  ```csharp
  public enum MembershipRole { Titular, Leitura, Lancar, Total }
  public enum MembershipStatus { Ativo, ConvitePendente }

  public sealed class Membership
  {
      public string Id { get; }
      public string AccountId { get; }
      public string? UserId { get; }       // null enquanto ConvitePendente
      public string Email { get; }
      public MembershipRole Role { get; }
      public MembershipStatus Status { get; }
      public DateTimeOffset CreatedAt { get; }

      public static Membership CreateTitular(string accountId, string userId, string email);
      public static Membership CreateInvite(string accountId, string email, MembershipRole role);
      public static Membership Restore(string id, string accountId, string? userId, string email,
          MembershipRole role, MembershipStatus status, DateTimeOffset createdAt);
  }
  ```
  `CreateTitular`/`CreateInvite` geram `Id = Guid.NewGuid().ToString()`
  internamente (mesmo padrão de `Account.Create()`). Nenhum método de
  instância pra transição de estado (`Accept`/`ChangeRole`) — a troca de
  `Status`/`Role`/`UserId` é responsabilidade do repositório
  (`IMembershipRepository`), igual ao padrão já usado por
  `Category`/`Expense` (a entidade é reconstruída via `Restore` a cada
  leitura, sem mutação in-place).
- `MembershipTests.cs` (`UnitTests`) precisa ser reescrito pros novos
  factory methods (`Id`/`Email` agora fazem parte da assinatura).

### Application — `GastosApp.Application`

- **Novo** `Common/Interfaces/IMembershipRepository.cs`:
  ```csharp
  public sealed record MembershipWriteResult(MembershipWriteOutcome Outcome, Membership? Membership);
  public enum MembershipWriteOutcome { Success, EmailConflict, NotFound }
  public sealed record AcceptedInvite(string AccountId, DateTimeOffset CreatedAt);

  public interface IMembershipRepository
  {
      Task<IReadOnlyList<Membership>> ListAsync(string accountId, CancellationToken ct = default);
      Task<Membership?> GetByIdAsync(string accountId, string membershipId, CancellationToken ct = default);
      Task<Membership?> FindByAccountAndUserIdAsync(string accountId, string userId, CancellationToken ct = default);
      Task<MembershipWriteResult> CreateInviteAsync(string accountId, string email, MembershipRole role, CancellationToken ct = default);
      Task<MembershipWriteResult> UpdateRoleAsync(string accountId, string membershipId, MembershipRole role, CancellationToken ct = default);
      Task<bool> DeleteAsync(string accountId, string membershipId, CancellationToken ct = default);
      Task<IReadOnlyList<AcceptedInvite>> AcceptPendingInvitesByEmailAsync(string email, string userId, CancellationToken ct = default);
  }
  ```
- `Common/Interfaces/IAccountRepository.cs` (assinatura muda):
  ```csharp
  public interface IAccountRepository
  {
      Task<string?> FindAccountIdByUserIdAsync(string userId, CancellationToken ct = default);
      Task<CreateAccountResult> CreateAsync(string userId, string email, CancellationToken ct = default); // +email
      Task SetActiveAccountAsync(string userId, string accountId, CancellationToken ct = default);          // novo
  }
  ```
- `Accounts/Commands/EnsureAccount/EnsureAccountCommand.cs`: ganha
  `Email`:
  ```csharp
  public sealed record EnsureAccountCommand(string UserId, string Email) : ICommand<Result<EnsureAccountResult>>;
  ```
  Handler passa `command.Email` pra `_accountRepository.CreateAsync`.
- **Novo** `Members/Queries/ResolveMembership/ResolveMembershipQuery.cs`
  — substitui `Accounts/Queries/ResolveAccountId/ResolveAccountIdQuery.cs`
  (arquivo removido, único consumidor era o filtro da Api):
  ```csharp
  public sealed record ResolveMembershipQuery(string UserId) : IQuery<Result<ResolveMembershipResult>>;
  public sealed record ResolveMembershipResult(string AccountId, string MembershipId, MembershipRole Role);
  ```
  Handler: `IAccountRepository.FindAccountIdByUserIdAsync` → `null` vira
  `AccountErrors.NotResolved` (401, mesmo código de erro de hoje). Achou
  → `IMembershipRepository.FindByAccountAndUserIdAsync(accountId, userId)`
  → `null` (inconsistência de dado — não deveria ocorrer em uso normal,
  mesma postura defensiva da FEAT-19) também vira
  `AccountErrors.NotResolved`. Achou → `Success(new
  ResolveMembershipResult(accountId, membership.Id, membership.Role))`.
- **Novo** `Members/Commands/AcceptPendingInvites/AcceptPendingInvitesCommand.cs`:
  ```csharp
  public sealed record AcceptPendingInvitesCommand(string UserId, string Email) : ICommand<Result<AcceptPendingInvitesResult>>;
  public sealed record AcceptPendingInvitesResult(string? SwitchedToAccountId);
  ```
  Handler: `IMembershipRepository.AcceptPendingInvitesByEmailAsync(email, userId)`
  → lista vazia → `Success(new(null))`. Lista não vazia → escolhe o item
  de `CreatedAt` mais alto, chama
  `IAccountRepository.SetActiveAccountAsync(userId, escolhido.AccountId)`,
  retorna `Success(new(escolhido.AccountId))`. Nunca falha por regra de
  negócio — mesmo espírito de `EnsureAccountCommand` (só propaga exceção
  de infraestrutura genuína, capturada por quem despacha).
- `Auth/Commands/Login/LoginUserCommand.cs`
  (`LoginUserCommandHandler.Handle`): depois do bloco existente que
  despacha `EnsureAccountCommand(result.Value.UserId, command.Email)`
  (assinatura atualizada), acrescenta um segundo bloco `try/catch`
  despachando `AcceptPendingInvitesCommand(result.Value.UserId,
  command.Email)` — mesmo padrão de log-e-nunca-propaga. Ordem
  importa: primeiro garante a conta própria (idempotente, normalmente
  no-op), depois processa convites pendentes (pode trocar a conta
  ativa).
- **Novo** `Members/Commands/InviteMember/InviteMemberCommand.cs`:
  ```csharp
  public sealed record InviteMemberCommand(string AccountId, string Email, string Role) : ICommand<Result<MemberResult>>;
  ```
  Handler: parse de `Role` (`Enum.TryParse<MembershipRole>`, já validado
  pelo Validator) → `IMembershipRepository.CreateInviteAsync(accountId,
  email, role)` → `EmailConflict` vira `MembershipErrors.AlreadyExists`
  (409), `Success` vira `MemberResult.FromEntity`.
  Validator (`InviteMemberCommandValidator`, FluentValidation): `Email`
  obrigatório + `.EmailAddress()`; `Role` obrigatório e restrito a
  `"Leitura"`/`"Lancar"`/`"Total"` (`.Must(r => r is "Leitura" or
  "Lancar" or "Total")`, mensagem "Papel de acesso inválido.").
- **Novo** `Members/Queries/GetMembers/GetMembersQuery.cs`:
  ```csharp
  public sealed record GetMembersQuery(string AccountId) : IQuery<Result<GetMembersResult>>;
  ```
  Handler: `IMembershipRepository.ListAsync(accountId)` →
  `GetMembersResult` com `items` mapeados via `MemberResult.FromEntity`,
  ordenados por `CreatedAt` (mesma ordem de criação, sem paginação —
  volume esperado é baixo, poucos membros por conta).
- **Novo** `Members/Commands/UpdateMemberRole/UpdateMemberRoleCommand.cs`:
  ```csharp
  public sealed record UpdateMemberRoleCommand(string AccountId, string MembershipId, string Role) : ICommand<Result<MemberResult>>;
  ```
  Handler: `IMembershipRepository.GetByIdAsync` → `null` →
  `MembershipErrors.NotFound` (404). Achou e
  `membership.Role == MembershipRole.Titular` →
  `MembershipErrors.CannotModifyTitular` (422). Senão,
  `UpdateRoleAsync` → `Success(MemberResult.FromEntity)`. Validator
  igual ao de `InviteMemberCommand` pro campo `Role`.
- **Novo** `Members/Commands/RemoveMember/RemoveMemberCommand.cs`:
  ```csharp
  public sealed record RemoveMemberCommand(string AccountId, string MembershipId) : ICommand<Result>;
  ```
  Handler: mesmo fluxo de busca prévia — `GetByIdAsync` → `null` → 404;
  `Role == Titular` → `MembershipErrors.CannotRemoveTitular` (422);
  senão `DeleteAsync` → `Success()`.
- **Novo** `Members/MemberResult.cs` (compartilhado entre os 4
  handlers acima):
  ```csharp
  public sealed record MemberResult(string Id, string Email, string Role, string Status, DateTimeOffset CreatedAt)
  {
      public static MemberResult FromEntity(Membership m) =>
          new(m.Id, m.Email, m.Role.ToString(), m.Status.ToString(), m.CreatedAt);
  }
  public sealed record GetMembersResult(IReadOnlyList<MemberResult> Items);
  ```
  `Role.ToString()`/`Status.ToString()` produzem exatamente
  `"Titular"`/`"Leitura"`/`"Lancar"`/`"Total"` e
  `"Ativo"`/`"ConvitePendente"` (nomes dos membros do enum escolhidos
  para bater 1:1 com o contrato da spec — sem `JsonStringEnumConverter`,
  mesma linha do resto do projeto que não expõe enum nenhum via JSON,
  ver `Expense.CategoryId`/FEAT-17).
- **Novo** `Members/MembershipErrors.cs`:
  ```csharp
  public static class MembershipErrors
  {
      public static Error NotFound => Error.NotFound("not-found", "Membro não encontrado.");
      public static Error AlreadyExists => Error.Conflict("member-already-exists", "Este e-mail já é membro desta conta.");
      public static Error CannotModifyTitular => Error.UnprocessableEntity("cannot-modify-titular", "O Titular da conta não pode ter o papel alterado.");
      public static Error CannotRemoveTitular => Error.UnprocessableEntity("cannot-remove-titular", "O Titular da conta não pode ser removido.");
      public static Error InsufficientPermission => Error.Forbidden("insufficient-permission", "Seu nível de acesso não permite esta ação.");
  }
  ```
- `Common/Results/Error.cs`: novo factory `Forbidden`:
  ```csharp
  public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);
  ```
- `Common/Results/ErrorType.cs`: novo membro `Forbidden`.

### Infrastructure — `GastosApp.Infrastructure`

- **Novo** `Members/DynamoDbMembershipRepository.cs` — ver modelo de
  dados na seção 2. Reaproveita `GSI1` (já provisionado), sem índice
  novo.
- `Accounts/DynamoDbAccountRepository.cs`:
  - `CreateAsync(userId, email, ct)`: o item de `Membership` (3º `Put`
    da transação) passa a gerar `Id = Guid.NewGuid().ToString()`,
    `SK = $"MEMBER#{membershipId}"` (era `MEMBER#{userId}`),
    `Email`, `Status = "Ativo"` — mantém `GSI1PK = USER#{userId}`,
    `GSI1SK = ACCOUNT#{accountId}`, `Role = "Titular"`.
  - **Novo** `SetActiveAccountAsync(userId, accountId, ct)`: `PutItem`
    incondicional sobrescrevendo o `AccountPointer`
    (`PK=USER#<userId>, SK=ACCOUNT#`) com o novo `AccountId` — troca
    deliberada de conta ativa, sem `ConditionExpression` (ao contrário
    de `CreateAsync`, aqui não há corrida a serializar: é uma
    sobrescrita de "última operação vence").
- `DependencyInjection/InfrastructureServiceCollectionExtensions.cs`:
  registra `IMembershipRepository → DynamoDbMembershipRepository`.

### Api — `GastosApp.Api`

- `Common/CurrentAccountContext.cs`: ganha `MembershipRole? Role` e
  `string? MembershipId` (preenchidos pelo filtro abaixo).
- `Common/ResolveAccountEndpointFilter.cs`: passa a despachar
  `ResolveMembershipQuery` (em vez de `ResolveAccountIdQuery`) e
  preenche `CurrentAccountContext.AccountId`/`Role`/`MembershipId` com
  o resultado. Continua sendo o único filtro aplicado
  `.RequireAuthorization().AddEndpointFilter<ResolveAccountEndpointFilter>()`
  nos três grupos (`/categories`, `/expenses`, e o novo `/members`) —
  nome da classe mantido (a responsabilidade "resolver o que esse
  request pode fazer nesta conta" continua cabendo nela).
- **Novo** `Common/RoleEndpointFilters.cs`:
  ```csharp
  public static class RoleEndpointFilters
  {
      public static Func<EndpointFilterInvocationContext, EndpointFilterDelegate, ValueTask<object?>> Require(
          params MembershipRole[] allowedRoles)
      {
          return async (context, next) =>
          {
              var currentAccount = context.HttpContext.RequestServices.GetRequiredService<CurrentAccountContext>();
              if (currentAccount.Role is null || !allowedRoles.Contains(currentAccount.Role.Value))
                  return Result.Failure(MembershipErrors.InsufficientPermission).ToHttpResult(() => Results.Ok());

              return await next(context);
          };
      }
  }
  ```
  Filtro leve (delegate inline, sem classe própria) aplicado só nas
  rotas que precisam restringir por papel — `RequireAuthorization()` +
  `ResolveAccountEndpointFilter` continuam cobrindo autenticação e
  resolução de conta/papel em todo o grupo; este filtro só decide
  permitir ou barrar.
- `Endpoints/CategoryEndpoints.cs`: `MapPost`/`MapPut`/`MapDelete`
  ganham `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total, MembershipRole.Titular))`
  + `.ProducesProblem(StatusCodes.Status403Forbidden)`. `MapGet`
  inalterado (qualquer papel autenticado já passa por
  `ResolveAccountEndpointFilter`).
- `Endpoints/ExpenseEndpoints.cs`: `MapPost` ganha
  `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Lancar, MembershipRole.Total, MembershipRole.Titular))`;
  `MapPut`/`MapDelete` ganham
  `.AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Total, MembershipRole.Titular))`;
  todos com `.ProducesProblem(StatusCodes.Status403Forbidden)`. `MapGet`
  (`/` e `/{id}`) inalterados.
- **Novo** `Endpoints/MemberEndpoints.cs` (`MapGroup("/members")`,
  mesmo esqueleto de `CategoryEndpoints.cs`):
  ```csharp
  var group = app.MapGroup("/members")
      .WithTags("Members")
      .RequireAuthorization()
      .AddEndpointFilter<ResolveAccountEndpointFilter>()
      .ProducesProblem(StatusCodes.Status401Unauthorized)
      .ProducesProblem(StatusCodes.Status500InternalServerError);

  group.MapGet("/", GetMembers); // qualquer papel

  group.MapPost("/", InviteMember)
      .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))
      .ProducesProblem(StatusCodes.Status403Forbidden);

  group.MapPut("/{id}", UpdateMemberRole)
      .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))
      .ProducesProblem(StatusCodes.Status403Forbidden);

  group.MapDelete("/{id}", RemoveMember)
      .AddEndpointFilter(RoleEndpointFilters.Require(MembershipRole.Titular))
      .ProducesProblem(StatusCodes.Status403Forbidden);
  ```
  Cada handler segue o padrão de `CategoryEndpoints` (injeta
  `CurrentAccountContext`/`ISender`, monta o Command/Query com
  `currentAccount.AccountId!`, `result.ToHttpResult(...)`).
  `InviteMemberRequest(string Email, string Role)`,
  `UpdateMemberRoleRequest(string Role)` como records no fim do
  arquivo (mesmo padrão de `CreateCategoryRequest`).
- `Common/AppJsonSerializerContext.cs`: novos
  `[JsonSerializable]` para `InviteMemberRequest`,
  `UpdateMemberRoleRequest`, `MemberResult`, `GetMembersResult`.
- `Program.cs`: sem mudança (`CurrentAccountContext` já registrado
  `Scoped` pela FEAT-19; `IMembershipRepository` entra via
  `AddInfrastructure`).

### `GastosApp.CognitoTriggers`

- `AccountTriggerHandler.cs`: além de `sub`, lê `email` de
  `evt.Request.UserAttributes` (atributo padrão do Cognito, sempre
  presente no evento de confirmação). Despacha
  `EnsureAccountCommand(userId, email)`. Se `email` vier ausente/vazio
  (não deveria acontecer — Cognito exige e-mail neste projeto, ver
  FEAT-01), loga e não despacha nada — mesma postura defensiva já usada
  pra `sub` ausente, não derruba a confirmação.

## 2. Modelo de dados (DynamoDB, tabela `GastosApp` já existente)

Nenhuma tabela nem índice novo — reaproveita `GSI1` (já provisionado,
`ALL`), agora com **dois formatos de `GSI1PK`** conforme o `Status` do
`Membership`.

### `Membership` (reformado — substitui o formato da FEAT-19)

| Atributo | Valor |
|---|---|
| `PK` | `ACCOUNT#<accountId>` |
| `SK` | `MEMBER#<membershipId>` — gerado uma vez na criação, **nunca muda** (mesmo durante a aceitação do convite) |
| `GSI1PK` | `USER#<userId>` quando `Status=Ativo` · `EMAIL#<emailNormalizado>` quando `Status=ConvitePendente` |
| `GSI1SK` | `ACCOUNT#<accountId>` (constante, nos dois estados) |
| `Email` | string, gravado como veio (exibido igual); comparações de duplicidade/normalização usam `email.Trim().ToLowerInvariant()` |
| `Role` | `"Titular"` \| `"Leitura"` \| `"Lancar"` \| `"Total"` |
| `Status` | `"Ativo"` \| `"ConvitePendente"` |
| `UserId` | presente só quando `Status=Ativo` |
| `CreatedAt` | ISO 8601 |

`membershipId` é o `Id` público retornado em toda resposta de
`/members` e usado em `/members/{id}` — decoupled de `userId`/`email`
de propósito, pra sobreviver à transição pendente→ativo sem invalidar
nenhum `id` que o Titular já tenha em mãos (ex.: numa lista renderizada
no frontend).

### Access patterns novos

| # | Query | Mecanismo |
|---|---|---|
| Listar membros da conta (`GET /members`) | `Query PK=ACCOUNT#<accountId>, begins_with(SK, "MEMBER#")` |
| Membro por id (`GET/PUT/DELETE /members/{id}`) | `GetItem PK=ACCOUNT#<accountId>, SK=MEMBER#<id>` — direto, sem GSI2 (diferente de Category/Expense: aqui o `id` já É o sufixo do `SK`) |
| Papel do chamador na conta ativa (todo request autenticado) | `Query GSI1, GSI1PK=USER#<userId> AND GSI1SK=ACCOUNT#<accountId>` (igualdade nos dois, no máximo 1 item) |
| Convites pendentes por e-mail (login) | `Query GSI1, GSI1PK=EMAIL#<emailNormalizado>` (sem condição em `GSI1SK` — pode haver convite em mais de uma conta) |

### Criação de convite (`CreateInviteAsync`)

1. `ListAsync(accountId)` (Query por `PK`, já necessário de qualquer
   forma) → verifica em memória se algum item já tem `Email`
   normalizado igual ao convidado (cobre Titular e membros existentes,
   pendentes ou ativos) → `MembershipWriteOutcome.EmailConflict` se
   achar.
2. Senão, `PutItem` do novo item (`Status=ConvitePendente`, sem
   `UserId`, `GSI1PK=EMAIL#<normalizado>`), `ConditionExpression:
   attribute_not_exists(PK)` como defesa (não é ela quem
   realisticamente barra corrida — `SK` usa um GUID novo).

Contas com poucas dezenas de membros no máximo (uso pessoal/familiar) —
`ListAsync` completo a cada convite é aceitável, evita manter um índice
adicional só pra unicidade de e-mail.

### Aceitação de convite no login (`AcceptPendingInvitesByEmailAsync`)

1. `Query GSI1PK=EMAIL#<emailNormalizado>` → 0..N itens
   (`Status=ConvitePendente` em 0 ou mais contas).
2. Pra cada item encontrado: `UpdateItem` setando `Status=Ativo`,
   `UserId=<resolvido>`, `GSI1PK=USER#<resolvido>` — só atributos,
   `PK`/`SK` não mudam, então **não precisa** do padrão delete+put usado
   por rename de `Category`/mudança de data de `Expense` (essa é a
   vantagem de já ter decidido que `SK` nunca muda pra `Membership`).
   Sem `ConditionExpression`: se dois logins concorrentes do mesmo
   e-mail corrida aqui, o pior caso é os dois fazerem o mesmo `UpdateItem`
   (idempotente, resultado final idêntico) — não há dado inconsistente
   possível.
3. Retorna a lista de `(AccountId, CreatedAt)` aceitos nesta chamada
   pro handler decidir qual vira a conta ativa (o de `CreatedAt` mais
   alto).

## 3. Decisões técnicas

**1. `Membership` ganha `Id` próprio e `SK` deixa de ser
`MEMBER#<userId>` (FEAT-19) para `MEMBER#<membershipId>`, inclusive
pro Titular.** Alternativa considerada: manter `SK=MEMBER#<userId>`
pros membros já ativos e só usar uma chave baseada em e-mail
temporariamente pros pendentes, replicando o padrão delete+put já usado
por `Category`/`Expense` quando a chave muda. Rejeitada porque o `id`
devolvido por `POST /members` precisa continuar válido depois que o
convite for aceito (o Titular pode ter esse `id` guardado numa tela já
carregada) — com `SK` fixo desde a criação, a aceitação vira um simples
`UpdateItem` de atributos, mais simples que a alternativa, não só mais
estável.

**2. `GSI1PK` muda de forma (`EMAIL#...` → `USER#...`) sem precisar de
`TransactWriteItems`/delete+put**, porque só atributos projetados mudam
— o par físico `PK`/`SK` (que define a localização real do item)
permanece o mesmo o tempo todo. Isso só é seguro porque `GSI1SK`
(`ACCOUNT#<accountId>`) já era estável nos dois estados — não há
nenhuma alteração de partição do índice, só de valor.

**3. `IMembershipRepository` separado de `IAccountRepository`.**
Mesma divisão de responsabilidade já usada pra
`ICategoryRepository`/`IExpenseRepository` (cada agregado, seu
repositório) — evita crescer `IAccountRepository` (que continua focado
em `Account`/`AccountPointer`) com toda a superfície de CRUD de
`Membership`. A criação do `Membership` do Titular continua dentro de
`DynamoDbAccountRepository.CreateAsync` (não migra pro repositório
novo) porque é parte da mesma transação atômica de 3 itens da FEAT-19 —
separar exigiria quebrar essa atomicidade ou introduzir uma segunda
`TransactWriteItems` coordenada entre dois repositórios, sem ganho
real.

**4. `ResolveAccountIdQuery` renomeada para `ResolveMembershipQuery`**
(retorna `AccountId` + `Role` + `MembershipId`, não só `AccountId`).
Único consumidor é `ResolveAccountEndpointFilter`, então o rename é
seguro (`git grep` confirma). Motivo: seu papel real passa a ser
"resolver o que esse `userId` pode fazer nesta conta", não só o
`accountId` — manter o nome antigo enquanto o retorno cresce ficaria
enganoso.

**5. Verificação de papel via filtro de endpoint delegate (`RoleEndpointFilters.Require`), não via `IEndpointFilter` com DI de
construtor.** Minimal API não permite parametrizar facilmente um
`IEndpointFilter` registrado via `AddEndpointFilter<T>()` com argumentos
diferentes por rota (os papéis permitidos variam por endpoint). Um
delegate factory resolvendo `CurrentAccountContext` via
`HttpContext.RequestServices` dentro do próprio delegate evita criar
uma classe nova por combinação de papéis, e é o padrão recomendado pela
própria documentação do ASP.NET Core pra filtros parametrizados.

**6. Autorização por papel nunca acontece dentro de Handler/Command.**
`RoleEndpointFilters.Require` barra a requisição antes de qualquer
`ISender.Send` — os Handlers de `Category`/`Expense`/`Members`
continuam sem saber que papéis existem (só recebem `AccountId` já
resolvido, igual à FEAT-19). Reforça a separação Api
(autenticação/autorização) vs. Application (regra de negócio) já
estabelecida.

**7. Sem checagem de existência de usuário Cognito no convite.**
`POST /members` aceita qualquer e-mail sintaticamente válido, sem
chamar `AdminGetUser`/`ListUsers` no Cognito pra confirmar que já existe
conta com esse e-mail — evita uma chamada de rede a mais (custo/latência)
e mantém o fluxo simples; se o e-mail nunca se cadastrar, o convite só
fica pendente pra sempre (ver spec.md, "Fora do escopo" — sem
expiração).

## 4. Recursos AWS usados ou afetados

**Nenhum recurso novo** (tabela, GSI, App Client do Cognito ou
parâmetro de Parameter Store) — reaproveita a tabela `GastosApp` e o
índice `GSI1` já provisionados (mesmo índice, uso dual-purpose descrito
na seção 2). **Correção pós-implementação:** esta seção originalmente
afirmava que nenhuma política Terraform precisaria mudar — **errado**,
só verificado de fato ao depurar um erro real em homologação (convite
ficava sempre `ConvitePendente` mesmo após o login do convidado). A
FEAT-20 é a primeira feature do projeto a chamar `UpdateItemAsync`
(`DynamoDbMembershipRepository.UpdateRoleAsync` e
`AcceptPendingInvitesByEmailAsync`) — a IAM Role de execução do Lambda
da Api (`gastos-app-api-lambda-exec{-hom}`, `lambda.tf` de cada
ambiente) só tinha `PutItem`/`GetItem`/`Query`/`DeleteItem`/
`TransactWriteItems`, sem `dynamodb:UpdateItem`. Corrigido adicionando
essa ação em `environments/{hom,prod}/lambda.tf` — exige
`terraform apply` (aprovação explícita do usuário, ainda não aplicado
no momento desta correção). `GastosApp.CognitoTriggers` não é afetado
por essa lacuna (nunca chama `UpdateItemAsync` — só
`EnsureAccountCommand`/`CreateAsync`).

## 5. Erros de negócio → `ErrorType`/HTTP

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `POST /members` pra e-mail já membro da conta | `member-already-exists` | `Conflict` | 409 |
| `POST`/`PUT /members` com `email`/`role` inválido | `validation-error` | `Validation` | 400 |
| `PUT`/`DELETE /members/{id}` com `id` inexistente na conta | `not-found` | `NotFound` | 404 |
| `PUT /members/{id}` no Titular | `cannot-modify-titular` | `UnprocessableEntity` | 422 |
| `DELETE /members/{id}` no Titular | `cannot-remove-titular` | `UnprocessableEntity` | 422 |
| Papel sem permissão pra ação (`/categories`, `/expenses`, `/members`) | `insufficient-permission` | **`Forbidden`** (novo) | 403 |

`ErrorType.Forbidden` é novo — `ResultHttpExtensions.BuildProblem` ganha
o caso `ErrorType.Forbidden => (StatusCodes.Status403Forbidden, "Acesso
negado", error.Message)`, mesmo padrão RFC 9457 dos demais (título fixo
genérico, mensagem específica em `detail`).

## 6. Testes (visão geral — detalhamento fica pro `tasks.md`)

- `ComponentTestWebApplicationFactory`: novo
  `IMembershipRepository MembershipRepositoryMock` (+
  `ResetMembershipRepositoryMock`). Mock padrão precisa resolver
  `FindByAccountAndUserIdAsync(qualquer accountId, qualquer userId)`
  retornando um `Membership` com `Role=Titular` — mesmo motivo já
  documentado no mock de `IAccountRepository` (FEAT-19): a maioria dos
  testes de `Category`/`Expense` já existentes não conhece papel algum
  e não deve precisar ser reescrita só por causa da checagem de
  permissão nova. Testes que precisam simular `Leitura`/`Lancar`/`Total`
  (403 esperado) sobrescrevem esse mock explicitamente.
- `MembershipTests.cs` (`UnitTests`): reescrito pros novos factory
  methods (`CreateTitular(accountId, userId, email)`,
  `CreateInvite(accountId, email, role)`, `Restore(...)` com `Id`).
- Novos `ComponentTests` (`Members/`): `POST`/`GET`/`PUT`/`DELETE
  /members` cobrindo toda a matriz de status da spec (201/200/204,
  400, 403, 404, 409, 422) — reaproveita
  `ComponentTestWebApplicationFactory`/`TestAuthHandler` já existentes.
- Novos `ComponentTests`/`UnitTests` pra `AcceptPendingInvitesCommand`
  (nenhum convite pendente → no-op; um convite → troca conta ativa;
  múltiplos convites em contas diferentes → escolhe o mais recente).
- `CategoryEndpointsTests`/`ExpenseEndpointsTests` existentes: casos
  novos cobrindo 403 pra papéis sem permissão em cada verbo de escrita
  (`Leitura`/`Lancar` em `/categories`; `Leitura` em `POST /expenses`;
  `Leitura`/`Lancar` em `PUT`/`DELETE /expenses`).
- `AccountTriggerHandlerTests` (`ComponentTests` da FEAT-19): ajustar
  pro `EnsureAccountCommand` agora exigir `Email` — incluir `email` nos
  `UserAttributes` do evento de teste.
- `LoginUserCommandHandlerTests`: novo caso cobrindo o despacho de
  `AcceptPendingInvitesCommand` (best-effort, não derruba login em
  falha) — mesmo padrão dos testes já existentes pra
  `EnsureAccountCommand`.
- Atualizar `backend/docs/openapi.json`: reflete os 3 endpoints novos
  de `/members`, os novos `403`/`409`/`422` nos endpoints já existentes
  de `/categories`/`/expenses`, e o novo formato de erro `Forbidden`.

## Pontos a confirmar antes do `/tasks`

1. Nome exato dos dois novos arquivos de e-mail normalizado
   (`EMAIL#<normalizado>` em `GSI1PK`) — confirmar que
   `Trim().ToLowerInvariant()` é suficiente (sem normalização de
   acentos, diferente do slug de `Category` — e-mail não costuma ter
   acento, e Cognito já trata e-mail como case-insensitive por padrão).
2. `MemberResult`/`GetMembersResult` não paginam (`GET /members`
   retorna a lista inteira) — confirmar que isso é aceitável dado o
   volume esperado (uso pessoal/familiar, poucas dezenas de membros no
   limite), diferente de `GetExpensesQuery` que já pagina.
3. `RoleEndpointFilters.Require` roda **depois** de
   `ResolveAccountEndpointFilter` na cadeia de filtros — confirmar que
   a ordem de registro (`AddEndpointFilter<ResolveAccountEndpointFilter>()`
   no grupo, `AddEndpointFilter(RoleEndpointFilters.Require(...))` na
   rota específica) garante essa ordem no ASP.NET Core Minimal API
   (grupo antes de rota — comportamento já assumido, mas vale validar
   com um teste de integração simples antes de escalar pra todas as
   rotas).
