# Plan — FEAT-41: Inativação de membros com transações lançadas

## Contexto técnico

`DELETE /members/{id}` (FEAT-20, `RemoveMemberCommandHandler`) hoje só
verifica se o alvo é o Titular antes de apagar o `Membership` de fato
(`IMembershipRepository.DeleteAsync`). Esta feature intercala uma nova
checagem: se o alvo é `Ativo` **e** já lançou pelo menos uma transação
na conta, a remoção vira uma inativação (`Status=Inativo`, item
preservado) em vez de um `DeleteItem`. Nenhuma tabela/índice novo é
necessário — a checagem "tem transação?" é feita com uma `Query` na
partição já existente (`PK=ACCOUNT#<accountId>`, mesmo padrão já usado
por `ITransactionRepository.QueryAsync`), e a inativação é um
`UpdateItem` de um único atributo (`Status`), reaproveitando a chave
estável (`SK=MEMBER#<membershipId>`) que a FEAT-20 já garante nunca
mudar.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | Novo valor `MembershipStatus.Inativo` |
| Application | `RemoveMemberCommandHandler` (nova ramificação), `UpdateMemberRoleCommandHandler` (novo bloqueio), `InviteMemberCommand`/`DynamoDbMembershipRepository.CreateInviteAsync` (duplicidade ignora `Inativo`), `ResolveMembershipQueryHandler` (só resolve `Ativo`), `IMembershipRepository`/`ITransactionRepository` (novo método cada), `MembershipErrors` (2 erros novos) |
| Infrastructure | `DynamoDbMembershipRepository.InactivateAsync` (novo), `DynamoDbMembershipRepository.CreateInviteAsync` (filtro de duplicidade), `DynamoDbTransactionRepository.ExistsByCreatedByUserIdAsync` (novo) |
| Api | Nenhuma mudança de rota/contrato — `MemberEndpoints.cs` já declara 422 em `PUT`/`DELETE /members/{id}`; só a `detail`/`type` do `ProblemDetails` muda conforme o novo `Error` retornado |

## Domain-layer

### `Membership.cs` (`GastosApp.Domain.Accounts`)

```csharp
public enum MembershipStatus
{
    Ativo,
    ConvitePendente,
    Inativo
}
```

Nenhum novo factory method: a transição `Ativo → Inativo` não passa por
`Membership.CreateX`, é uma mutação de atributo feita direto pelo
repositório (`InactivateAsync`), mesmo padrão já usado por
`AcceptPendingInvitesByEmailAsync` para `ConvitePendente → Ativo`
(`UpdateItem` de atributos, sem recriar o objeto de domínio no
caminho de escrita).

## Application-layer

### `MembershipErrors.cs` — dois erros novos

```csharp
public static Error CannotModifyInactiveMember => Error.UnprocessableEntity(
    "cannot-modify-inactive-member", "Não é possível alterar o papel de um membro inativo.");

public static Error MemberAlreadyInactive => Error.UnprocessableEntity(
    "member-already-inactive", "Este membro já está inativo.");
```

### `IMembershipRepository` — novo método

```csharp
// Transforma um membro Ativo em Inativo (UpdateItem de um atributo — SK nunca
// muda, mesmo padrão de AcceptPendingInvitesByEmailAsync). GSI1PK permanece
// USER#<userId> (decisão técnica 1) — quem passa a rejeitar um Inativo é
// ResolveMembershipQueryHandler, não a query em si.
Task<bool> InactivateAsync(string accountId, string membershipId, CancellationToken cancellationToken = default);
```

### `ITransactionRepository` — novo método

```csharp
// Existência de qualquer transação lançada por esse userId nesta conta —
// usado só por RemoveMemberCommandHandler pra decidir inativar em vez de
// remover de fato. Sem GSI dedicado (não introduzido nesta feature — ver
// "Recursos AWS"): Query por PK=ACCOUNT#<accountId>, FilterExpression
// CreatedByUserId, parando no primeiro item encontrado (mesmo padrão de
// paginação em loop de QueryAsync, MaxPaginationIterations).
Task<bool> ExistsByCreatedByUserIdAsync(string accountId, string userId, CancellationToken cancellationToken = default);
```

### `RemoveMemberCommandHandler` — nova ramificação

