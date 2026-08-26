# Plan: FEAT-21 — Categoria: tipo, orçamento e remoção de cor/ícone — Plano Técnico

## Contexto técnico

`spec.md` (revisado) fecha três mudanças no mesmo contrato de
`Category` (FEAT-16): adiciona `tipo` (`despesa`\|`receita`,
obrigatório) e `orcamentoMensalCents` (opcional, inteiro positivo em
centavos), adiciona filtro `GET /categories?tipo=`, e **remove `cor` e
`icone`** — campos que o design system atual não usa mais (categoria é
exibida com um avatar de letra derivado do `nome` no próprio frontend,
sem cor/ícone customizável nem armazenado). Nenhuma role nova (o
`POST`/`PUT /categories` já exige `Total`/`Titular` desde a FEAT-20).

Este plan substitui a versão anterior (escrita antes da spec remover
`cor`/`icone`) — toda referência a `Cor`/`Icone`/`HexColor` do plan
anterior foi removida.

Duas decisões técnicas centrais moldam este plano — nenhuma delas
visível a partir do `spec.md` (são detalhes de implementação):

1. **Colisão de nome com o atributo interno `Tipo` já existente no
   item de `Category`.** `DynamoDbCategoryRepository`/
   `DynamoDbExpenseRepository` já gravam um atributo `Tipo` — mas é o
   **discriminador de tipo de item** no `GSI2` compartilhado entre
   `Category` (`Tipo="categoria"`) e `Expense` (`Tipo="despesa"`), não
   o `tipo` de negócio desta feature (ver `backend/docs/data-model.md`,
   seção "Espaço de chave compartilhado"). Reaproveitar esse mesmo
   atributo pro novo campo de negócio quebraria esse discriminador. O
   novo atributo de negócio usa um nome diferente: **`TipoLancamento`**
   (valores `"despesa"`\|`"receita"`) — o discriminador `Tipo` continua
   existindo, inalterado, só de uso interno.
2. **Categorias já existentes em hom/prod não têm `TipoLancamento`** —
   apesar do roadmap permitir recriar a tabela do zero, a FEAT-19/20 na
   prática **não** recriaram a tabela (o discriminador `Tipo` acima já
   é tratado como `"categoria"` implícito quando ausente, exatamente
   por isso). Pra não quebrar `GET /categories` em produção logo após
   o deploy desta feature, `MapToCategory` trata a ausência do
   atributo como `"despesa"` implícito — mesmo padrão defensivo já
   estabelecido pro discriminador `Tipo`, e suposição razoável (todas
   as categorias hoje em produção são de despesa; `receita` nunca
   existiu antes desta feature). Essas mesmas categorias antigas também
   têm `Cor`/`Icone` gravados no item — como a remoção é só de
   contrato/leitura (ver ponto "Infrastructure-layer" abaixo), esses
   atributos ficam órfãos no item (nunca mais lidos nem escritos), sem
   necessidade de nenhum cleanup ativo. **Ver "Pontos a confirmar antes
   do `/tasks`"**.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| Domain | `Category` perde `Cor`/`Icone`; ganha `Tipo` (`string`) e `OrcamentoMensalCents` (`long?`) |
| Application | `CreateCategoryCommand`/`UpdateCategoryCommand`/`GetCategoriesQuery` (+ Validators, Results) perdem `Cor`/`Icone`, ganham `Tipo`/`OrcamentoMensalCents`/filtro; `ICategoryRepository.ListAsync`/`UpdateAsync` acompanham |
| Infrastructure | `DynamoDbCategoryRepository`: `BuildItem`/`MapToCategory` param de `Cor`/`Icone` removidos; novo atributo `TipoLancamento` (distinto do discriminador `Tipo`), `OrcamentoMensalCents` opcional, filtro de `tipo` em `ListAsync` |
| Api | `CategoryEndpoints`: `CreateCategoryRequest`/`UpdateCategoryRequest` perdem `Cor`/`Icone`, ganham `Tipo`/`OrcamentoMensalCents`; `GetCategories` ganha `[AsParameters] GetCategoriesRequest` (`tipo`) |
| AWS/Terraform | Nenhum recurso novo — mesma tabela `GastosApp` já existente, só atributos regulares (não indexados) |

