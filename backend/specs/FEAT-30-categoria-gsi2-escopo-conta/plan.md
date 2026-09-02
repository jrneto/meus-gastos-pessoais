# Plan — FEAT-30: Categoria — escopar busca por ID (GSI2) por conta

O usuário confirmou que garantirá as tabelas `GastosApp`/
`GastosApp-Hom`/`GastosApp-Local` zeradas (sem dado de categoria
pré-existente) em todos os ambientes antes do deploy desta correção —
por isso o plano abaixo **não inclui backfill nem estratégia de
transição/leitura dupla**: a mudança de `GSI2PK` é um cutover simples,
sem relação de ordem entre deploy e dado. O outro requisito explícito
do usuário — manter `backend/docs/data-model.md` atualizado — é
tratado como critério de aceite desta feature (seção 5).

## 1. Camadas afetadas

### Domain — `GastosApp.Domain`
Nenhuma mudança. `Category` continua sem saber nada sobre `GSI2PK` —
isso é decisão de Infrastructure.

### Application — `GastosApp.Application`
Nenhuma mudança de assinatura. `ICategoryRepository.GetByIdAsync`/
`UpdateAsync`/`DeleteAsync` já recebem `accountId` hoje — o bug é
inteiramente interno à implementação Infrastructure, que hoje ignora
esse `accountId` na busca por `GSI2` e só o usa depois, tarde demais,
num post-check.

### Infrastructure — `GastosApp.Infrastructure`

- **`Categories/CategoryItemMapper.cs`** — `BuildItem`: `GSI2PK` passa
  de `$"ID#{category.Id}"` para `$"ID#{category.AccountId}#{category.Id}"`.
  Único ponto de escrita do formato do item `Category` (compartilhado
  com o seed de `DynamoDbAccountRepository`, FEAT-28) — a mudança
  cobre os dois caminhos de escrita automaticamente, sem tocar em
  `DynamoDbAccountRepository.cs`.

- **`Categories/DynamoDbCategoryRepository.cs`**:
  - `LookupByIdAsync` ganha `accountId` como parâmetro (hoje só recebe
    `categoryId`) e passa a consultar `GSI2` por
    `GSI2PK = "ID#<accountId>#<categoryId>"` em vez de
    `GSI2PK = "ID#<categoryId>"` — `Limit = 1` continua correto, agora
    sem ambiguidade possível, já que a chave é única por conta+categoria.
  - `GetByIdAsync`/`UpdateAsync`/`DeleteAsync`: passam `accountId` para
    `LookupByIdAsync` e **removem** o post-check `if (pk !=
    $"ACCOUNT#{accountId}") return null/NotFound` — fica redundante,
    já que `LookupByIdAsync` agora só pode devolver um item que já
    pertence à conta do chamador (é a própria condição da `Query`).
  - `MapToCategory`: extração do `id` a partir do `GSI2PK` troca
    `IndexOf('#')` por `LastIndexOf('#')` — o `id` é sempre o último
    segmento, mais robusto caso o formato precise mudar de novo no
    futuro (sem necessidade prática nesta feature, já que não há mais
    item em formato antigo a conviver).

### Api — `GastosApp.Api`
Nenhuma mudança — nenhum endpoint, request/response ou status code
muda (confirmado também no `spec.md`, seção "Contratos da API").

## 2. Contratos técnicos

### `LookupByIdAsync` (privado, `DynamoDbCategoryRepository`) — nova assinatura

```csharp
private async Task<(string Pk, string Sk)?> LookupByIdAsync(
    string accountId, string categoryId, CancellationToken cancellationToken)
{
    var lookup = await _dynamoDbClient.QueryAsync(new QueryRequest
    {
        TableName = _options.TableName,
        IndexName = Gsi2Index,
        KeyConditionExpression = "GSI2PK = :gsi2pk",
        ExpressionAttributeValues = new Dictionary<string, AttributeValue>
        {
            [":gsi2pk"] = new AttributeValue { S = $"ID#{accountId}#{categoryId}" }
        },
        Limit = 1
    }, cancellationToken);

    if (lookup.Items.Count == 0)
        return null;

    return (lookup.Items[0]["PK"].S, lookup.Items[0]["SK"].S);
}
```

Praticamente idêntico ao método atual — só o valor de `:gsi2pk` muda,
incorporando `accountId`.

### DynamoDB — `GSI2` de `Category` (entrada/saída)

| | Antes | Depois |
|---|---|---|
| `GSI2PK` gravado (`BuildItem`) | `ID#<categoryId>` | `ID#<accountId>#<categoryId>` |
| Query de busca (`LookupByIdAsync`) | `GSI2PK = "ID#<categoryId>"`, `Limit=1`, **sem filtro de conta** (bug: pode achar item de outra conta) | `GSI2PK = "ID#<accountId>#<categoryId>"`, `Limit=1`, sempre da conta certa por construção |

Nenhuma mudança na definição da `GSI2` em si (continua hash-key-only,
`GSI2PK`, `Projection: KEYS_ONLY`) — só no formato do valor gravado.
Sem impacto em `Transaction`, que continua gravando/lendo
`GSI2PK = "ID#<transactionId>"` (fora de escopo, ver `spec.md`).

## 3. Decisões técnicas

**1. `GSI2PK` de categoria ganha `accountId` (`ID#<accountId>#<categoryId>`),
em vez da correção rápida de tirar `Limit=1` e filtrar em memória.**
Decisão já fechada com o usuário (ver `backlog.md`/`spec.md`): filtrar
em memória teria custo de `Query` crescente por conta e não resolve a
causa raiz (o índice continuaria compartilhando espaço de chave entre
contas). Incluir `accountId` na chave torna a busca precisa por
construção, sem exigir nenhuma lógica de desambiguação depois.