```csharp
public sealed class RemoveMemberCommandHandler : ICommandHandler<RemoveMemberCommand, Result>
{
    private readonly IMembershipRepository _membershipRepository;
    private readonly ITransactionRepository _transactionRepository;

    public RemoveMemberCommandHandler(
        IMembershipRepository membershipRepository, ITransactionRepository transactionRepository)
    {
        _membershipRepository = membershipRepository;
        _transactionRepository = transactionRepository;
    }

    public async ValueTask<Result> Handle(RemoveMemberCommand command, CancellationToken cancellationToken)
    {
        var membership = await _membershipRepository.GetByIdAsync(command.AccountId, command.MembershipId, cancellationToken);
        if (membership is null)
            return Result.Failure(MembershipErrors.NotFound);

        if (membership.Role == MembershipRole.Titular)
            return Result.Failure(MembershipErrors.CannotRemoveTitular);

        if (membership.Status == MembershipStatus.Inativo)
            return Result.Failure(MembershipErrors.MemberAlreadyInactive);

        if (membership.Status == MembershipStatus.Ativo)
        {
            var hasTransactions = await _transactionRepository.ExistsByCreatedByUserIdAsync(
                command.AccountId, membership.UserId!, cancellationToken);

            if (hasTransactions)
            {
                var inactivated = await _membershipRepository.InactivateAsync(
                    command.AccountId, command.MembershipId, cancellationToken);
                return inactivated ? Result.Success() : Result.Failure(MembershipErrors.NotFound);
            }
        }

        var deleted = await _membershipRepository.DeleteAsync(command.AccountId, command.MembershipId, cancellationToken);
        return deleted ? Result.Success() : Result.Failure(MembershipErrors.NotFound);
    }
}
```

`membership.UserId!` é seguro: `Status==Ativo` garante `UserId` presente
(única forma de um `Membership` ter `Status=Ativo`, ver `Membership.cs`/
`AcceptPendingInvitesByEmailAsync`). `ConvitePendente` cai direto no
`DeleteAsync` final (comportamento inalterado — nunca tem transação
possível, `UserId` é `null`).

### `UpdateMemberRoleCommandHandler` — novo bloqueio

Adiciona, logo após a checagem de Titular já existente:

```csharp
if (membership.Status == MembershipStatus.Inativo)
    return Result.Failure<MemberResult>(MembershipErrors.CannotModifyInactiveMember);
```

### `ResolveMembershipQueryHandler` — só resolve membro `Ativo`

```csharp
var membership = await _membershipRepository.FindByAccountAndUserIdAsync(accountId, query.UserId, cancellationToken);
if (membership is null || membership.Status != MembershipStatus.Ativo)
    return Result.Failure<ResolveMembershipResult>(AccountErrors.NotResolved);
```

Isso é o que faz um membro inativado perder acesso à conta (US4 da
spec): a partir da inativação, toda chamada autenticada dele contra
essa conta recebe 401 `account-not-found` — mesmo erro que já existe
hoje para um `Membership` ausente, sem precisar de nenhum
`ErrorType`/status novo.

### `CreatedByLabelResolver` — sem mudança de código

`FindByAccountAndUserIdAsync` já não filtra por `Status` (comentário
existente no arquivo já antecipava exatamente esta feature). Como
`InactivateAsync` preserva `GSI1PK=USER#<userId>`, o resolver encontra
o `Membership` `Inativo` normalmente e devolve `membership.Email` — só
o comentário do arquivo é atualizado (de "quando isso for implementado"
para uma referência a esta FEAT-41), sem mudança funcional.

### `DynamoDbMembershipRepository.CreateInviteAsync` — duplicidade ignora `Inativo`

```csharp
var existingMembers = await ListAsync(accountId, cancellationToken);
if (existingMembers.Any(m => m.Status != MembershipStatus.Inativo && NormalizeEmail(m.Email) == normalizedEmail))
    return MembershipWriteResult.EmailConflict();
```

Um novo convite pro mesmo e-mail cria um `Membership` novo
(`Guid` novo via `Membership.CreateInvite`), coexistindo com o
registro `Inativo` antigo — nenhuma outra mudança em `CreateInviteAsync`.

### `DynamoDbMembershipRepository.InactivateAsync` — novo