## Domain-layer

`backend/src/GastosApp.Domain/Categories/Category.cs` (`Cor`/`Icone`
removidos, dois campos novos — sem enum, mesmo padrão que `Cor`/`Icone`
já seguiam: validação de formato fica inteiramente no Validator):

```csharp
public sealed class Category
{
    public string Id { get; }
    public string AccountId { get; }
    public string Nome { get; }
    public string Tipo { get; }                    // "despesa" | "receita"
    public long? OrcamentoMensalCents { get; }      // null = sem orçamento definido
    public DateTimeOffset CreatedAt { get; }

    private Category(
        string id, string accountId, string nome, string tipo,
        long? orcamentoMensalCents, DateTimeOffset createdAt)
    {
        Id = id; AccountId = accountId; Nome = nome;
        Tipo = tipo; OrcamentoMensalCents = orcamentoMensalCents; CreatedAt = createdAt;
    }

    public static Category Create(string accountId, string nome, string tipo, long? orcamentoMensalCents) =>
        new(Guid.NewGuid().ToString(), accountId, nome, tipo, orcamentoMensalCents, DateTimeOffset.UtcNow);

    public static Category Restore(
        string id, string accountId, string nome, string tipo,
        long? orcamentoMensalCents, DateTimeOffset createdAt) =>
        new(id, accountId, nome, tipo, orcamentoMensalCents, createdAt);
}
```

Sem `enum CategoryTipo`: diferente de `MembershipRole` (usado em
autorização, cross-cutting, com `RoleEndpointFilters`), `tipo` aqui é
só um atributo de dado sem lógica própria nesta feature — introduzir
um enum + mapeamento manual pra bater com `"despesa"`/`"receita"` em
minúsculo (diferente de `Role.ToString()`, que já bate 1:1 com
`"Titular"` capitalizado) seria complexidade sem benefício real. Se a
FEAT-22 precisar de comportamento por tipo (ex.: validar `Expense`
contra o tipo da categoria), promover pra enum ali é uma refatoração
pequena e isolada.

`CategorySlug` (unicidade de `nome`) não muda — nunca dependeu de
`Cor`/`Icone`.

## Application-layer

### `ICategoryRepository` — assinaturas alteradas
(`backend/src/GastosApp.Application/Common/Interfaces/ICategoryRepository.cs`)

```csharp
public interface ICategoryRepository
{
    Task<CategoryWriteResult> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> ListAsync(string accountId, string? tipo, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
    Task<CategoryWriteResult> UpdateAsync(
        string accountId, string categoryId, string nome, string tipo, long? orcamentoMensalCents,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
}
```
`CreateAsync`/`GetByIdAsync`/`DeleteAsync` não mudam de assinatura —
`Tipo`/`OrcamentoMensalCents` já vêm dentro do `Category` recebido por
`CreateAsync`.

### `CreateCategoryCommand`/`Result` (`Categories/Commands/CreateCategory/CreateCategoryCommand.cs`)

```csharp
public sealed record CreateCategoryCommand(
    string AccountId, string Nome, string Tipo, long? OrcamentoMensalCents)
    : ICommand<Result<CreateCategoryResult>>;
```
Handler: `Category.Create(command.AccountId, command.Nome, command.Tipo, command.OrcamentoMensalCents)`
— resto do fluxo (outcome switch) inalterado.

```csharp
public record CreateCategoryResult(
    string Id, string Nome, string Tipo, long? OrcamentoMensalCents, DateTimeOffset CreatedAt)
{
    public static CreateCategoryResult FromEntity(Category category) => new(
        category.Id, category.Nome, category.Tipo, category.OrcamentoMensalCents, category.CreatedAt);
}
```
Mesma mudança espelhada em `UpdateCategoryCommand`/`UpdateCategoryResult`
(`Categories/Commands/UpdateCategory/UpdateCategoryCommand.cs`) — só
que `UpdateAsync` passa a receber `command.Tipo`/`command.OrcamentoMensalCents`
no lugar de `command.Cor`/`command.Icone`.

### Validators (`CreateCategoryCommandValidator`/`UpdateCategoryCommandValidator`)

