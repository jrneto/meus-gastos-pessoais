# FEAT-21: Categoria — tipo, orçamento e remoção de cor/ícone

## Objetivo

Adicionar dois atributos de negócio a `Category` (FEAT-16): `tipo`
(`despesa` ou `receita`, obrigatório) e `orcamentoMensalCents`
(opcional). `GET /categories` passa a aceitar filtro por `tipo`.
Editar/definir orçamento continua exigindo role `Total` (ou `Titular`)
— mesma regra de escrita já aplicada a `/categories` inteira desde a
FEAT-20. Ao mesmo tempo, remove `cor` e `icone` do contrato — campos
introduzidos pela FEAT-16 que o design system atual não usa mais (ver
"Contexto").

## Contexto

Hoje `Category` (`backend/specs/FEAT-16-crud-categorias/`) tem `nome`,
`cor` e `icone` — sem noção de tipo (despesa/receita) nem de
orçamento. O design system (`frontend/design-system/jrnexpenses-web.dc.html`,
tela "Categorias e orçamentos", e `screenshots/11-categorias-orcamentos.png`)
já assume que toda categoria tem um orçamento mensal opcional, exibido
como barra de progresso (gasto atual / orçamento) — mas revisitando o
mockup atual, confirma-se que **`cor` e `icone` não existem mais**: o
formulário "Nova categoria" só pede `nome` e orçamento mensal, e toda
categoria é exibida com um avatar de letra derivado automaticamente da
primeira letra do próprio nome (ex.: "Alimentação" → "A"), sem nenhuma
cor customizável nem catálogo de ícones — a cor usada na barra de
progresso é só o indicador visual de estouro de orçamento (mesma cor
para toda categoria), não um atributo por categoria. Esses dois campos
foram introduzidos pela FEAT-16 antes de o design system chegar a esse
estado; como esta feature já mexe no mesmo contrato (`POST`/`PUT`/`GET
/categories`) para adicionar `tipo`/`orcamentoMensalCents`, é o momento
natural de removê-los — evita uma segunda mudança de contrato só para
isso, e o front (ainda não iniciado) nunca chegou a depender deles.

O campo `tipo` é pré-requisito direto da FEAT-22 (generalização de
`Expense` para `Transação` com `tipo`), que vai usar o `tipo` da
categoria vinculada para decidir se um lançamento é despesa ou
receita — mas essa ligação em si é escopo da FEAT-22, não desta.

Segue `backend/docs/roadmap.md` (item "FEAT-21 — Categoria: tipo e
orçamento") e `backend/docs/data-model.md` (seção "Backlog", que já
antecipava os dois atributos).

**Decisões de escopo fechadas nesta spec:**

1. **`tipo` é obrigatório em toda categoria, sem valor implícito.**
   Categorias criadas antes desta feature não são migradas — não há
   necessidade de compatibilidade retroativa (decisão já fechada no
   roadmap: a tabela pode ser recriada do zero). `GET /categories`
   depois desta feature assume que toda categoria tem `tipo` válido.
2. **`tipo` é editável via `PUT /categories/{id}`**, como qualquer
   outro campo já editável (`nome`) — o `PUT` continua exigindo o
   corpo completo (não é `PATCH` parcial), então `tipo` passa a ser
   mais um campo obrigatório nesse corpo. Trocar o `tipo` de uma
   categoria já referenciada por despesas não é validado nem
   bloqueado nesta feature (validar `tipo` de categoria contra
   lançamentos é escopo da FEAT-22).
3. **Nenhuma role nova é criada para orçamento.** `POST`/`PUT
   /categories` já exigem role `Total` ou `Titular` desde a FEAT-20 —
   como orçamento é só mais um campo desses mesmos endpoints, "editar
   orçamento exige Total" (texto do roadmap) já está coberto pela
   regra existente, sem granularidade adicional por campo.
4. **`orcamentoMensalCents` aceita `null`/ausência (sem orçamento
   definido) ou um inteiro positivo em centavos** — `0` e valores
   negativos são rejeitados (um orçamento de R$ 0,00 não expressa
   nada de útil; frontend trata "sem orçamento" só pela ausência do
   campo, sem usar `0` como sentinela).
5. **`GET /categories?tipo=`** aceita exatamente `despesa` ou
   `receita`; qualquer outro valor retorna 400. Sem o parâmetro,
   continua retornando todas as categorias da conta, como hoje.
6. **`cor` e `icone` deixam de existir no contrato de `Category`.**
   `POST`/`PUT /categories` não os aceitam mais e `GET /categories`
   não os retorna mais. Um cliente antigo que ainda envie esses campos
   no corpo não recebe erro — eles são simplesmente ignorados (mesmo
   comportamento padrão de desserialização já usado pelo projeto para
   campos desconhecidos). Não há dado a migrar: nenhuma tela de
   produção depende de `cor`/`icone` hoje (frontend ainda não
   iniciado).