```csharp
public async Task<bool> InactivateAsync(string accountId, string membershipId, CancellationToken cancellationToken = default)
{
    try
    {
        await _dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _options.TableName,
            Key = ItemKey(accountId, membershipId),
            UpdateExpression = "SET #status = :inativo",
            ExpressionAttributeNames = new Dictionary<string, string> { ["#status"] = "Status" },
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":inativo"] = new AttributeValue { S = MembershipStatus.Inativo.ToString() },
                [":ativo"] = new AttributeValue { S = MembershipStatus.Ativo.ToString() }
            },
            ConditionExpression = "attribute_exists(PK) AND #status = :ativo",
            ReturnValues = ReturnValue.NONE
        }, cancellationToken);

        return true;
    }
    catch (ConditionalCheckFailedException)
    {
        // Item sumiu ou deixou de ser Ativo entre o GetByIdAsync do handler e
        // este UpdateItem (corrida rara) — mesmo tratamento de "não achou" que
        // DeleteAsync já dá hoje.
        return false;
    }
}
```

`GSI1PK`/`GSI1SK`/`UserId`/`Role`/`Email`/`CreatedAt` propositalmente
**não** mudam (decisão técnica 1) — só `Status`.

### `DynamoDbTransactionRepository.ExistsByCreatedByUserIdAsync` — novo

```csharp
public async Task<bool> ExistsByCreatedByUserIdAsync(string accountId, string userId, CancellationToken cancellationToken = default)
{
    Dictionary<string, AttributeValue>? exclusiveStartKey = null;
    var iterations = 0;

    while (true)
    {
        iterations++;
        if (iterations > MaxPaginationIterations)
        {
            throw new InvalidOperationException(
                "Número máximo de iterações de paginação excedido ao verificar transações do membro.");
        }

        var response = await _dynamoDbClient.QueryAsync(new QueryRequest
        {
            TableName = _options.TableName,
            KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
            FilterExpression = "CreatedByUserId = :userId",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
                [":skPrefix"] = new AttributeValue { S = "TXN#" },
                [":userId"] = new AttributeValue { S = userId }
            },
            ExclusiveStartKey = exclusiveStartKey
        }, cancellationToken);

        if (response.Items.Count > 0)
            return true;

        exclusiveStartKey = response.LastEvaluatedKey is { Count: > 0 } ? response.LastEvaluatedKey : null;
        if (exclusiveStartKey is null)
            return false;
    }
}
```

Reaproveita `MaxPaginationIterations` (já `private const` na classe).
`Query` por `PK` com `FilterExpression` — nunca `Scan` (regra da
constitution) — parando na primeira página que contiver ao menos um
item do autor, sem precisar ler a conta inteira quando o membro lançou
algo recentemente (`ScanIndexForward` não importa aqui, não há
`Limit`: cada página já é filtrada pelo DynamoDB antes de retornar).

## Api-layer

Nenhuma mudança. `MemberEndpoints.cs` já declara
`.ProducesProblem(StatusCodes.Status422UnprocessableEntity)` em
`PUT`/`DELETE /members/{id}` desde a FEAT-20 — os dois erros novos
reaproveitam esse mesmo status code, só com `type`/`detail` diferentes
no corpo. `GET /members` também não muda: `GetMembersQueryHandler` já
lista todo item `MEMBER#*` da partição, `Inativo` incluso, sem filtro
algum.

## Mapeamento de erros

| Situação | `Error` | `ErrorType` | HTTP |
|---|---|---|---|
| `PUT /members/{id}` sobre membro `Inativo` | `MembershipErrors.CannotModifyInactiveMember` (`cannot-modify-inactive-member`) | `UnprocessableEntity` | 422 |
| `DELETE /members/{id}` sobre membro já `Inativo` | `MembershipErrors.MemberAlreadyInactive` (`member-already-inactive`) | `UnprocessableEntity` | 422 |
| Chamada autenticada de um membro recém-inativado | `AccountErrors.NotResolved` (`account-not-found`, já existente) | `Unauthorized` | 401 |

Nenhum `ErrorType` novo — os dois erros novos reaproveitam
`UnprocessableEntity`, já mapeado em `ResultHttpExtensions.BuildProblem`.

## Recursos AWS

**Nenhum recurso novo.** Sem GSI adicional (a checagem de "tem
transação lançada" usa `Query` na partição base já existente, ver
`ExistsByCreatedByUserIdAsync` acima) e sem mudança de atributo
projetado em nenhum índice. `Status=Inativo` é só mais um valor
possível do atributo `Status`, já `String` livre no schema físico
(DynamoDB não impõe enum). Nenhuma alteração em
`infra-jrnexpenses/terraform/` nem em `backend/infra/terraform/`.

