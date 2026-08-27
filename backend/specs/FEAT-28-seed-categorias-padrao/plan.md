# Plan — FEAT-28: Seed de categorias padrão

Decisão de arquitetura já fechada: as 13 categorias padrão são criadas
**na mesma `TransactWriteItems`** que hoje cria `AccountPointer` +
`Account` + `Membership` (`DynamoDbAccountRepository.CreateAsync`, ver
`backend/specs/FEAT-19-conta-multi-tenant/plan.md`), em vez de uma
escrita separada depois. Isso torna a criação da conta inteira — conta,
titular e categorias padrão — atômica e tudo-ou-nada, reaproveitando a
mesma trava de concorrência (`AccountPointer` como item 0) e o mesmo
mecanismo de retry (próximo login) que a FEAT-19 já garante para
`Account`/`Membership`. Ver seção 3 (decisão técnica 1) e "Pontos a
confirmar" para a implicação sobre um trecho da spec.

## 1. Camadas afetadas

### Domain — `GastosApp.Domain`
- **Novo** `Categories/DefaultCategorySeed.cs`: catálogo estático das 13
  categorias padrão (dado de negócio fixo, não configuração de
  ambiente):
  ```csharp
  public static class DefaultCategorySeed
  {
      public const string Tipo = "despesa";

      public static readonly IReadOnlyList<(string Id, string Nome)> Items =
      [
          ("862d8a7c-c3ef-412b-b4d3-88c1b4d317d9", "Moradia"),
          ("369a308a-f96e-4ba9-ac43-3c9e8696141f", "Alimentação"),
          ("a95ac718-1608-4c64-96da-4eefdc33e3e9", "Transporte"),
          ("2644f155-1215-4936-8f9a-606e0ba58315", "Saúde"),
          ("ceb83cec-9ca0-4ec0-a58f-adac83574faf", "Educação"),
          ("f2d554c0-16d6-4fee-bef1-3364d9bb8ec3", "Filhos e Dependentes"),
          ("24ef9ebc-58b3-4197-b9ac-1f203b79f07b", "Lazer e Entretenimento"),
          ("0af4581d-37bf-4636-9805-ce2302403330", "Vestuário e Cuidados Pessoais"),
          ("319ddec7-f867-427f-997a-66cd4ed9d8e1", "Pets"),
          ("89bfe4ec-8747-44d3-92ba-4266960dd00f", "Dívidas e Financiamentos"),
          ("961a8b3c-d210-4bd5-a470-1ef15c3549c3", "Impostos, Taxas e Seguros"),
          ("d8865733-b002-4b11-b160-94237b2391c1", "Doações e Presentes"),
          ("e9b32f2d-3eb7-4318-a268-438bb2d72f44", "Outros"),
      ];
  }
  ```
  `Tipo` fixo `"despesa"` e `OrcamentoMensalCents` sempre `null` para
  todo item — não guardados no catálogo por serem sempre os mesmos.

### Application — `GastosApp.Application`
- **Nenhuma mudança de assinatura.** `EnsureAccountCommand`/
  `EnsureAccountCommandHandler` e `IAccountRepository.CreateAsync`
  continuam exatamente como hoje (`(userId, email, ct) →
  CreateAccountResult`). O seed acontece dentro da implementação de
  `CreateAsync` (Infrastructure) — mesmo nível em que a criação do
  `Membership` (Titular) já é uma decisão de negócio embutida ali,
  sem o Application precisar orquestrar categoria alguma (ver decisão
  técnica 2).
- Doc-comment de `EnsureAccountCommand` atualizado para mencionar que a
  criação também semeia as categorias padrão (só comentário, sem mudar
  código executável).

### Infrastructure — `GastosApp.Infrastructure`
- **Novo** `Categories/CategoryItemMapper.cs`: extrai `BuildSk(nome)` e
  `BuildItem(Category, sk)` de dentro de `DynamoDbCategoryRepository`
  (hoje `private static`) para uma classe `internal static`
  compartilhada — usada tanto pelo `DynamoDbCategoryRepository` (refactor
  mecânico, sem mudança de comportamento) quanto pelo novo uso em
  `DynamoDbAccountRepository.CreateAsync`. Motivo: dois repositórios
  passam a escrever o mesmo formato de item `Category`, e não deve haver
  duas fontes de verdade pra esse shape.