## Requisitos de negócio

- `tipo`: obrigatório em `POST`/`PUT /categories`, aceita somente
  `"despesa"` ou `"receita"` — qualquer outro valor (incluindo
  ausente/vazio) retorna 400
- `orcamentoMensalCents`: opcional (pode ser omitido ou `null`,
  significando "sem orçamento definido"); quando informado, deve ser
  um inteiro maior que zero (centavos) — `0`, negativo ou não-inteiro
  retorna 400
- `cor` e `icone` não fazem mais parte do request nem do response de
  nenhuma rota de `/categories` — enviá-los não é erro, só não tem
  efeito algum
- `GET /categories?tipo=despesa` (ou `receita`) retorna somente
  categorias daquele tipo, na conta ativa do chamador; sem o
  parâmetro, retorna todas, independente do tipo; valor de `tipo`
  fora de `despesa`/`receita` retorna 400
- Criar ou editar categoria (incluindo só o orçamento) continua
  exigindo role `Total` ou `Titular` na conta ativa (regra já
  aplicada pela FEAT-20 a todo `POST`/`PUT`/`DELETE /categories`,
  sem mudança de comportamento aqui)
- `GET /categories`/`GET /categories/{id}` continuam liberados para
  qualquer role autenticada da conta (`Leitura` incluído), como hoje
- Demais regras de `Category` já existentes (unicidade de `nome` por
  conta via slug, bloqueio de exclusão com despesas associadas,
  isolamento entre contas) não mudam nesta feature

## User Stories

**US1 — Criar categoria com tipo e sem orçamento**
- Given um usuário autenticado com role `Total` ou `Titular`
- When ele envia `POST /categories` com `nome` e `tipo` válidos, sem
  `orcamentoMensalCents`
- Then a categoria é criada com o `tipo` informado e
  `orcamentoMensalCents` nulo, e a API retorna 201

**US2 — Criar categoria com orçamento**
- Given um usuário autenticado com role `Total` ou `Titular`
- When ele envia `POST /categories` com `tipo` válido e
  `orcamentoMensalCents` igual a um inteiro positivo
- Then a categoria é criada com esse orçamento, e a API retorna 201
  com o valor exato em `orcamentoMensalCents`

**US3 — Rejeitar tipo inválido ou ausente**
- Given um usuário autenticado com role `Total` ou `Titular`
- When ele envia `POST`/`PUT /categories` com `tipo` ausente, vazio
  ou fora de `despesa`/`receita`
- Then a API retorna 400 e nenhuma categoria é criada/alterada

**US4 — Rejeitar orçamento inválido**
- Given um usuário autenticado com role `Total` ou `Titular`
- When ele envia `POST`/`PUT /categories` com `orcamentoMensalCents`
  igual a `0`, negativo ou não-inteiro
- Then a API retorna 400 e nenhuma categoria é criada/alterada

**US5 — Editar orçamento de categoria existente**
- Given um usuário autenticado com role `Total` ou `Titular`, com uma
  categoria sua sem orçamento definido
- When ele envia `PUT /categories/{id}` com os demais campos válidos
  e `orcamentoMensalCents` igual a um inteiro positivo
- Then o orçamento é atualizado e a API retorna 200 com o novo valor

**US6 — Remover orçamento de categoria existente**
- Given um usuário autenticado com role `Total` ou `Titular`, com uma
  categoria sua com orçamento definido
- When ele envia `PUT /categories/{id}` com os demais campos válidos
  e `orcamentoMensalCents` omitido ou `null`
- Then o orçamento é removido (volta a `null`) e a API retorna 200

**US7 — Impedir edição de orçamento por role sem permissão**
- Given um usuário autenticado com role `Leitura` ou `Lancar`
- When ele tenta `PUT /categories/{id}` alterando
  `orcamentoMensalCents` (ou qualquer outro campo)
- Then a API retorna 403 e nenhuma alteração é feita (mesmo
  comportamento já garantido pela FEAT-20 para `/categories`)

**US8 — Filtrar categorias por tipo**
- Given um usuário autenticado com categorias de ambos os tipos
  cadastradas na conta ativa
- When ele chama `GET /categories?tipo=receita`
- Then a API retorna somente as categorias com `tipo="receita"`

**US9 — Consultar categorias sem filtro de tipo**
- Given um usuário autenticado com categorias de ambos os tipos
- When ele chama `GET /categories` sem o parâmetro `tipo`
- Then a API retorna todas as categorias da conta, dos dois tipos,
  como hoje