## `backend/docs/openapi.json`

Nenhum campo novo, nenhum status code novo nos endpoints afetados (422
já é documentado em `PUT`/`DELETE /members/{id}` desde a FEAT-20;
`status` de `MemberResult` já é `type: string` livre, sem enum
declarado no schema). **Diff esperado após regenerar: vazio** — ainda
assim, rodar `backend/scripts/export-openapi.sh` e confirmar
(`git diff --stat backend/docs/openapi.json`) continua sendo o passo
final de praxe, evitando qualquer surpresa não percebida.

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `RemoveMemberCommandHandlerTests` (existente, novos casos): membro
  `Ativo` sem transação → `DeleteAsync` chamado, sucesso; membro
  `Ativo` com transação → `InactivateAsync` chamado (não
  `DeleteAsync`), sucesso; membro já `Inativo` →
  `MemberAlreadyInactive`, nenhum repositório de escrita chamado;
  `ConvitePendente` → `DeleteAsync` chamado, sem checar transações
  (`ExistsByCreatedByUserIdAsync` nunca invocado — asserção de que o
  mock não foi chamado)
- `UpdateMemberRoleCommandHandlerTests` (existente, novo caso): membro
  `Inativo` → `CannotModifyInactiveMember`, `UpdateRoleAsync` nunca
  chamado
- `InviteMemberCommandHandlerTests`/`DynamoDbMembershipRepositoryTests`
  (existentes, novo caso): convite pro e-mail de um `Inativo` existente
  → sucesso (não é tratado como conflito)
- `ResolveMembershipQueryHandlerTests` (existente, novo caso): membro
  `Inativo` encontrado por `FindByAccountAndUserIdAsync` →
  `AccountErrors.NotResolved`, não o `Role` do membro
- `MembershipTests` (Domain, novo caso): `MembershipStatus.Inativo`
  round-trips via `Membership.Restore`
- Novo: `DynamoDbTransactionRepositoryTests` (ou arquivo próprio) para
  `ExistsByCreatedByUserIdAsync` — sem transação → `false`; com
  transação do autor → `true`; com transação de outro autor só → `false`

### Component tests (`backend/tests/GastosApp.ComponentTests/Members/MemberEndpointsTests.cs`)

- `DELETE /members/{id}` de membro `Ativo` com transação (mock de
  `ITransactionRepository.ExistsByCreatedByUserIdAsync` retornando
  `true`) → 204, `IMembershipRepository.InactivateAsync` chamado (não
  `DeleteAsync`)
- `DELETE /members/{id}` de membro `Ativo` sem transação → 204,
  `DeleteAsync` chamado (não `InactivateAsync`) — regressão do
  comportamento pré-FEAT-41
- `DELETE /members/{id}` de membro já `Inativo` → 422
  `member-already-inactive`
- `PUT /members/{id}` de membro `Inativo` → 422
  `cannot-modify-inactive-member`
- `GET /members` retornando um item `Inativo` do mock → aparece na
  resposta com `status: "Inativo"`

### Integration tests (`backend/tests/GastosApp.IntegrationTests/Members/MembersFlowTests.cs`)

Pelo menos um fluxo de sucesso ponta a ponta (obrigatório por
constitution): convidar membro → aceitar via login → lançar uma
transação como esse membro → Titular chama `DELETE /members/{id}` →
confirma 204 e que `GET /members` ainda lista o membro, agora
`Inativo` → confirma que uma nova chamada autenticada desse membro
(ex.: `GET /transactions`) retorna 401.

## Critical Files

- `backend/src/GastosApp.Domain/Accounts/Membership.cs`
- `backend/src/GastosApp.Application/Members/MembershipErrors.cs`
- `backend/src/GastosApp.Application/Members/Commands/RemoveMember/RemoveMemberCommand.cs`
- `backend/src/GastosApp.Application/Members/Commands/UpdateMemberRole/UpdateMemberRoleCommand.cs`
- `backend/src/GastosApp.Application/Members/Queries/ResolveMembership/ResolveMembershipQuery.cs`
- `backend/src/GastosApp.Application/Common/Interfaces/IMembershipRepository.cs`
- `backend/src/GastosApp.Application/Common/Interfaces/ITransactionRepository.cs`
- `backend/src/GastosApp.Application/Transactions/Common/CreatedByLabelResolver.cs` (só comentário)
- `backend/src/GastosApp.Infrastructure/Members/DynamoDbMembershipRepository.cs`
- `backend/src/GastosApp.Infrastructure/Transactions/DynamoDbTransactionRepository.cs`
- `backend/docs/data-model.md` (atualizar seção `Membership`: `Status`
  ganha `Inativo`; mencionar que `GSI1PK` permanece `USER#<userId>`
  mesmo depois da inativação)