- `Accounts/DynamoDbAccountRepository.CreateAsync`: a lista
  `TransactItems` da `TransactWriteItemsRequest` ganha 13 `TransactWriteItem`
  novos (um `Put` por entrada de `DefaultCategorySeed.Items`), cada um
  construído a partir de
  `Category.Restore(id, accountId, nome, DefaultCategorySeed.Tipo, null, createdAt)`
  (mesmo `createdAt` já gerado pro `Account`/`Membership`) +
  `CategoryItemMapper.BuildItem`, com `ConditionExpression:
  attribute_not_exists(PK)` — mesma defesa já usada nos outros itens da
  transação. Itens finais na transação: 3 (`AccountPointer`, `Account`,
  `Membership`, já existentes) + 13 (categorias) = 16, bem dentro do
  limite de 100 itens do `TransactWriteItems`.
- Tratamento de `TransactionCanceledException` **não muda**: continua
  olhando só `CancellationReasons[0]` (o `AccountPointer`, item 0, que
  segue sendo o único com chave determinística a partir só do `userId`)
  para decidir se foi corrida. Se a condição de alguma categoria falhar
  (cenário só possível com dado corrompido/manual — ver decisão técnica
  1), a transação inteira cancela, `reasons[0].Code` não é
  `ConditionalCheckFailed`, e a exceção sobe normalmente — cai no mesmo
  tratamento genérico que hoje já existe (o chamador loga e tenta de
  novo no próximo login, sem duplicar nada).

### Api — `GastosApp.Api`
Nenhuma mudança — nenhum endpoint novo ou alterado.

### `GastosApp.CognitoTriggers`
Nenhuma mudança de código — já despacha `EnsureAccountCommand`, que por
baixo passa a semear as categorias como efeito transparente da mesma
chamada.

## 2. Modelo de dados (DynamoDB, tabela `GastosApp` já existente)

Nenhuma tabela nem índice novo. 13 itens `Category` a mais por conta
nova, no mesmo formato já documentado em `backend/docs/data-model.md`
(seção `Category`) — só o `Id`/`GSI2PK` deixam de vir de
`Guid.NewGuid()` e passam a ser um dos 13 valores fixos abaixo.

| Nome | Id fixo (`GSI2PK = ID#<id>`) | `SK` (`CAT#<slug>`) |
|---|---|---|
| Moradia | `862d8a7c-c3ef-412b-b4d3-88c1b4d317d9` | `CAT#moradia` |
| Alimentação | `369a308a-f96e-4ba9-ac43-3c9e8696141f` | `CAT#alimentacao` |
| Transporte | `a95ac718-1608-4c64-96da-4eefdc33e3e9` | `CAT#transporte` |
| Saúde | `2644f155-1215-4936-8f9a-606e0ba58315` | `CAT#saude` |
| Educação | `ceb83cec-9ca0-4ec0-a58f-adac83574faf` | `CAT#educacao` |
| Filhos e Dependentes | `f2d554c0-16d6-4fee-bef1-3364d9bb8ec3` | `CAT#filhos-e-dependentes` |
| Lazer e Entretenimento | `24ef9ebc-58b3-4197-b9ac-1f203b79f07b` | `CAT#lazer-e-entretenimento` |
| Vestuário e Cuidados Pessoais | `0af4581d-37bf-4636-9805-ce2302403330` | `CAT#vestuario-e-cuidados-pessoais` |
| Pets | `319ddec7-f867-427f-997a-66cd4ed9d8e1` | `CAT#pets` |
| Dívidas e Financiamentos | `89bfe4ec-8747-44d3-92ba-4266960dd00f` | `CAT#dividas-e-financiamentos` |
| Impostos, Taxas e Seguros | `961a8b3c-d210-4bd5-a470-1ef15c3549c3` | `CAT#impostos-taxas-e-seguros` |
| Doações e Presentes | `d8865733-b002-4b11-b160-94237b2391c1` | `CAT#doacoes-e-presentes` |
| Outros | `e9b32f2d-3eb7-4318-a268-438bb2d72f44` | `CAT#outros` |

