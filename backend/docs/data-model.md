# Data model — GastosApp

## Tabela: GastosApp (single-table, PAY_PER_REQUEST)

Chaves e índices provisionados (`backend/infra/terraform/environments/{hom,prod}/dynamodb.tf`):

| Atributo | Tipo | Papel |
|---|---|---|
| `PK` | S | Partition key da tabela base |
| `SK` | S | Sort key da tabela base |
| `GSI1PK` / `GSI1SK` | S | Índice `GSI1`, projeção `ALL` |
| `GSI2PK` | S | Índice `GSI2`, projeção `KEYS_ONLY` (só devolve `PK`/`SK`/`GSI2PK` — qualquer outro atributo exige `GetItem` complementar) |

Toda entidade de negócio (`Category`, `Transaction`, `Membership`) vive
particionada por `ACCOUNT#<accountId>` — a conta (FEAT-19), não mais o
usuário isolado, é o tenant real da tabela desde que a FEAT-19 migrou
`Category`/`Expense` (renomeada `Transaction` na FEAT-22) de
`PK=USER#<userId>`.

## Tipos de item

### `AccountPointer` (resolução da conta ativa de um usuário)

- `PK`: `USER#<userId>`
- `SK`: `ACCOUNT#` (literal fixo — um só item por `userId`)
- Atributos: `AccountId` (string)

Resolve, a partir do `userId` do JWT, qual é a conta **ativa** desse
usuário (`GetItem` direto, sem `Query` — mais barato). É o único item
cuja chave é determinística a partir só do `userId`, o que o torna o
ponto real de serialização de concorrência na criação de conta
(`ConditionExpression: attribute_not_exists(PK)` no `PutItem`/no
primeiro item da transação — ver `DynamoDbAccountRepository.CreateAsync`).
Sobrescrito (`PutItem` incondicional) quando o usuário aceita um
convite no login (`SetActiveAccountAsync`, FEAT-20) — troca deliberada
de conta ativa, sem migrar nem apagar a conta anterior.

### `Account` (metadado da conta)

- `PK`: `ACCOUNT#<accountId>`
- `SK`: `ACCOUNT#` (literal fixo)
- Atributos: `CreatedAt` (string, ISO 8601)

Criada automaticamente (trigger `PostConfirmation` do Cognito, com
fallback no primeiro login) para todo usuário que se cadastra — ver
`backend/specs/FEAT-19-conta-multi-tenant/`.

A criação (`DynamoDbAccountRepository.CreateAsync`) é um único
`TransactWriteItems` que grava, atomicamente: `AccountPointer` +
`Account` + `Membership` (Titular) + as **13 categorias padrão**
(`DefaultCategorySeed`, FEAT-28 — ver seção `Category` abaixo). Ou a
conta nasce completa com as categorias, ou a transação inteira cancela
e é retentada do zero no próximo login/trigger — nunca existe conta
sem categoria.

### `Membership` (vínculo usuário↔conta, com papel de acesso)

- `PK`: `ACCOUNT#<accountId>`
- `SK`: `MEMBER#<membershipId>` — `membershipId` é um `Guid` próprio,
  gerado uma vez na criação e **nunca muda** (inclusive para o
  Titular) — é o `id` público de `/members`, estável mesmo quando um
  convite passa de pendente para aceito
- `GSI1PK`: **dual-purpose**, conforme `Status`:
  - `USER#<userId>` quando `Status=Ativo` (membro resolvido — Titular
    sempre nasce assim; convidado, a partir do primeiro login cujo
    e-mail bate com o convite)
  - `EMAIL#<emailNormalizado>` (`Trim().ToLowerInvariant()`) quando
    `Status=ConvitePendente` (convite ainda não aceito, `UserId`
    desconhecido)
- `GSI1SK`: `ACCOUNT#<accountId>` (constante, nos dois estados)
- Atributos: `Email` (string), `Role` (`Titular` \| `Leitura` \|
  `Lancar` \| `Total`), `Status` (`Ativo` \| `ConvitePendente`),
  `UserId` (presente só quando `Status=Ativo`), `CreatedAt`

Como o `SK` nunca muda, aceitar um convite (`Status=ConvitePendente` →
`Ativo`) é um `UpdateItem` simples de atributos (`Status`, `UserId`,
`GSI1PK`) — nunca precisa do padrão delete+put usado por `Category`/
`Transaction` quando a chave física muda. Ver
`backend/specs/FEAT-20-membros-convites-permissoes/`.