- `backend/docs/backlog.md` (marcar o item como `[x]` ao concluir, com
  nota apontando pra esta FEAT-41)

## Verificação

1. `dotnet build GastosApp.sln`
2. `dotnet test GastosApp.sln` (unit + component; deve continuar 100%
   verde, sem quebrar nenhum teste existente de `/members`)
3. `backend/infra/lambda/run-local.sh` + suíte de integração
   (`--filter Category=Integration`) rodando localmente — obrigatório
   antes de considerar a feature concluída (constitution)
4. `backend/scripts/export-openapi.sh` + `git diff --stat
   backend/docs/openapi.json` — confirmar que o diff é vazio (esperado,
   ver seção acima)

## Decisões técnicas

1. **`GSI1PK` de um `Membership` `Inativo` permanece `USER#<userId>`,
   sem mudar pra nenhum outro formato.** A alternativa (esvaziar/mudar
   `GSI1PK` na inativação) exigiria que `CreatedByLabelResolver`
   passasse a buscar por outro caminho (`GetByIdAsync` não serve — ele
   não sabe o `membershipId`, só o `createdByUserId`), complicando um
   fluxo que hoje já funciona de graça. Em vez disso, quem passa a
   rejeitar o membro inativado é `ResolveMembershipQueryHandler`
   (checagem de `Status==Ativo` depois do `FindByAccountAndUserIdAsync`)
   — mudança de uma linha, sem tocar `Infrastructure`.
2. **Sem GSI novo para "existe transação deste autor".**
   `ExistsByCreatedByUserIdAsync` usa `Query` por `PK` (partição já
   existente) + `FilterExpression`, igual ao padrão já usado por
   `QueryAsync` sem filtro de categoria. Um GSI dedicado
   (`GSI3PK=ACCOUNT#<accountId>#<createdByUserId>`, por exemplo) seria
   mais barato em leitura para contas com muitas transações, mas é um
   recurso AWS novo — fora do escopo sem pedido explícito (ver
   `backend/docs/constitution.md`); registrar como possível melhoria
   futura se o custo de leitura vier a incomodar em produção.
3. **`InactivateAsync` tem `ConditionExpression` exigindo `Status=Ativo`
   no momento da escrita** (não só `attribute_exists(PK)`) — defesa
   contra uma corrida rara entre duas chamadas concorrentes de
   `DELETE /members/{id}` pro mesmo membro (ex.: duplo clique):
   a segunda falha com `ConditionalCheckFailedException` → `false` →
   `MembershipErrors.NotFound` no handler (mesmo efeito prático de
   "não achou nada pra fazer", sem uma mensagem de erro dedicada pra
   esse caso de corrida, que nunca é o caminho feliz).
4. **Nenhuma mudança em `MemberResult`/`GetMembersResult`.** `Status`
   já serializa via `ToString()` do enum (`MemberResult.FromEntity`) —
   o valor `"Inativo"` aparece automaticamente assim que o enum ganha o
   membro novo, sem tocar `AppJsonSerializerContext` nem nenhum DTO.

## Pontos que precisam de confirmação antes do `/tasks`

- Confirmar que **nenhum GSI novo** é aceitável para
  `ExistsByCreatedByUserIdAsync` mesmo sabendo que, em contas com
  muitas transações, o `Query`+`FilterExpression` lê mais itens do que
  um GSI dedicado leria (decisão técnica 2) — dado o histórico do
  projeto de não introduzir recurso AWS novo sem pedido explícito,
  assumi que sim, mas é uma escolha de custo/latência que vale
  confirmar.
- Confirmar que **não é necessário nenhum teste de carga/volume**
  específico para `ExistsByCreatedByUserIdAsync` (ex.: conta com
  centenas de transações) — o plano de testes acima cobre só
  corretude funcional (existe/não existe transação do autor).
