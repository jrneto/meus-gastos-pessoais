# FEAT-22: Transações — generalizar Despesa para Receita/Despesa

## Objetivo

Generalizar a entidade `Expense` (despesa) para `Transação`, que passa a
representar tanto despesas quanto receitas. `/expenses` deixa de existir
e dá lugar a `/transactions`, com um novo campo `tipo`
(`despesa`|`receita`) e exposição de quem lançou cada transação
(`createdByUserId`/`createdByLabel`), viabilizando o rótulo "Lançado
por: Você" do design system e uma futura permissão mais granular para o
papel `Lancar`.

## Contexto

Hoje `Expense` (`backend/specs/FEAT-04-registro-despesa/`,
`FEAT-06-consulta-despesas/`, `FEAT-07-exclusao-despesa/`,
`FEAT-08-atualizacao-despesa/`, `FEAT-17-despesas-categoria-dinamica/`)
só representa despesas — o atributo interno `Tipo` do item sempre grava
`"despesa"` (ver `backend/docs/data-model.md`), sem `"receita"`
implementado. O design system
(`frontend/design-system/screenshots/09-transacoes.png`,
`08-nova-receita.png`, `17-detalhe-transacao.png`) já assume uma tela
única "Transações" com lançamentos de receita e despesa misturados numa
mesma lista, filtro "Todas" + chips por categoria (não um toggle
despesa/receita), botões separados "+ Nova receita"/"+ Nova despesa", e
um modal de detalhe que mostra "Lançado por: Você".

A FEAT-21 já preparou o terreno adicionando `tipo` (`despesa`|`receita`)
a `Category` — esta feature usa esse `tipo` da categoria vinculada para
determinar (e validar) o tipo do lançamento. A FEAT-20 já define o papel
`Lancar` como "pode visualizar e lançar novas despesas", mas hoje ele
não pode editar/excluir nenhum lançamento, nem o que ele mesmo criou —
`backend/docs/data-model.md` (seção "Backlog") já registrava
`createdByUserId`/`createdByLabel` como pré-requisito para o papel
`Lancar` poder editar/excluir só o que lançou, adiado para esta feature.