Regras de `Cor`/`Icone` (incluindo o `HexColor` regex) são removidas
por completo. `Nome` mantém as mesmas regras de hoje (obrigatório, até
50 caracteres, slug não-vazio). Duas regras novas, idênticas nos dois
validators:

```csharp
RuleFor(c => c.Tipo)
    .NotEmpty().WithMessage("Tipo é obrigatório.")
    .Must(tipo => tipo is "despesa" or "receita")
        .WithMessage("Tipo deve ser \"despesa\" ou \"receita\".");

RuleFor(c => c.OrcamentoMensalCents)
    .GreaterThan(0).WithMessage("Orçamento mensal deve ser um valor positivo em centavos.")
    .When(c => c.OrcamentoMensalCents is not null);
```
Comparação de `Tipo` é case-sensitive (mesmo padrão já usado pro
`Role` de `Membership` na FEAT-20 — `"Leitura"/"Lancar"/"Total"` exatos,
sem normalização de caixa).

### `GetCategoriesQuery` (`Categories/Queries/GetCategories/GetCategoriesQuery.cs`)

```csharp
public sealed record GetCategoriesQuery(string AccountId, string? Tipo) : IQuery<Result<GetCategoriesResult>>;

public sealed class GetCategoriesQueryHandler : IQueryHandler<GetCategoriesQuery, Result<GetCategoriesResult>>
{
    public async ValueTask<Result<GetCategoriesResult>> Handle(GetCategoriesQuery query, CancellationToken ct)
    {
        var categories = await _categoryRepository.ListAsync(query.AccountId, query.Tipo, ct);
        return Result.Success(GetCategoriesResult.FromEntities(categories));
    }
}

public sealed record CategorySummary(
    string Id, string Nome, string Tipo, long? OrcamentoMensalCents, DateTimeOffset CreatedAt)
{
    public static CategorySummary FromEntity(Category category) => new(
        category.Id, category.Nome, category.Tipo, category.OrcamentoMensalCents, category.CreatedAt);
}
```

### **Novo** `GetCategoriesQueryValidator` (`Categories/Queries/GetCategories/GetCategoriesQueryValidator.cs`)

Mirror de `GetExpensesQueryValidator` (primeiro precedente de Query com
Validator no projeto, FEAT-06):

```csharp
public sealed class GetCategoriesQueryValidator : AbstractValidator<GetCategoriesQuery>
{
    public GetCategoriesQueryValidator()
    {
        RuleFor(q => q.Tipo)
            .Must(tipo => tipo is null or "despesa" or "receita")
            .WithMessage("tipo deve ser \"despesa\" ou \"receita\".");
    }
}
```

### `ApplicationServiceCollectionExtensions.AddApplicationServices` — novo registro manual

```csharp
services.AddScoped<IValidator<GetCategoriesQuery>, GetCategoriesQueryValidator>();
```
(`CreateCategoryCommandValidator`/`UpdateCategoryCommandValidator` já
registrados — sem mudança de linha, só o conteúdo dos arquivos).

## Infrastructure-layer — `DynamoDbCategoryRepository`

Item model (atualizado):

| Atributo | Valor |
|---|---|
| `PK` | `ACCOUNT#{accountId}` (inalterado) |
| `SK` | `CAT#{CategorySlug.From(nome)}` (inalterado) |
| `GSI2PK` | `ID#{id}` (inalterado) |
| `Tipo` | `"categoria"` (discriminador interno, **inalterado** — não confundir com o campo de negócio) |
| `TipoLancamento` | **novo** — `"despesa"` \| `"receita"` |
| `OrcamentoMensalCents` | **novo**, opcional — atributo `N` (numérico), **omitido do item** quando não informado (não gravado como `NULL`) |
| `Cor`, `Icone` | **removidos do `BuildItem`** — deixam de ser gravados em categorias novas/atualizadas. Categorias já existentes mantêm esses atributos no item (não há `TransactWriteItems`/migração pra apagá-los), mas `MapToCategory` para de lê-los — viram lixo inofensivo no item, nunca mais expostos pela API |
| `Nome`, `CreatedAt` | inalterados |