Access patterns cobertos pelo `GSI1` de `Membership`:
| # | Query | Mecanismo |
|---|---|---|
| Papel do chamador na conta ativa | `GSI1PK=USER#<userId> AND GSI1SK=ACCOUNT#<accountId>` (igualdade nos dois) |
| Convites pendentes por e-mail (aceite no login) | `GSI1PK=EMAIL#<emailNormalizado>` (sem condição em `GSI1SK` — pode haver convite em mais de uma conta) |

Listagem de membros de uma conta (`GET /members`) não usa `GSI1` — é
`Query PK=ACCOUNT#<accountId>, begins_with(SK, "MEMBER#")` na tabela
base.

### `Category`

- `PK`: `ACCOUNT#<accountId>`
- `SK`: `CAT#<slug>` — `slug` é o nome normalizado (minúsculo, sem
  acento, sem caractere especial, espaços colapsados em `-`), garante
  unicidade de nome por conta via `ConditionExpression:
  attribute_not_exists(PK)` no `PutItem`
- `GSI2PK`: `ID#<id>` — resolve `GET`/`PUT`/`DELETE /categories/{id}`
  a partir só do `id` (sem depender de conhecer o `slug` atual pra
  montar a `SK`), combinado com um `GetItem` seguinte (`GSI2` só
  projeta `PK`/`SK`/`GSI2PK`)
- Atributos (formato gravado por `CategoryItemMapper`, fonte única do
  shape do item — usada tanto por `DynamoDbCategoryRepository` quanto
  por `DynamoDbAccountRepository` ao semear as categorias padrão):
  `Nome` (string), `Tipo` (string, sempre `"categoria"` —
  **discriminador** contra `Transaction` no `GSI2` compartilhado, ver
  abaixo; **ausente** em itens gravados antes dessa checagem existir,
  tratado como `"categoria"` implícito por compatibilidade),
  `TipoLancamento` (string, `"despesa"` \| `"receita"` — tipo de
  lançamento da categoria em si, não confundir com o atributo `Tipo`
  acima; **ausente** em categorias gravadas antes da FEAT-21, tratado
  como `"despesa"` implícito), `OrcamentoMensalCents` (long, opcional
  — orçamento mensal da categoria, FEAT-21), `CreatedAt`. `Cor` e
  `Icone` **não são mais gravados** desde a FEAT-21 — se um item
  antigo ainda os tiver, são lidos e ignorados.

Editar o `Nome` muda o `slug` e, portanto, o `SK` — não é `UpdateItem`
in-place: se o slug não mudou, é um `PutItem` simples sobrescrevendo o
item; se mudou, é `TransactWriteItems` (`Delete` do item antigo + `Put`
condicional do novo, pra impedir colisão entre duas renomeações
concorrentes). Ver `backend/specs/FEAT-16-crud-categorias/`.

Toda conta nova recebe automaticamente 13 categorias padrão
(`DefaultCategorySeed`, FEAT-28 — ids fixos e iguais em todo ambiente),
sempre com `TipoLancamento="despesa"` e sem `OrcamentoMensalCents`,
semeadas na mesma transação de `DynamoDbAccountRepository.CreateAsync`
que cria `Account`/`Membership` (ver seção `Account` acima).

### `Transaction` (renomeada de `Expense` na FEAT-22)

- `PK`: `ACCOUNT#<accountId>`
- `SK`: `TXN#<YYYY-MM-DD>#<id>` — granularidade diária (permite
  ordenação cronológica e filtro por intervalo de datas via
  `begins_with`/`BETWEEN` na própria chave, sem GSI extra)
- `GSI1PK`: `ACCOUNT#<accountId>#<categoryId>` — usado pelo filtro
  `categoryId` de `GET /transactions` e por
  `ExistsByCategoryAsync` (bloqueia exclusão de categoria com
  transações associadas)
- `GSI1SK`: `<YYYY-MM-DD>#<id>`
- `GSI2PK`: `ID#<id>` — resolve `GET`/`PUT`/`DELETE /transactions/{id}`
  a partir só do `id`, mesmo mecanismo de `Category` (**mesmo índice,
  mesmo formato de chave** — ver "Espaço de chave compartilhado"
  abaixo)
- Atributos: `Description` (string), `AmountInCents` (long, centavos),
  `CategoryId` (string, referência a uma `Category` cadastrada —
  **não é mais um enum fechado desde a FEAT-17**), `Date` (string,
  `YYYY-MM-DD`), `Tipo` (string, `"despesa"` \| `"receita"` — filtro
  opcional de `GET /transactions` via `FilterExpression`),
  `CreatedByUserId` (string, autor do lançamento — **nunca muda numa
  edição**), `CreatedAt`