**2. Sem backfill nem leitura dupla/transição.** Diferente de toda
migração anterior de `GSI1`/`GSI2` neste projeto (que já podia assumir
tabela vazia ou recriável), aqui haveria dado real em homologação/
produção — mas o usuário confirmou que zerará as tabelas de todos os
ambientes antes do deploy desta correção. Com isso, a mudança de
schema é um cutover simples: não há item em formato antigo para
conviver com o código novo, então `LookupByIdAsync` não precisa de
nenhum fallback nem de nenhuma ordem específica entre "código no ar" e
"dado migrado" (elimina inteiramente a complexidade que uma versão
anterior deste plano havia desenhado para esse cenário).

**3. `MapToCategory` usa `LastIndexOf('#')` em vez de `IndexOf('#')`
para extrair o `id`.** Mudança pequena, feita já nesta feature por
estar no mesmo método sendo tocado — deixa a extração correta
independentemente de quantos `#` o `GSI2PK` tiver, útil se o formato
precisar mudar de novo no futuro (não é estritamente necessária hoje,
já que não há mais formato antigo a conviver, mas é defensiva e de
custo zero).

## 4. Recursos AWS usados ou afetados

**Nenhum recurso novo, nenhuma mudança de recurso existente.**
Reaproveita a tabela `GastosApp`/`GastosApp-Hom`/`GastosApp-Local` e a
`GSI2` já provisionadas — só o valor gravado em `GSI2PK` muda. Sem
Terraform, sem IAM, sem script administrativo (o usuário cuida de
zerar as tabelas por fora desta feature).

## 5. Documentação de dados (`backend/docs/data-model.md`)

Requisito explícito do usuário: a documentação de dados nunca fica
divergente do que o código grava. Atualizar a seção `Category`:

- Linha do `GSI2PK`: `ID#<id>` → `ID#<accountId>#<categoryId>`,
  ajustando também a frase que descreve o mecanismo (deixa de ser só
  "resolve a partir do `id`" e passa a "resolve a partir de
  `accountId` + `id`, sem ambiguidade entre contas").
- Seção "Espaço de chave compartilhado entre tipos de item de uma
  conta": a frase "o mesmo formato de `GSI2PK` (`ID#<id>`)" deixa de
  valer para `Category` — `Category` e `Transaction` passam a ter
  formatos de `GSI2PK` diferentes (`ID#<accountId>#<id>` vs.
  `ID#<id>`), então não colidem mais no espaço de busca por id (a
  colisão de tipos, resolvida pelo atributo `Tipo`, deixa de ser
  necessária para `Category` especificamente, mas o texto deve deixar
  claro que `Transaction` sozinha ainda pode colidir consigo mesma se
  algum dia ganhar ids compartilhados — não é o caso hoje). Avaliar,
  ao redigir, se vale simplificar essa seção ou só anotar a
  divergência de formato entre os dois tipos.

## 6. Erros de negócio → `ErrorType`/HTTP

Nenhum `Error`/`ErrorType` novo. O mapeamento já existente não muda —
`GetByIdAsync`/`UpdateAsync`/`DeleteAsync` continuam devolvendo
`null`/`NotFound` (404) quando a categoria realmente não existe na
conta do chamador; `POST`/`PUT /transactions` continuam devolvendo 400
`validation-error`/"Categoria inválida." pelo mesmo motivo. A única
mudança é que esses erros deixam de ocorrer **erroneamente** quando a
categoria existe de fato na conta do chamador.

## 7. Testes (visão geral — detalhamento fica pro `tasks.md`)

- `DynamoDbCategoryRepositoryTests`: atualizar todos os testes que
  montam item de teste com `GSI2PK` pra usar o formato novo
  (`ID#<accountId>#<categoryId>`); novo teste cobrindo o cenário de
  colisão (dois itens mock com o mesmo `categoryId`, `accountId`s
  diferentes) confirmando que `GetByIdAsync`/`UpdateAsync`/
  `DeleteAsync` só encontram o item da conta esperada, nunca o outro
  — a query já não devolveria o item errado, mas o teste documenta a
  garantia.
- Testes de componente (`CategoryEndpointsTests`,
  `TransactionEndpointsTests` para a validação de `categoryId`): sem
  mudança de contrato esperada, só confirmar ausência de regressão.
- **Sem teste integrado nesta feature** (decisão do usuário): hoje só
  `AuthFlowTests` existe em `GastosApp.IntegrationTests` — categorias,
  transações e membros/convites ainda não têm nenhuma infraestrutura
  de teste integrado (débito técnico já registrado,
  `backend/specs/FEAT-29-testes-integrados/spec.md`). Reproduzir a
  US1 via teste integrado exigiria construir do zero a infra de
  `POST /members`/aceite de convite só para esta correção — fora de
  proporção para um bugfix. A cobertura de unit + componente já
  garante a query correta ao `GSI2` e a ausência de regressão de
  contrato; a lacuna de teste integrado do módulo `categories`/
  `transactions` continua coberta pelo débito técnico já existente,
  sem crescer nem encolher por causa desta feature.
- Regenerar `backend/docs/openapi.json`: sem diff esperado (confirma o
  "Contratos da API" do `spec.md`).

## Pontos a confirmar antes do `/tasks`

Nenhum ponto em aberto — o backfill (único ponto pendente da versão
anterior deste plano) deixou de ser necessário por decisão do usuário,
e a atualização de `backend/docs/data-model.md` já está incorporada
como critério de aceite (seção 5). Pronto para seguir ao `/tasks`.