```csharp
private static Dictionary<string, AttributeValue> BuildItem(Category category, string sk)
{
    var item = new Dictionary<string, AttributeValue>
    {
        ["PK"] = new AttributeValue { S = $"ACCOUNT#{category.AccountId}" },
        ["SK"] = new AttributeValue { S = sk },
        ["GSI2PK"] = new AttributeValue { S = $"ID#{category.Id}" },
        ["Nome"] = new AttributeValue { S = category.Nome },
        ["Tipo"] = new AttributeValue { S = TipoCategoria },              // discriminador, inalterado
        ["TipoLancamento"] = new AttributeValue { S = category.Tipo },   // novo, campo de negócio
        ["CreatedAt"] = new AttributeValue { S = category.CreatedAt.ToString("O") }
    };

    if (category.OrcamentoMensalCents is { } orcamento)
        item["OrcamentoMensalCents"] = new AttributeValue { N = orcamento.ToString(CultureInfo.InvariantCulture) };

    return item;
}

private static Category MapToCategory(Dictionary<string, AttributeValue> item)
{
    var pk = item["PK"].S;
    var accountId = pk[(pk.IndexOf('#') + 1)..];
    var gsi2pk = item["GSI2PK"].S;
    var id = gsi2pk[(gsi2pk.IndexOf('#') + 1)..];
    var createdAt = DateTimeOffset.Parse(item["CreatedAt"].S, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    // Ausência de TipoLancamento == categoria gravada antes desta feature — tratada como
    // "despesa" implícito (mesma postura defensiva já usada pro discriminador Tipo acima;
    // ver "Contexto técnico", ponto 2).
    var tipo = item.TryGetValue("TipoLancamento", out var tipoAttr) ? tipoAttr.S : "despesa";
    var orcamentoMensalCents = item.TryGetValue("OrcamentoMensalCents", out var orcamentoAttr)
        ? long.Parse(orcamentoAttr.N, CultureInfo.InvariantCulture)
        : (long?)null;

    // Cor/Icone: se o item ainda os tiver (categoria antiga), são simplesmente ignorados —
    // não fazem mais parte de Category.
    return Category.Restore(id, accountId, item["Nome"].S, tipo, orcamentoMensalCents, createdAt);
}
```

### `ListAsync` — filtro de `tipo` em memória, sem `FilterExpression`

```csharp
public async Task<IReadOnlyList<Category>> ListAsync(string accountId, string? tipo, CancellationToken cancellationToken = default)
{
    var response = await _dynamoDbClient.QueryAsync(new QueryRequest
    {
        TableName = _options.TableName,
        KeyConditionExpression = "PK = :pk AND begins_with(SK, :skPrefix)",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":pk"] = new AttributeValue { S = $"ACCOUNT#{accountId}" },
            [":skPrefix"] = new AttributeValue { S = SkPrefix }
        }
    }, cancellationToken);

    var categories = response.Items.Select(MapToCategory);
    if (tipo is not null)
        categories = categories.Where(c => c.Tipo == tipo);

    return categories.ToList();
}
```
Filtragem depois do `MapToCategory` (não via `FilterExpression` do
DynamoDB) — decisão deliberada: o default "ausente = despesa" (ponto 2
do Contexto técnico) precisa se aplicar antes do filtro, senão uma
categoria antiga sem `TipoLancamento` nunca bateria num
`FilterExpression: TipoLancamento = :tipo`, mesmo quando `tipo=despesa`
(quebrando o mesmo default que a leitura sem filtro já aplica). Contas
têm poucas categorias (uso pessoal/familiar) — buscar todas via `Query`
por `PK` e filtrar em memória é desprezível em custo/latência, mesma
lógica já aceita em `ListAsync` de `Membership` na FEAT-20.

`UpdateAsync` recebe `tipo`/`orcamentoMensalCents` como parâmetros
novos (no lugar de `cor`/`icone`), repassados direto pro
`Category.Restore(...)` que monta `updated` (e, por consequência, pro
`BuildItem` chamado logo em seguida, que já não grava mais
`Cor`/`Icone`) — sem mudança na lógica de slug/rename/
`TransactWriteItems` já existente. Uma renomeação (`PUT` mudando
`nome`) de uma categoria antiga continua fazendo `Delete` do item
velho (que ainda tinha `Cor`/`Icone`) + `Put` do item novo (sem esses
atributos) — efeito colateral bem-vindo: renomear uma categoria antiga
já "limpa" os atributos órfãos dela.