Segue `backend/docs/roadmap.md` (item "FEAT-22 — Transações: generalizar
Despesa para Receita/Despesa").

**Decisões de escopo fechadas nesta spec (revisão com o usuário antes de
detalhar o contrato):**

1. **Rota única `/transactions`, com `tipo` como atributo e filtro de
   query** — não duas rotas espelhadas (`/expenses` + `/incomes`).
   Confirmado com o usuário: a tela "Transações" do design system exibe
   uma lista mista (despesas e receitas juntas, ordenadas
   cronologicamente), com filtro "Todas" + chips de categoria — não um
   toggle despesa/receita — o que casa com um recurso único filtrável
   por `tipo`, no mesmo padrão já usado por `GET /categories?tipo=`
   (FEAT-21).
2. **`/expenses` deixa de existir, sem compatibilidade retroativa.**
   Nenhum redirecionamento, nenhum endpoint duplicado mantido em
   paralelo — mesma decisão de "sem migração" já aplicada em toda a
   leva do roadmap atual (tabela pode ser recriada do zero).
3. **Campo de data renomeado de `expenseDate` para `date`.** Fazia
   sentido só quando só existia despesa; agora que o mesmo campo serve
   receita e despesa, o nome genérico evita um `expenseDate` numa
   receita.
4. **`tipo` é obrigatório em `POST`/`PUT /transactions`** (`"despesa"`
   ou `"receita"`) **e precisa bater com o `tipo` da categoria
   referenciada por `categoryId`** — uma transação de tipo `despesa` só
   pode referenciar categoria de tipo `despesa`, e vice-versa.
   Divergência retorna 400. Fecha o item que a FEAT-21 deixou
   explicitamente fora do seu escopo ("validar/usar o tipo da categoria
   em transações").
5. **`createdByUserId` é sempre o `userId` (JWT `sub`) de quem criou a
   transação (`POST`), nunca vem do body, e nunca muda** — mesmo que
   outro membro edite a transação depois (`PUT`), o "autor" original é
   preservado. `createdByLabel` é derivado, não persistido como texto
   livre: `"Você"` quando `createdByUserId` é o próprio chamador,
   caso contrário o e-mail do membro que criou (resolvido via
   `Membership` da conta ativa).
6. **Papel `Lancar` passa a poder editar/excluir (`PUT`/`DELETE`) as
   transações que ele mesmo criou** (`createdByUserId` igual ao
   `userId` do chamador) — continua bloqueado (403) para transações
   criadas por outro membro. Papéis `Total`/`Titular` continuam podendo
   editar/excluir qualquer transação da conta, sem checagem de autoria
   (mesmo comportamento já existente). `Leitura` continua somente
   leitura. Isso muda a matriz de autorização de `/expenses` publicada
   na FEAT-20 (lá, `Lancar` não editava/excluía nenhuma despesa).
7. **Sem migração de dados.** Transações gravadas hoje como `Expense`
   (item `Tipo="despesa"` implícito) não são migradas automaticamente
   para o novo formato — mesma decisão já aplicada pelo roadmap atual
   (recriação da tabela do zero antes de ir para homologação/produção).
8. **Filtro `categoryId` de `GET /transactions` não valida tipo** — um
   `categoryId` de categoria `receita` filtrando implicitamente só
   retorna transações `receita` (porque só elas podem referenciá-la),
   sem necessidade de regra adicional.

## Requisitos de negócio

- `description`, `amountInCents`, `categoryId`, `date`, `tipo`:
  obrigatórios em `POST`/`PUT /transactions` — mesmas regras de
  validação já existentes para os quatro primeiros campos
  (`backend/specs/FEAT-04-registro-despesa/`,
  `FEAT-17-despesas-categoria-dinamica/`), `tipo` aceita somente
  `"despesa"` ou `"receita"`
- `categoryId` deve referenciar uma categoria que exista, pertença à
  conta ativa do chamador, **e tenha o mesmo `tipo` da transação** —
  qualquer divergência (categoria inexistente, de outra conta, ou de
  tipo diferente) retorna 400, sem diferenciar os três casos (não vaza
  informação sobre categorias de outras contas)
- `createdByUserId` é sempre o `userId` do JWT de quem chama
  `POST /transactions`, nunca informado no body, e nunca é alterado por
  uma edição (`PUT`) posterior feita por outro membro
- `createdByLabel` é sempre derivado do `createdByUserId` no momento da
  resposta (`"Você"` para o próprio chamador, e-mail do criador caso
  contrário) — não é um campo aceito em `POST`/`PUT`
- `GET /transactions` aceita filtro opcional `tipo` (`despesa` \|
  `receita`), além dos filtros já existentes de `GET /expenses`
  (`categoryId`, `yearMonth`, `dateFrom`/`dateTo`,
  `minAmountInCents`/`maxAmountInCents`, `cursor`, `limit`), todos
  combináveis; `tipo` fora de `despesa`/`receita` retorna 400
- Toda consulta/escrita é escopada à conta ativa do chamador
  (`accountId` resolvido do JWT, nunca do body) — nunca expõe
  transações de outra conta
- Autorização por papel (conta ativa do chamador):
  - `GET /transactions`, `GET /transactions/{id}`: qualquer papel
    (`Leitura`, `Lancar`, `Total`, `Titular`)
  - `POST /transactions`: `Lancar`, `Total`, `Titular` (403 pra
    `Leitura`)
  - `PUT`/`DELETE /transactions/{id}`: `Total`/`Titular` sempre;
    `Lancar` somente quando `createdByUserId` da transação é o próprio
    chamador (403 caso contrário); `Leitura` sempre 403
- Transação inexistente ou pertencente a outra conta: `GET`, `PUT` e
  `DELETE` retornam 404, sem diferenciar os dois casos
- `id`, `createdAt` e `createdByUserId` nunca mudam depois de criados,
  mesmo após `PUT`
- `DELETE /categories/{id}` continua bloqueando a exclusão com 422
  quando existir transação vinculada à categoria (mesmo comportamento
  já coberto pela FEAT-16/FEAT-17, só a nomenclatura interna muda de
  "despesa" para "transação")

## User Stories

**US1 — Registrar despesa**
- Given um usuário autenticado com papel `Lancar`, `Total` ou `Titular`
  e uma categoria própria de tipo `despesa`
- When ele envia `POST /transactions` com `tipo="despesa"`,
  `categoryId` dessa categoria e os demais campos válidos
- Then a transação é criada com `tipo="despesa"`, `createdByUserId`
  igual ao seu `userId` e `createdByLabel="Você"`, e a API retorna 201

**US2 — Registrar receita**
- Given um usuário autenticado com papel `Lancar`, `Total` ou `Titular`
  e uma categoria própria de tipo `receita`
- When ele envia `POST /transactions` com `tipo="receita"`,
  `categoryId` dessa categoria e os demais campos válidos
- Then a transação é criada com `tipo="receita"`, e a API retorna 201

**US3 — Rejeitar tipo ausente ou inválido**
- Given um usuário autenticado com papel de escrita
- When ele envia `POST`/`PUT /transactions` com `tipo` ausente, vazio ou
  fora de `despesa`/`receita`
- Then a API retorna 400 e nenhuma transação é criada/alterada

**US4 — Rejeitar transação cujo tipo diverge da categoria**
- Given um usuário autenticado com papel de escrita e uma categoria
  própria de tipo `despesa`
- When ele envia `POST /transactions` com `tipo="receita"` e
  `categoryId` dessa categoria de tipo `despesa`
- Then a API retorna 400 e nenhuma transação é criada

**US5 — Impedir uso de categoria inexistente ou de outra conta**
- Given um usuário autenticado com papel de escrita
- When ele envia `POST /transactions` com `categoryId` inexistente ou
  pertencente a outra conta
- Then a API retorna 400 (mesmo tratamento dos dois casos), e nenhuma
  transação é criada

**US6 — Consultar todas as transações sem filtro**
- Given um usuário autenticado com despesas e receitas registradas na
  conta ativa
- When ele consulta `GET /transactions` sem nenhum filtro
- Then a API retorna todas as transações da conta (dos dois tipos),
  paginadas, ordenadas da mais recente para a mais antiga

**US7 — Filtrar transações por tipo**
- Given um usuário autenticado com transações de ambos os tipos
- When ele consulta `GET /transactions?tipo=receita`
- Then a API retorna somente as transações com `tipo="receita"`

**US8 — Rejeitar filtro de tipo inválido**
- Given um usuário autenticado
- When ele consulta `GET /transactions?tipo=invalido`
- Then a API retorna 400 e nenhuma transação é retornada

**US9 — Combinar filtro de tipo com os demais filtros**
- Given um usuário autenticado com transações variadas
- When ele consulta `GET /transactions` combinando `tipo`, `categoryId`,
  `yearMonth`, intervalo de datas e/ou faixa de valor
- Then a API retorna apenas as transações que satisfazem todos os
  filtros informados simultaneamente (mesmo comportamento combinável já
  coberto pela FEAT-06 para os filtros que já existiam)

**US10 — Consultar detalhe de transação lançada por outro membro**
- Given um usuário autenticado numa conta com outro membro, e uma
  transação criada por esse outro membro
- When ele consulta `GET /transactions/{id}` dessa transação
- Then a API retorna 200 com `createdByUserId` do outro membro e
  `createdByLabel` igual ao e-mail dele (não "Você")

**US11 — Editar transação própria com papel Total/Titular**
- Given um usuário autenticado com papel `Total` ou `Titular`, dono de
  uma transação da conta
- When ele envia `PUT /transactions/{id}` com dados válidos
- Then a transação é atualizada e a API retorna 200, preservando `id`,
  `createdAt` e `createdByUserId` originais

**US12 — Editar transação de outro membro com papel Total/Titular**
- Given um usuário autenticado com papel `Total` ou `Titular`, e uma
  transação criada por outro membro da mesma conta
- When ele envia `PUT /transactions/{id}` com dados válidos
- Then a transação é atualizada normalmente e a API retorna 200 (papel
  `Total`/`Titular` não é limitado por autoria)

**US13 — Papel Lancar edita transação que ele mesmo criou**
- Given um usuário autenticado com papel `Lancar`, dono
  (`createdByUserId`) de uma transação
- When ele envia `PUT /transactions/{id}` dessa transação com dados
  válidos
- Then a transação é atualizada e a API retorna 200

**US14 — Papel Lancar não edita transação de outro membro**
- Given um usuário autenticado com papel `Lancar`, e uma transação
  criada por outro membro da mesma conta
- When ele envia `PUT /transactions/{id}` dessa transação
- Then a API retorna 403 e a transação não é alterada

**US15 — Papel Lancar exclui transação que ele mesmo criou**
- Given um usuário autenticado com papel `Lancar`, dono de uma
  transação
- When ele envia `DELETE /transactions/{id}` dessa transação
- Then a API retorna 204 e a transação deixa de existir

**US16 — Papel Lancar não exclui transação de outro membro**
- Given um usuário autenticado com papel `Lancar`, e uma transação
  criada por outro membro da mesma conta
- When ele envia `DELETE /transactions/{id}` dessa transação
- Then a API retorna 403 e a transação permanece intacta

**US17 — Papel Leitura não cria, edita nem exclui transação**
- Given um usuário autenticado com papel `Leitura`
- When ele tenta `POST /transactions`, `PUT /transactions/{id}` ou
  `DELETE /transactions/{id}`
- Then a API retorna 403 em todos os casos, e nenhuma transação é
  criada/alterada/excluída

**US18 — Isolamento entre contas**
- Given dois usuários autenticados em contas diferentes, cada um com
  suas próprias transações
- When um deles consulta, edita ou exclui uma transação pelo `id`
- Then a API só afeta/retorna transações da sua própria conta ativa;
  tentar acessar `id` de outra conta retorna 404 em `GET`/`PUT`/`DELETE`

**US19 — Impedir qualquer operação sem autenticação**
- Given uma requisição sem token JWT válido
- When o cliente tenta `GET`/`POST`/`PUT`/`DELETE /transactions`
- Then a API retorna 401 e nenhum dado é retornado/alterado

**US20 — Excluir categoria com transações vinculadas continua bloqueado**
- Given um usuário autenticado com uma categoria vinculada a pelo menos
  uma transação
- When ele tenta `DELETE /categories/{id}` dessa categoria
- Then a API retorna 422 (mesmo comportamento já coberto pela FEAT-16/
  FEAT-17), e a categoria permanece intacta

## Contratos da API

### POST /transactions

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "tipo": "despesa",
  "date": "2025-06-15"
}
```

Response 201 (Location: /transactions/{id}):
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "tipo": "despesa",
  "date": "2025-06-15",
  "createdByUserId": "a1b2c3d4-...",
  "createdByLabel": "Você",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 400 (validation-error): campo obrigatório ausente/inválido
(inclui `tipo` fora de `despesa`/`receita`, `categoryId` inexistente/de
outra conta/de tipo diferente da transação).
Response 401 (unauthorized).
Response 403 (insufficient-permission): papel `Leitura`.

### GET /transactions

Query params (todos opcionais, combináveis):

| Param | Tipo | Formato |
|---|---|---|
| `tipo` | string | `despesa` \| `receita` |
| `categoryId` | string | id de uma categoria (não precisa existir — sem resultado, retorna lista vazia) |
| `yearMonth` | string | `YYYY-MM` |
| `dateFrom` | string | `YYYY-MM-DD` |
| `dateTo` | string | `YYYY-MM-DD` |
| `minAmountInCents` | long | > 0 |
| `maxAmountInCents` | long | > 0 |
| `cursor` | string | token opaco retornado por uma consulta anterior |
| `limit` | int | tamanho de página desejado (sujeito a padrão/máximo da API) |

Response 200:
```json
{
  "items": [
    {
      "id": "...",
      "description": "Almoço no restaurante",
      "amountInCents": 4590,
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "tipo": "despesa",
      "date": "2025-06-15",
      "createdByUserId": "a1b2c3d4-...",
      "createdByLabel": "Você",
      "createdAt": "2025-06-15T12:34:56Z"
    }
  ],
  "nextCursor": "opaque-token-or-null"
}
```

Response 400 (validation-error): algum filtro inválido (inclui `tipo`
fora de `despesa`/`receita`).
Response 401 (unauthorized).

### GET /transactions/{id}

Response 200: mesmo formato de item acima.
Response 401 (unauthorized).
Response 404 (not-found): transação inexistente ou de outra conta.

### PUT /transactions/{id}

Request (corpo completo, mesmo padrão já usado hoje):
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 5290,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "tipo": "despesa",
  "date": "2025-06-16"
}
```

Response 200: mesmo formato de item do `POST`, com `createdByUserId`/
`createdByLabel` preservando o autor original (não muda pra quem
editou).
Response 400 (validation-error): campo obrigatório ausente/inválido.
Response 401 (unauthorized).
Response 403 (insufficient-permission): papel `Leitura`, ou papel
`Lancar` numa transação que não é sua (`createdByUserId` diferente do
chamador).
Response 404 (not-found): transação inexistente ou de outra conta.

### DELETE /transactions/{id}

Sem request body.

Response 204 (sem corpo).
Response 401 (unauthorized).
Response 403 (insufficient-permission): papel `Leitura`, ou papel
`Lancar` numa transação que não é sua.
Response 404 (not-found): transação inexistente ou de outra conta.

### Autorização por papel (atualiza a matriz de `/expenses` publicada na FEAT-20)

| Endpoint | Leitura | Lancar | Total | Titular |
|---|:-:|:-:|:-:|:-:|
| `GET /transactions`, `GET /transactions/{id}` | ✅ | ✅ | ✅ | ✅ |
| `POST /transactions` | 403 | ✅ | ✅ | ✅ |
| `PUT`/`DELETE /transactions/{id}` (transação própria, `createdByUserId` = chamador) | 403 | ✅ | ✅ | ✅ |
| `PUT`/`DELETE /transactions/{id}` (transação de outro membro) | 403 | 403 | ✅ | ✅ |

### Erros comuns a todas as rotas

Formato padrão de erro do projeto (`ResultHttpExtensions.BuildProblem`):
`title` fixo e genérico por tipo de erro (RFC 9457), mensagem
específica sempre em `detail`. Fonte de verdade exata:
`backend/docs/openapi.json`.

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Tipo deve ser \"despesa\" ou \"receita\"."
}
```

Response 401 (unauthorized):
```json
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Não autorizado",
  "status": 401
}
```

Response 403 (insufficient-permission):
```json
{
  "type": "https://gastosapp.dev/errors/insufficient-permission",
  "title": "Acesso negado",
  "status": 403,
  "detail": "Seu nível de acesso não permite esta ação."
}
```

Response 404 (not-found):
```json
{
  "type": "https://gastosapp.dev/errors/not-found",
  "title": "Recurso não encontrado",
  "status": 404,
  "detail": "Transação não encontrada."
}
```

Response 422 (category-in-use, em `DELETE /categories/{id}`):
```json
{
  "type": "https://gastosapp.dev/errors/category-in-use",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "A categoria não pode ser excluída enquanto houver transações associadas a ela."
}
```

## Critérios de aceite

- [x] `POST /transactions` com `tipo="despesa"` e categoria de tipo
      `despesa` cria a transação e retorna 201 com `createdByUserId`/
      `createdByLabel="Você"`
- [x] `POST /transactions` com `tipo="receita"` e categoria de tipo
      `receita` cria a transação e retorna 201
- [x] `POST`/`PUT /transactions` com `tipo` ausente ou fora de
      `despesa`/`receita` retorna 400
- [x] `POST`/`PUT /transactions` com `tipo` divergente do `tipo` da
      categoria referenciada retorna 400
- [x] `POST /transactions` com `categoryId` inexistente ou de outra
      conta retorna 400
- [x] `GET /transactions` sem filtro retorna todas as transações da
      conta (despesas e receitas), paginadas, mais recente primeiro
- [x] `GET /transactions?tipo=despesa`/`?tipo=receita` retorna somente
      transações daquele tipo
- [x] `GET /transactions?tipo=` com valor fora de `despesa`/`receita`
      retorna 400
- [x] Filtros combinados (`tipo` + `categoryId` + `yearMonth` + datas +
      valor) retornam apenas transações que satisfazem todos
      simultaneamente
- [x] `GET /transactions/{id}` retorna `createdByLabel="Você"` quando o
      chamador é o autor, e o e-mail do autor quando é outro membro
- [x] `PUT`/`DELETE /transactions/{id}` por papel `Total`/`Titular`
      funciona em qualquer transação da conta, inclusive de outro
      membro
- [x] `PUT`/`DELETE /transactions/{id}` por papel `Lancar` funciona
      quando `createdByUserId` é o próprio chamador
- [x] `PUT`/`DELETE /transactions/{id}` por papel `Lancar` numa
      transação de outro membro retorna 403 e nada é alterado
- [x] `PUT`/`DELETE /transactions/{id}` por papel `Leitura` retorna 403
      em qualquer transação
- [x] `PUT /transactions/{id}` preserva `id`, `createdAt` e
      `createdByUserId` originais
- [x] `GET`/`PUT`/`DELETE /transactions/{id}` de transação inexistente
      ou de outra conta retorna 404
- [x] `DELETE /categories/{id}` com transações vinculadas continua
      retornando 422
- [x] `/expenses` não existe mais em nenhuma rota (substituído
      integralmente por `/transactions`)
- [x] `backend/docs/openapi.json` regenerado refletindo a remoção de
      `/expenses`, a criação de `/transactions` (`GET`/`POST`/`PUT`/
      `DELETE`, incluindo `/{id}`), os novos campos (`tipo`, `date` no
      lugar de `expenseDate`, `createdByUserId`, `createdByLabel`) e o
      novo parâmetro de query `tipo` em `GET /transactions`

## Status

Implementado conforme `plan.md`/`tasks.md`. `Expense` (Domain/Application/
Infrastructure/Api) renomeado em cascata pra `Transaction` nas quatro
camadas — mapa completo em `plan.md`. `Transaction` (Domain) ganhou
`Tipo` (`string`, sem enum, mesmo padrão de `Category.Tipo` da FEAT-21)
e `CreatedByUserId` (`string`, sempre presente); `ExpenseDate` virou
`Date`. `ITransactionRepository`/`DynamoDbTransactionRepository`: o
atributo `Tipo` do item DynamoDB deixou de ser a constante `"despesa"`
e passou a gravar o valor real (`"despesa"`\|`"receita"`), continuando
a servir de discriminador do `GSI2` compartilhado com `Category`
(`IsTransactionItem` generalizado pra aceitar os dois valores, ao
invés do antigo `IsDespesaItem`); atributo de data renomeado de
`ExpenseDate` para `Date` no item também (sem custo de compatibilidade
— ver próximo parágrafo); novo atributo `CreatedByUserId`, sempre
gravado; filtro `?tipo=` aplicado via `FilterExpression` do próprio
DynamoDB (sem ressalva de "ausente = default", diferente do que a
FEAT-21 precisou fazer pra `Category.Tipo`).

`RegisterTransactionCommandValidator`/`UpdateTransactionCommandValidator`
validam `tipo` (`despesa`\|`receita`) e cruzam com o `tipo` da
`Category` referenciada numa única consulta (`BeAnOwnedCategoryOfMatchingTypeAsync`,
mesma mensagem genérica pros três casos: categoria inexistente, de
outra conta, ou de tipo divergente). `Update`/`DeleteTransactionCommandHandler`
implementam a checagem de posse do papel `Lancar`: buscam a transação
via `GetByIdAsync` antes de escrever e retornam
`MembershipErrors.InsufficientPermission` (403, reaproveitado — sem
`Error` novo) quando o chamador é `Lancar` e não é o autor;
`Total`/`Titular` seguem sem essa checagem. Novo `CreatedByLabelResolver`
(helper interno) resolve `"Você"`/e-mail do autor/`"Ex-membro"`, usado
por `Register`(direto)/`Update`/`GetTransactionById`/`GetTransactionsQueryHandler`
(este último com cache por página, evitando repetir a consulta de
`Membership` pro mesmo autor). `CurrentAccountContext` ganhou `UserId`,
populado por `ResolveAccountEndpointFilter` a partir do mesmo `userId`
já extraído do JWT. `RoleEndpointFilters` de `PUT`/`DELETE /transactions/{id}`
passaram a incluir `Lancar` no gate estático (a exclusão continua
acontecendo, agora dentro do Handler).

**Decisão confirmada com o usuário durante a revisão do `plan.md`:** a
tabela `GastosApp` local (LocalStack) foi recriada antes da
implementação — não há item gravado como `Expense` (formato antigo)
pra ler, então `CreatedByUserId`/`Date` não precisaram de nenhum
tratamento defensivo de "atributo ausente" (diferente do que a FEAT-21
precisou fazer pra `TipoLancamento`). O mesmo runbook (recriar a
tabela) é pré-requisito de deploy em hom/prod, fora deste código — ver
`plan.md`, "Recursos AWS".

`backend/docs/openapi.json` regenerado localmente (API rodando contra
LocalStack/cognito-local, `backend/infra/`) — `git diff` confirma a
remoção completa de `/expenses`, o novo `/transactions`
(`GET`/`POST`/`PUT`/`DELETE`, incluindo `/{id}`), os campos `tipo`/
`date`/`createdByUserId`/`createdByLabel` nos schemas, e o novo
parâmetro de query `tipo`; nenhuma mudança em `/categories` ou
`/members`.

Suíte completa (`dotnet test` na solução) passa: 472/472 (1
IntegrationTests placeholder + 329 UnitTests + 142 ComponentTests).

## Fora do escopo

- `GET /summary` (dashboard) e `GET /reports` (relatórios) — agregações
  sobre transações ficam para FEAT-23/FEAT-24
- Exportação CSV de transações — FEAT-25
- Comprovante de transação (upload real) — só de UI por decisão já
  fechada no roadmap, sem bucket S3 nem campo no modelo
- Migração/backfill de despesas gravadas antes desta feature (`Expense`,
  formato antigo) — sem compatibilidade retroativa; confirmado com o
  usuário que a tabela `GastosApp` é recriada do zero antes do deploy
  desta feature (dados atuais são só de teste), então nem chega a
  existir item antigo pra ler (ver `plan.md`)
- Tags em transações — requer GSI adicional ou filtro em memória,
  registrado como backlog em `backend/docs/data-model.md`
- Alterar a autorização de `/categories` — a mudança de matriz desta
  feature (papel `Lancar` editar/excluir o que criou) vale só para
  `/transactions`; `/categories` continua exigindo `Total`/`Titular`
  para qualquer escrita, sem noção de autoria (FEAT-20/FEAT-21)
- Papel `Titular` deixar de ser "acesso total" — nenhuma mudança na
  definição de papéis além da nova regra de autoria do `Lancar`
- Bloquear `DELETE /members` para um membro com transações lançadas
  (e introduzir um status `Inativo` em `Membership` pra esse caso, que
  continuaria aparecendo como `createdByLabel` das transações que já
  criou) — confirmado com o usuário como débito técnico, registrado em
  `backend/docs/roadmap.md` para implementação futura. Nesta feature,
  `DELETE /members` mantém o comportamento atual da FEAT-20
  (remove o `Membership` de fato, mesmo com transações associadas);
  `createdByLabel` cai em `"Ex-membro"` quando isso acontece (ver
  `plan.md`)