**US10 — Rejeitar filtro de tipo inválido**
- Given um usuário autenticado
- When ele chama `GET /categories?tipo=invalido`
- Then a API retorna 400 e nenhuma categoria é retornada

**US11 — Enviar cor/icone não tem efeito**
- Given um usuário autenticado com role `Total` ou `Titular`
- When ele envia `POST`/`PUT /categories` incluindo `cor` e/ou `icone`
  no corpo, além dos campos válidos do contrato atual
- Then a categoria é criada/atualizada normalmente e a resposta não
  contém `cor` nem `icone` — os dois campos são ignorados, sem erro

## Contratos da API

### GET /categories

Query string opcional: `tipo` (`despesa` \| `receita`). Sem o
parâmetro, retorna todas as categorias da conta ativa do chamador,
como hoje.

Response 200:
```json
{
  "items": [
    {
      "id": "...",
      "nome": "Alimentacao",
      "tipo": "despesa",
      "orcamentoMensalCents": 80000,
      "createdAt": "2025-06-15T12:34:56Z"
    },
    {
      "id": "...",
      "nome": "Salario",
      "tipo": "receita",
      "orcamentoMensalCents": null,
      "createdAt": "2025-06-15T12:34:56Z"
    }
  ]
}
```

Response 400 (validation-error): `tipo` informado fora de
`despesa`/`receita`.

### POST /categories

Request:
```json
{
  "nome": "Viagem",
  "tipo": "despesa",
  "orcamentoMensalCents": 50000
}
```
`tipo`: `"despesa"` \| `"receita"` (obrigatório).
`orcamentoMensalCents`: inteiro positivo em centavos, ou omitido/`null`
(opcional). `cor`/`icone` não fazem mais parte do request (ver
"Decisões de escopo fechadas nesta spec").

Response 201 (Location: /categories/{id}):
```json
{
  "id": "...",
  "nome": "Viagem",
  "tipo": "despesa",
  "orcamentoMensalCents": 50000,
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 400 (validation-error): campo obrigatório ausente/inválido
(inclui `tipo` fora de `despesa`/`receita`, e
`orcamentoMensalCents` igual a `0`/negativo/não-inteiro).
Response 403 (insufficient-permission): role sem permissão de escrita
em `/categories` (`Leitura`/`Lancar`).
Response 422 (name-conflict): já existe categoria com esse nome.

### PUT /categories/{id}

Request (corpo completo, mesmo padrão já usado hoje):
```json
{
  "nome": "Viagens",
  "tipo": "despesa",
  "orcamentoMensalCents": 60000
}
```

Response 200: dados atualizados da categoria (mesmo formato do
`POST`).
Response 400 (validation-error): campo obrigatório ausente/inválido.
Response 403 (insufficient-permission): role sem permissão de escrita.
Response 404 (not-found): categoria não existe ou não pertence à conta.
Response 422 (name-conflict): nome já usado por outra categoria da
conta.

### DELETE /categories/{id}

Sem mudança de contrato nesta feature — continua conforme
`backend/specs/FEAT-16-crud-categorias/spec.md` e
`backend/specs/FEAT-20-membros-convites-permissoes/spec.md` (role
`Total`/`Titular`, bloqueado por 422 quando há despesas associadas).

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
  "detail": "Categoria não encontrada."
}
```

Response 422 (name-conflict):
```json
{
  "type": "https://gastosapp.dev/errors/name-conflict",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Já existe uma categoria com esse nome."
}
```

## Critérios de aceite

- [x] `POST /categories` com `tipo` válido e sem `orcamentoMensalCents`
      cria a categoria com orçamento nulo e retorna 201
- [x] `POST /categories` com `tipo` e `orcamentoMensalCents` válidos
      (inteiro positivo) cria a categoria com esse orçamento e retorna
      201
- [x] `POST`/`PUT /categories` com `tipo` ausente ou fora de
      `despesa`/`receita` retorna 400
- [x] `POST`/`PUT /categories` com `orcamentoMensalCents` igual a `0`,
      negativo ou não-inteiro retorna 400
- [x] `PUT /categories/{id}` atualiza `orcamentoMensalCents` de um
      valor existente para outro válido e retorna 200
- [x] `PUT /categories/{id}` com `orcamentoMensalCents` omitido/`null`
      remove um orçamento previamente definido (volta a `null`) e
      retorna 200
- [x] `PUT /categories/{id}` chamado por role `Leitura`/`Lancar`
      retorna 403 e nada é alterado (comportamento já coberto pela
      FEAT-20, sem regressão)
- [x] `GET /categories?tipo=despesa` retorna só categorias de tipo
      despesa; `?tipo=receita` retorna só as de receita
- [x] `GET /categories` sem `tipo` continua retornando todas as
      categorias da conta, dos dois tipos