## Api-layer

`backend/src/GastosApp.Api/Endpoints/CategoryEndpoints.cs`:

```csharp
public record CreateCategoryRequest(string Nome, string Tipo, long? OrcamentoMensalCents);
public record UpdateCategoryRequest(string Nome, string Tipo, long? OrcamentoMensalCents);
public record GetCategoriesRequest(string Tipo = "");
```
`Cor`/`Icone` removidos dos dois records. Um corpo de request que ainda
inclua esses campos (cliente antigo) não quebra — `System.Text.Json`
ignora propriedades desconhecidas por padrão, sem configuração extra
necessária (comportamento já usado implicitamente pelo projeto).

```csharp
private static async Task<IResult> GetCategories(
    [AsParameters] GetCategoriesRequest request,
    CurrentAccountContext currentAccount,
    ISender sender,
    CancellationToken cancellationToken)
{
    var query = new GetCategoriesQuery(currentAccount.AccountId!, NullIfEmpty(request.Tipo));
    var result = await sender.Send(query, cancellationToken);
    return result.ToHttpResult(value => Results.Ok(value));
}

private static string? NullIfEmpty(string value) => string.IsNullOrEmpty(value) ? null : value;
```
(mesmo helper local já usado em `ExpenseEndpoints.cs` — sem utilitário
compartilhado novo, mantendo o padrão atual do projeto).

`CreateCategory`/`UpdateCategory` passam `request.Tipo`/
`request.OrcamentoMensalCents` pro Command, sem mais mudanças
estruturais. `GetCategories` ganha
`.ProducesProblem(StatusCodes.Status400BadRequest)` (novo — antes não
tinha nenhuma validação de query, então nunca respondia 400).

### `AppJsonSerializerContext.cs`

Nenhum `[JsonSerializable]` novo — os mesmos tipos já registrados
(`CreateCategoryRequest`, `UpdateCategoryRequest`, `CreateCategoryResult`,
`UpdateCategoryResult`, `GetCategoriesResult`, `CategorySummary`) só
têm propriedades trocadas (`Cor`/`Icone` saem, `Tipo`/
`OrcamentoMensalCents` entram), o source generator já cobre.
`GetCategoriesRequest` é um tipo novo, mas é bindado via
`[AsParameters]` a partir da query string — não passa pelo
`JsonSerializerContext` (mesmo caso de `GetExpensesRequest`, que também
não está na lista).

## Mapeamento de erros

Nenhum `ErrorType`/`Error` novo — reaproveita `ErrorType.Validation`
(400) já existente:

| Cenário | `Error.Code` | `ErrorType` | HTTP |
|---|---|---|---|
| `tipo` ausente/vazio ou fora de `despesa`/`receita` (`POST`/`PUT`) | `validation-error` | `Validation` | 400 |
| `orcamentoMensalCents` igual a `0`, negativo (`PUT`/`POST`) | `validation-error` | `Validation` | 400 |
| `GET /categories?tipo=` fora de `despesa`/`receita` | `validation-error` | `Validation` | 400 |

Demais erros (`403 insufficient-permission`, `404 not-found`,
`422 name-conflict`) já existem e não mudam. Nenhum erro novo pra
`cor`/`icone` — como eles deixam de existir no contrato, não há mais
regra de validação pra remover; um corpo que ainda os envie
simplesmente os ignora (sem 400).

## Recursos AWS

Nenhum recurso novo. `TipoLancamento`/`OrcamentoMensalCents` são
atributos regulares (não projetados em nenhum GSI) — reaproveita a
tabela `GastosApp` já provisionada, sem alteração em
`backend/infra/terraform/`. A remoção de `Cor`/`Icone` também não
requer nenhuma mudança de infra — são só atributos que o código para
de gravar/ler, sem afetar índice ou schema (DynamoDB é schemaless por
item).

## Plano de testes