Todas com `Tipo="categoria"` (discriminador do `GSI2`, igual a qualquer
`Category`), `TipoLancamento="despesa"`, sem `OrcamentoMensalCents`
(omitido, igual a qualquer categoria sem orçamento), `CreatedAt` igual
ao `Account`/`Membership` criados na mesma transação.

### Criação (`CreateAsync`, dentro de `DynamoDbAccountRepository`)
`TransactWriteItems` com 16 `Put` (3 já existentes + 13 novos):
1. `AccountPointer` — `ConditionExpression: attribute_not_exists(PK)`
   (único item que realmente serializa a concorrência, inalterado).
2. `Account`.
3. `Membership` (Titular).
4–16. Uma `Category` por entrada de `DefaultCategorySeed.Items`, mesma
   `ConditionExpression` por defesa (nunca deve barrar em uso normal —
   a conta é sempre nova nesse ponto, não pode ter categoria prévia com
   slug colidente).

## 3. Decisões técnicas

**1. Atomicidade via `TransactWriteItems` único (Account + Membership +
13 categorias), em vez de uma escrita separada de categorias depois da
`CreateAsync`.** Alternativa considerada: criar a conta normalmente e,
só quando `AlreadyExisted == false`, disparar uma segunda escrita
(idempotente por `ConditionExpression`) para as 13 categorias. Rejeitada
porque reabre exatamente o problema de resiliência que a FEAT-19 já
resolveu para `Account`/`Membership`: se essa segunda escrita falhasse
parcialmente, nada re-tentaria especificamente as categorias no próximo
login (o próximo `EnsureAccountCommand` já encontraria a conta via
`FindAccountIdByUserIdAsync` e nunca chamaria `CreateAsync` de novo).
Colocar tudo numa única transação elimina esse buraco: ou a conta nasce
completa (com as 13 categorias), ou a criação inteira cancela e é
re-tentada do zero no próximo login/trigger — mesma garantia que
`Account`/`Membership` já tinham, agora estendida às categorias, sem
precisar de nenhuma flag nova (`CategoriesSeeded` ou similar) nem de
checagem extra em todo login. Trade-off aceito e necessário: com
`TransactWriteItems` a operação é tudo-ou-nada — não existe mais
"cria a conta mas ignora só uma categoria colidente" (ver ponto a
confirmar abaixo, sobre ajustar a spec).

**2. Seed embutido em `DynamoDbAccountRepository.CreateAsync`
(Infrastructure), não orquestrado por `EnsureAccountCommandHandler`
(Application).** Mesmo nível de decisão já usado hoje pro `Membership`
(Titular) — `EnsureAccountCommandHandler` não escolhe papel nem
conteúdo do `Membership`, só chama `CreateAsync`; o "o que uma conta
nova contém" é uma decisão de forma de escrita, não de fluxo de caso de
uso. Manter consistência evita espalhar a mesma responsabilidade em
duas camadas.

**3. `CategoryItemMapper` extraído como compartilhado entre os dois
repositórios.** Sem essa extração, `DynamoDbAccountRepository`
precisaria duplicar o formato exato do item `Category` (`PK`, `SK`,
`GSI2PK`, `Tipo`, `TipoLancamento`, `OrcamentoMensalCents` condicional)
— reintroduzindo o mesmo risco que o comentário de
`DynamoDbCategoryRepository` já descreve para o `GSI2` compartilhado
com `Expense` (duas fontes de verdade divergindo). Refactor mecânico,
sem mudança de comportamento nos testes já existentes de
`DynamoDbCategoryRepository`.