- [x] `GET /categories?tipo=` com valor fora de `despesa`/`receita`
      retorna 400
- [x] `POST`/`PUT /categories` respondem 201/200 normalmente mesmo
      quando `cor`/`icone` são enviados no corpo, e a resposta não
      contém esses campos
- [x] `GET /categories` não retorna `cor` nem `icone` em nenhum item
- [x] Todas as regras já existentes de `Category` (unicidade de nome,
      bloqueio de exclusão com despesas associadas, isolamento entre
      contas, 401 sem token) continuam passando sem regressão
- [x] `backend/docs/openapi.json` regenerado refletindo os novos
      campos (`tipo`, `orcamentoMensalCents`), a remoção de
      `cor`/`icone`, e o novo parâmetro de query (`tipo`) em
      `GET /categories`

## Status

Implementado conforme `plan.md`/`tasks.md`. `Category` (Domain) perdeu
`Cor`/`Icone` e ganhou `Tipo` (`string`, sem enum) e
`OrcamentoMensalCents` (`long?`). `ICategoryRepository.ListAsync`
ganhou o parâmetro `tipo`; `UpdateAsync` trocou `cor`/`icone` por
`tipo`/`orcamentoMensalCents`. `CreateCategoryCommand`/
`UpdateCategoryCommand`/`GetCategoriesQuery` (+ Validators, Results)
atualizados na mesma linha; novo `GetCategoriesQueryValidator`
registrado manualmente em
`ApplicationServiceCollectionExtensions` (primeiro Query validator do
módulo de categorias, mirror de `GetExpensesQueryValidator`).

`DynamoDbCategoryRepository`: novo atributo `TipoLancamento` — nome
escolhido de propósito para não colidir com o atributo interno `Tipo`
já existente no item (discriminador do `GSI2` compartilhado com
`Expense`, sempre `"categoria"`, inalterado). `OrcamentoMensalCents`
gravado só quando informado (omitido, não `NULL`, quando ausente).
Categorias gravadas antes desta feature (sem `TipoLancamento`) são
lidas como `"despesa"` implícito — mesma postura defensiva já usada
pro discriminador `Tipo`. Filtro `?tipo=` aplicado em memória, depois
do mapeamento (não via `FilterExpression`), pra esse default participar
corretamente do filtro. `Cor`/`Icone` só pararam de ser gravados/lidos
— categorias antigas que ainda os têm no item mantêm esses atributos
órfãos até a próxima edição (que sobrescreve o item inteiro), sem
nenhuma migração/cleanup ativo.

`CategoryEndpoints`: `CreateCategoryRequest`/`UpdateCategoryRequest`
refletem os novos campos; `GetCategories` passou a usar
`[AsParameters] GetCategoriesRequest` (`Tipo`) com o mesmo padrão de
`NullIfEmpty` já usado em `ExpenseEndpoints`. Nenhum `ErrorType`/`Error`
novo — reaproveita `ErrorType.Validation` (400) para `tipo`/
`orcamentoMensalCents` inválidos.

`backend/docs/openapi.json` regenerado localmente (API rodando contra
LocalStack/cognito-local, `backend/infra/`) — `git diff` confirma só as
mudanças esperadas nos schemas de `Category` (remoção de `cor`/`icone`,
adição de `tipo`/`orcamentoMensalCents`) e o novo parâmetro de query
`tipo`/novo `400` em `GET /categories`, sem tocar em `/expenses` ou
`/members`.

Suíte completa (`dotnet test` na solução) passa: 454/454 (1
IntegrationTests placeholder + 299 UnitTests + 154 ComponentTests).

## Fora do escopo

- Validar/usar o `tipo` da categoria em `/expenses` (ex.: impedir
  lançar uma despesa numa categoria de tipo `receita`) — só chega na
  FEAT-22, que generaliza `Expense` para `Transação`
- Cálculo de gasto atual vs. orçamento (a barra de progresso do
  mockup) — isso é o `GET /summary` da FEAT-23, que agrega despesas
  por categoria; esta feature só guarda e expõe o valor do orçamento
- Orçamento versionado por mês (histórico de mudanças de orçamento)
  — é sempre um valor recorrente único por categoria, como já fechado
  no roadmap
- Migração/backfill de categorias criadas antes desta feature — não
  há compatibilidade retroativa (mesma decisão já aplicada à tabela
  inteira desde o início do roadmap atual)
- Qualquer forma de identidade visual customizável por categoria
  (cor, ícone, emoji) — removida nesta feature, sem substituto; se o
  design system voltar a precisar de algo assim no futuro, é escopo
  de uma feature própria, não uma reintrodução automática de
  `cor`/`icone`
- Seed de categorias padrão — FEAT-27