### Unit tests (`backend/tests/GastosApp.UnitTests/`)

- `Domain/CategoryTests.cs` (já existe, reescrever): remover
  asserções de `Cor`/`Icone`; `Create`/`Restore` com
  `tipo`/`orcamentoMensalCents` (incluindo `null`)
- `Application/CreateCategoryCommandValidatorTests.cs`/
  `UpdateCategoryCommandValidatorTests.cs` (já existem, reescrever):
  remover casos de `Cor`/`Icone` (formato hex, etc.); novos casos:
  `tipo` ausente/vazio/fora de `despesa`\|`receita` → inválido;
  `orcamentoMensalCents` `null` → válido; `0`/negativo → inválido;
  positivo → válido
- **Novo** `Application/GetCategoriesQueryValidatorTests.cs`: `tipo`
  `null` → válido; `"despesa"`/`"receita"` → válido; qualquer outro
  valor → inválido
- `Application/CreateCategoryCommandHandlerTests.cs`/
  `UpdateCategoryCommandHandlerTests.cs` (já existem): ajustar
  construção do Command/mock pros novos parâmetros (sem
  `cor`/`icone`), sem novo cenário de outcome (nenhum outcome novo)
- `Application/GetCategoriesQueryHandlerTests.cs` (já existe): novo
  caso repassando `Tipo` pro repositório mockado
- `Infrastructure/DynamoDbCategoryRepositoryTests.cs` (já existe,
  reescrever): remover asserções de `Cor`/`Icone` em `BuildItem`;
  novos casos — `BuildItem` omite `OrcamentoMensalCents` quando `null`
  e inclui quando informado, e não grava mais `Cor`/`Icone`;
  `MapToCategory` default `"despesa"` quando `TipoLancamento` ausente
  do item (simulando categoria antiga) e ignora `Cor`/`Icone` caso
  ainda estejam no item; `ListAsync` com `tipo` filtra corretamente
  após o mapeamento (incluindo um item sem `TipoLancamento` no meio da
  lista, pra provar que o default participa do filtro)

### Component tests (`backend/tests/GastosApp.ComponentTests/Categories/CategoryEndpointsTests.cs`, já existe)

Remover os casos de validação de `Cor`/`Icone` (formato hex, ausência).
Novos casos cobrindo os critérios de aceite do `spec.md`: `POST`/`PUT`
com `tipo`/`orcamentoMensalCents` válidos (incluindo sem orçamento);
`tipo` inválido/ausente → 400; `orcamentoMensalCents` `0`/negativo →
400; `PUT` removendo orçamento existente (→ `null`); `PUT` por role
sem permissão → 403 (não-regressão, já coberto pela FEAT-20, só
confirmar que continua passando com o novo shape de request);
`GET /categories?tipo=despesa`/`?tipo=receita` → só os do tipo;
`GET /categories` sem `tipo` → todos; `?tipo=invalido` → 400; `POST`/
`PUT` enviando `cor`/`icone` no corpo → sucesso normal, resposta sem
esses campos (US11).

## Critical Files

- `backend/src/GastosApp.Domain/Categories/Category.cs`
- `backend/src/GastosApp.Application/Common/Interfaces/ICategoryRepository.cs`
- `backend/src/GastosApp.Application/Categories/Commands/CreateCategory/CreateCategoryCommand.cs`
- `backend/src/GastosApp.Application/Categories/Commands/CreateCategory/CreateCategoryCommandValidator.cs`
- `backend/src/GastosApp.Application/Categories/Commands/UpdateCategory/UpdateCategoryCommand.cs`
- `backend/src/GastosApp.Application/Categories/Commands/UpdateCategory/UpdateCategoryCommandValidator.cs`
- `backend/src/GastosApp.Application/Categories/Queries/GetCategories/GetCategoriesQuery.cs`
- `backend/src/GastosApp.Application/Categories/Queries/GetCategories/GetCategoriesQueryValidator.cs` (novo)
- `backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs` — registrar `GetCategoriesQueryValidator`
- `backend/src/GastosApp.Infrastructure/Categories/DynamoDbCategoryRepository.cs`
- `backend/src/GastosApp.Api/Endpoints/CategoryEndpoints.cs`
- Testes listados em "Plano de testes"