`GET /transactions` também aceita filtro por faixa de valor
(`MinAmountInCents`/`MaxAmountInCents`, via `FilterExpression` sobre
`AmountInCents`) além de `categoryId`, intervalo de datas e `yearMonth`
(esses três direto na `KeyConditionExpression`, nunca em
`FilterExpression`).

Atualizar uma transação que muda `Date` muda a `SK`/`GSI1SK` — mesmo
padrão de delete+put de `Category` (se a data não muda, é um `PutItem`
simples sobrescrevendo o item).

## Espaço de chave compartilhado entre tipos de item de uma conta

`Category` e `Transaction` compartilham a mesma partição
(`PK=ACCOUNT#<accountId>`) e o mesmo formato de `GSI2PK`
(`ID#<id>`) — o discriminador entre os dois é a `SK` (`CAT#<slug>` vs.
`TXN#<data>#<id>`) na tabela base, mas **no `GSI2` os dois tipos
colidem no mesmo espaço de busca por id**. Os dois repositórios se
defendem disso conferindo o atributo `Tipo` depois do `GetItem`
(`IsTransactionItem`/`IsCategoriaItem`) antes de aceitar o item como o
tipo esperado:

- `DynamoDbTransactionRepository` exige `Tipo <> "categoria"` (aceita
  `"despesa"` **e** `"receita"` sem enumerar os dois — qualquer valor
  diferente de `"categoria"` já basta como transação) — achado
  originalmente como bug real quando só existia `"despesa"`
  (`GET /expenses/{id}` com um `categoryId` por engano respondia 500
  em vez de 404, por tentar ler atributos que só transação tem).
- `DynamoDbCategoryRepository` aceita `Tipo="categoria"` **ou
  ausência do atributo** (categorias gravadas antes dessa checagem
  existir nunca tiveram `Tipo`, e não há migração retroativa) —
  mesmo cenário espelhado (`GET/PUT/DELETE /categories/{id}` com um
  `id` de transação por engano).

### `UserProfile` (dados cadastrais do usuário)

- `PK`: `USER#<userId>`
- `SK`: `PROFILE#` (literal fixo)
- Atributos: `Name` (string), `PhoneNumber` (string), `Cpf` (string),
  `CreatedAt`

### `CpfPointer` (unicidade de CPF entre usuários)

- `PK`: `CPF#<cpf>`
- `SK`: `CPF#` (literal fixo)
- Atributos: `UserId` (string)

Item-sentinela só pra barrar CPF duplicado via
`ConditionExpression: attribute_not_exists(PK)`, mesmo padrão do
`AccountPointer` — gravado atomicamente com `UserProfile` num único
`TransactWriteItems` (`DynamoDbUserProfileRepository.CreateAsync`).

## Regras do modelo

- Valor sempre em centavos (`long`) — sem `float`/`decimal` no banco
- `accountId` é sempre resolvido a partir do `userId` do JWT (claim
  `sub`) via `AccountPointer`/`Membership` — nunca vem do corpo do
  request; `userId` continua sendo a claim usada só para essa
  resolução (e para o `GSI1PK=USER#<userId>` de `Membership`)
- Sem `Scan` — todo acesso é `Query` por `PK`/`SK` ou pelos GSIs

## Backlog (fora do MVP, não implementado)

- **Resumo mensal agregado** (`SUMMARY#<YYYY-MM>` por conta, com
  `totalDespesas`/`totalReceitas`/`saldoMes`/`porCategoria`): segue só
  como ideia de modelagem futura — a tabela hoje não tem DynamoDB
  Streams habilitado nem Lambda trigger algum, então nenhuma agregação
  é calculada ou persistida automaticamente (FEAT-23 do roadmap)
- `createdByLabel` em `Transaction` (rótulo legível de quem lançou,
  pro papel `Lancar` poder editar/excluir só o que lançou) —
  `CreatedByUserId` já existe desde a FEAT-22, mas só guarda o `userId`
  bruto, sem exibir um nome amigável
- Tags em transações: requer GSI adicional ou filtro em memória
- Seletor/troca manual entre múltiplas contas de um mesmo usuário —
  hoje a troca de conta ativa só acontece como efeito colateral de
  aceitar um convite no login (FEAT-20); navegar entre contas já
  pertencentes sem novo convite fica pra uma feature futura
- Comprovante de despesa (upload S3): sem bucket, sem `ReceiptS3Key`
  no modelo

## Link para representação visual do MER
  - [MER do modelo](https://claude.ai/code/artifact/d83300e5-b886-40f6-932d-d109bf7dab02)