**4. Catálogo de categorias padrão vive em `Domain`
(`DefaultCategorySeed`), não em `Infrastructure`/config/appsettings.**
São dados de negócio fixos — como um enum —, não configuração de
ambiente: garante que os 13 nomes/ids são idênticos em qualquer
ambiente (dev/hom/prod) sem exigir nenhuma variável de ambiente ou
parâmetro de Parameter Store novo, e sem risco de divergência entre
ambientes por erro de configuração manual.

**5. `Category.Id` continua `string` livre, sem validação de formato.**
Os 13 GUIDs fixos são só valores literais usados nesse ponto de criação
específico — não introduzem nenhuma restrição nova sobre o `Id` de
categorias criadas manualmente (continuam `Guid.NewGuid().ToString()`
via `Category.Create`).

## 4. Recursos AWS usados ou afetados

**Nenhum recurso novo.** Reaproveita a tabela `GastosApp` (mesmo
`TransactWriteItems`, só com mais itens) e a mesma Lambda
`account-trigger` (FEAT-19) + o fallback já existente no login (Api
Lambda). Nenhuma mudança de IAM: as permissões já concedidas
(`dynamodb:PutItem`/`GetItem`/`TransactWriteItems` na tabela) já cobrem
os itens extras — `TransactWriteItems` é uma única chamada de API,
independente de quantos itens carrega.

## 5. Erros de negócio → `ErrorType`/HTTP

Nenhum `Error`/`ErrorType` novo — esta feature não introduz nem altera
endpoint algum, e a criação de categorias padrão nunca é reportada como
erro pro chamador (mesma postura de "melhor esforço" já aplicada a toda
criação de conta, FEAT-19).

## 6. Testes (visão geral — detalhamento fica pro `tasks.md`)

- `DynamoDbAccountRepositoryTests.CreateAsync`: expandir os testes
  existentes para conferir que a `TransactWriteItemsRequest` capturada
  tem 16 itens, incluindo as 13 categorias com `Id`/`Nome`/`SK`/`Tipo`/
  `TipoLancamento` corretos (mock de `IAmazonDynamoDB`, mesmo padrão já
  usado nesse arquivo).
- Novo teste garantindo que uma segunda chamada de `CreateAsync` (ou o
  `TransactionCanceledException` simulado com `reasons[0]` colidindo)
  não gera uma segunda `TransactWriteItemsRequest` — a resolução do
  vencedor via `FindAccountIdByUserIdAsync` continua sem tocar em
  categoria alguma.
- `DynamoDbCategoryRepositoryTests`: sem mudança de comportamento
  esperada — só confirmar que o refactor pra `CategoryItemMapper`
  mantém todos os testes existentes passando (verificação do refactor,
  não teste novo).
- `AccountTriggerHandlerTests`/ComponentTests de `EnsureAccountCommand`:
  sem mudança de assinatura pública — não devem precisar de ajuste, só
  rodar pra confirmar ausência de regressão (mock de `IAccountRepository`,
  não desce até `DynamoDbAccountRepository` real).
- `CategoryEndpointsTests`: conferir se algum teste hoje assume `GET
  /categories` vazio logo após "criar" uma conta de teste via
  `AccountRepositoryMock`; ajustar apenas se necessário (a maioria já
  usa `CategoryRepositoryMock` isolado, sem depender do seed real).
- Regenerar `backend/docs/openapi.json` mesmo sem mudança de contrato
  esperada (exigência da constitution) — só pra confirmar ausência de
  diff.

## Pontos confirmados pelo usuário

1. Ajuste em `spec.md` (o requisito de colisão de slug virou "criação
   atômica tudo-ou-nada, re-tentada por completo no próximo login" —
   ver `spec.md`, seção "Requisitos de negócio" e US9) — **confirmado e
   aplicado**.
2. Extração de `CategoryItemMapper` compartilhado entre
   `DynamoDbCategoryRepository` e `DynamoDbAccountRepository` —
   **confirmado**.
3. `DefaultCategorySeed` em `GastosApp.Domain.Categories` — **confirmado**.