## Verificação

- `dotnet build backend/GastosApp.sln`
- `dotnet test backend/GastosApp.sln` — suíte completa, sem regressão
  em `Categories`/`Expenses`/`Members`
- `./scripts/export-openapi.sh` — regenera `backend/docs/openapi.json`
  (critério de aceite da constitution) — `git diff` deve mostrar
  remoção de `cor`/`icone` e adição de `tipo`/`orcamentoMensalCents`
  nos schemas de `Category`, além do novo parâmetro de query `tipo`
- Smoke manual (opcional, contra ambiente local
  `infra/README.md`/LocalStack): criar categoria `tipo=despesa` sem
  orçamento; criar `tipo=receita` com orçamento; editar orçamento de
  um valor pra outro; remover orçamento (`null`); filtrar
  `?tipo=receita`; tentar `tipo` inválido (400); tentar
  `orcamentoMensalCents=0` (400); enviar `cor`/`icone` no corpo e
  confirmar que são ignorados

## Decisões técnicas

1. **Novo atributo `TipoLancamento`, distinto do discriminador `Tipo`
   já existente** — confirmado, ver "Contexto técnico" ponto 1.
   Reaproveitar `Tipo` quebraria o discriminador do `GSI2` compartilhado
   com `Expense`.
2. **Sem enum `CategoryTipo` no Domain** — `Tipo` fica `string`,
   validado só no Validator, mesmo padrão que `Cor`/`Icone` já
   seguiam. Revisitar se a FEAT-22 precisar de lógica por tipo.
3. **Filtro de `tipo` em `ListAsync` é feito em memória, depois do
   mapeamento** (não `FilterExpression` do DynamoDB) — necessário pro
   default "ausente = despesa" participar corretamente do filtro; ver
   "Infrastructure-layer" acima.
4. **`OrcamentoMensalCents` omitido do item quando `null`** (não
   gravado como `NULL` do DynamoDB) — mais simples de checar ausência
   (`TryGetValue`) do que checar o tipo `NULL` explicitamente.
5. **`Cor`/`Icone` são removidos só de escrita/leitura do código —
   sem migração/cleanup ativo dos atributos órfãos** em categorias já
   gravadas. `BuildItem` simplesmente para de incluí-los; um `PutItem`
   completo (caso de "slug não mudou" em `UpdateAsync`) sobrescreve o
   item inteiro e por si só já os remove; uma renomeação (`Delete`+
   `Put`) também. Só uma categoria nunca mais editada mantém os
   atributos órfãos indefinidamente — inofensivo (nunca lidos,
   dispensam qualquer script de limpeza).

## Pontos a confirmar antes do `/tasks`

1. **Categorias já existentes em hom/prod sem `TipoLancamento` são
   lidas como `"despesa"` implícito** (ponto 2 do "Contexto técnico") —
   confirmar que essa suposição é aceitável (nenhuma categoria de
   receita existe hoje, então nenhuma categoria seria classificada
   errado). Alternativa mais rígida seria não aplicar nenhum default e
   aceitar que `GET /categories` quebre para essas categorias até
   serem editadas via `PUT` — rejeitada por ser uma regressão real em
   produção sem necessidade.
2. **Atributos órfãos `Cor`/`Icone` ficam nos itens antigos sem
   nenhuma limpeza ativa** (decisão técnica 5) — confirmar que isso é
   aceitável (não há script de migração nem `UpdateItem` para removê-los
   preventivamente; eles somem naturalmente só quando a categoria for
   editada de novo).
3. **Filtro de `tipo` via `Query` completo + filtro em memória** (sem
   `FilterExpression` nem índice novo) — confirmar que é aceitável
   dado o volume esperado (poucas categorias por conta, uso
   pessoal/familiar), mesmo raciocínio já aplicado a `ListAsync` de
   `Membership` na FEAT-20.
4. **Sem enum para `Tipo`** — confirmar que manter como `string` (sem
   introduzir `CategoryTipo` no Domain agora) é aceitável, mesmo
   sabendo que a FEAT-22 provavelmente vai precisar decidir algo
   parecido pra `Expense`/`Transação`.
