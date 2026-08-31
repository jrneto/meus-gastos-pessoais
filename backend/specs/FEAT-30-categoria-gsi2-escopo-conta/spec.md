# FEAT-30: Categoria — escopar busca por ID (GSI2) por conta

## Objetivo

Corrigir bug em que a busca de categoria por id (`GSI2`) não é
escopada por conta: quando duas contas têm categorias com o mesmo
`id` — hoje sempre verdade para as 13 categorias padrão (FEAT-28),
que usam os mesmos ids literais em toda conta nova — a busca pode
devolver o item da conta errada, e o código atual trata isso como
"não encontrado". Inclui a atualização da documentação de dados
(`backend/docs/data-model.md`) para refletir o novo formato de
`GSI2PK` de `Category`.

## Contexto

Bug registrado em `backend/docs/backlog.md` (seção "Débitos técnicos
e melhorias futuras"), encontrado em 2026-08-31 testando a FEAT-29 do
frontend em ambiente local.

**Repro:** conta A convida usuário B
(`backend/specs/FEAT-20-membros-convites-permissoes/`); B se cadastra
(ganha conta própria + 13 categorias padrão via `EnsureAccount`,
FEAT-28) e aceita o convite no login (`AcceptPendingInvites`, troca a
conta ativa pra conta de A); B tenta lançar uma despesa numa das 13
categorias padrão (ex.: "Vestuário e Cuidados Pessoais", id
`0af4581d-37bf-4636-9805-ce2302403330`) — `POST /transactions` retorna
400 `validation-error`/"Categoria inválida." mesmo a categoria
existindo (idêntica) na conta ativa.

**Causa raiz:** `DefaultCategorySeed.cs` (FEAT-28) usa, de propósito,
os mesmos 13 ids literais hardcoded em toda conta nova ("fácil
rastrear a mesma categoria entre ambientes"). Mas
`DynamoDbCategoryRepository.LookupByIdAsync`
(`backend/src/GastosApp.Infrastructure/Categories/DynamoDbCategoryRepository.cs`)
— usada por `GetByIdAsync` (validação de `POST`/`PUT /transactions`,
`ICategoryRepository.GetByIdAsync`), `UpdateAsync` e `DeleteAsync` —
faz `Query` no `GSI2` só por `GSI2PK = "ID#<categoryId>"`, sem filtrar
por conta, com `Limit = 1`. Como o mesmo id existe em várias contas
(uma cópia por conta que tem aquela categoria padrão), o DynamoDB pode
devolver o item de **qualquer uma delas** (sem `ORDER BY`/sort
determinístico nessa query) — o código só descobre depois, comparando
`pk != "ACCOUNT#<accountId esperado>"`, e trata como "não encontrado"
quando o item de outra conta veio primeiro. Afeta qualquer uma das 13
categorias padrão, em qualquer conta, assim que existe mais de uma
conta no ambiente (comum a partir da FEAT-20 — convites — e sempre
verdadeiro em produção com mais de um usuário).

**Decisão de correção (fechada com o usuário, ver backlog):**
corrigir pelo schema, não pela correção rápida de tirar `Limit=1` e
filtrar em memória (teria custo de `Query` crescente por conta e não
resolve a causa). O `GSI2PK` de categoria passa a incluir o
`accountId`: `ID#<accountId>#<categoryId>` em vez de só
`ID#<categoryId>` — busca passa a ser precisa por conta+id, sem
colisão possível, já que quem chama sempre conhece o `accountId` do
requisitante (extraído do JWT, como em todo o resto da API).

`GSI2` já é uma GSI só com hash key (`GSI2PK`, `Projection:
KEYS_ONLY`, ver `backend/infra/scripts/init-dynamodb.sh` e o
Terraform equivalente de hom/prod) — mudar o **formato do valor**
gravado em `GSI2PK` não exige alterar a definição da tabela/GSI no
Terraform, só o dado em si.

**Sem necessidade de backfill:** o usuário confirmou que garantirá as
tabelas `GastosApp`/`GastosApp-Hom`/`GastosApp-Local` zeradas (sem
dado de categoria pré-existente) em todos os ambientes antes do
deploy desta correção — por conta disso, esta feature não precisa de
migração/backfill de dado já gravado, nem de nenhuma estratégia de
leitura dupla/transição para não quebrar categorias existentes. A
correção de schema é aplicada diretamente, como cutover simples.

**Fora de escopo desta spec, por decisão consciente:** aplicar o
mesmo tratamento ao `GSI2` de `Transação`
(`DynamoDbTransactionRepository`, que usa o mesmo padrão
`GSI2PK = "ID#<transactionId>"`sem escopo de conta). Diferente de
`Category`, o id de toda `Transação` é um Guid gerado individualmente
na criação — nunca um id literal compartilhado entre contas como as
13 categorias padrão — então não há colisão possível na prática hoje.
Fica registrado aqui como algo a reavaliar se `Transação` algum dia
ganhar ids compartilhados entre contas (não é o caso hoje).

## Requisitos de negócio

- `GSI2PK` de todo item de categoria passa a ser
  `ID#<accountId>#<categoryId>` (`CategoryItemMapper.BuildItem`),
  tanto para categoria criada via `POST /categories` quanto para as
  13 categorias padrão semeadas na criação da conta
  (`DynamoDbAccountRepository.CreateAsync`, que reusa o mesmo
  mapper).
- A busca de categoria por id (`GetByIdAsync`, e a montagem de chave
  usada internamente por `UpdateAsync`/`DeleteAsync`) passa a incluir
  o `accountId` do chamador diretamente na condição da `Query` no
  `GSI2` — a categoria de uma conta nunca é candidata a "achado" para
  outra conta, mesmo quando o `id` colide.
- `GET /categories/{id}`, `PUT /categories/{id}`, `DELETE
  /categories/{id}` e a validação de categoria em `POST`/`PUT
  /transactions` continuam retornando 404 (ou 400
  `"Categoria inválida."`, conforme já documentado) quando o id não
  existe **na conta do chamador** — mesmo que exista em outra conta.
- Nenhum contrato de API observável muda nesta feature (mesmos
  endpoints, mesmo request/response, mesmos status codes) — é
  correção interna de chave de acesso ao dado.
- `backend/docs/data-model.md` (seção `Category`) passa a documentar
  o novo formato de `GSI2PK` — a documentação de dados nunca fica
  divergente do que o código realmente grava.

## User Stories

**US1 — Duas contas com a mesma categoria padrão não colidem mais**
- Given duas contas distintas, cada uma com a categoria padrão
  "Vestuário e Cuidados Pessoais" (mesmo id literal,
  `0af4581d-37bf-4636-9805-ce2302403330`)
- When um usuário da conta B (ex.: membro convidado, ver repro no
  "Contexto") lança uma transação (`POST /transactions`) referenciando
  essa categoria
- Then a categoria é encontrada na conta B, a transação é criada
  normalmente, e a API retorna 201 — sem 400 `"Categoria inválida."`

**US2 — `GET /categories/{id}` nunca expõe categoria de outra conta**
- Given duas contas distintas com categorias de mesmo id
- When um usuário de uma das contas chama `GET /categories/{id}`
- Then a resposta é sempre a categoria da **sua própria** conta,
  nunca a de outra, independente da ordem em que o DynamoDB retornar
  candidatos internamente

**US3 — `PUT /categories/{id}` só altera a categoria da própria conta**
- Given duas contas distintas com categorias de mesmo id
- When um usuário com role `Total`/`Titular` de uma das contas chama
  `PUT /categories/{id}` alterando nome/tipo/orçamento
- Then só o item da sua própria conta é alterado; o item de mesmo id
  na outra conta permanece intocado

**US4 — `DELETE /categories/{id}` só exclui a categoria da própria conta**
- Given duas contas distintas com categorias de mesmo id
- When um usuário com role `Total`/`Titular` de uma das contas chama
  `DELETE /categories/{id}`
- Then só o item da sua própria conta é excluído; o item de mesmo id
  na outra conta permanece intacto e continua acessível normalmente
  pela conta dele

**US5 — Id inexistente na própria conta continua 404, mesmo existindo em outra**
- Given uma categoria com id `X` que existe na conta A mas não foi
  seedada/criada na conta B
- When um usuário da conta B chama `GET`/`PUT`/`DELETE
  /categories/X`
- Then a API retorna 404 `not-found` — o item de mesmo id em outra
  conta nunca "vaza" como resultado

## Contratos da API

Esta feature **não introduz nem altera nenhum endpoint, campo ou
status code**. Todos os contratos abaixo continuam idênticos aos já
documentados em `backend/docs/openapi.json`:

- `GET/POST/PUT/DELETE /categories` — sem mudança
  (`backend/specs/FEAT-16-crud-categorias/`,
  `backend/specs/FEAT-21-categoria-tipo-orcamento/`)
- `POST/PUT /transactions` — sem mudança de contrato; só o
  comportamento interno de validação de `categoryId` deixa de
  colidir entre contas (`backend/specs/FEAT-22-transacoes-receita-despesa/`)

`backend/docs/openapi.json` não deve ter diff de contrato ao final
desta feature — é critério de aceite justamente a ausência de
mudança, dado que é uma correção interna.

## Critérios de aceite

- [x] `GSI2PK` de categoria passa a ser `ID#<accountId>#<categoryId>`,
      tanto para categorias criadas via `POST /categories` quanto
      para as 13 categorias padrão semeadas na criação da conta
- [x] Repro exato do bug (US1: conta B convidada por A, lança
      transação numa categoria padrão) passa a funcionar sem 400 —
      garantido pela correção de schema em si (a `Query` já sai
      escopada por conta) e verificado no nível de unidade; sem
      execução end-to-end contra a API real (ver "Status")
- [x] `GET`/`PUT`/`DELETE /categories/{id}` nunca leem, alteram ou
      excluem item de outra conta quando duas contas têm categoria
      com o mesmo id (US2, US3, US4)
- [x] Id inexistente na própria conta continua retornando 404, mesmo
      existindo em outra conta (US5)
- [x] Nenhum contrato de API muda — `backend/docs/openapi.json`
      regenerado sem diffs de contrato
- [x] `backend/docs/data-model.md` (seção `Category`) atualizado com o
      novo formato de `GSI2PK`
- [x] Suíte de testes (unitário/componente) passando — sem teste
      integrado nesta feature, por decisão do usuário (ver "Fora do
      escopo": lacuna de teste integrado de `categories`/
      `transactions` continua coberta pelo débito técnico já
      registrado na FEAT-29)
- [ ] Tabelas `GastosApp`/`GastosApp-Hom`/`GastosApp-Local` zeradas
      pelo usuário em todos os ambientes antes do deploy desta
      correção (pré-condição, fora do escopo de implementação desta
      feature — pendente do lado do usuário, ver "Status")

## Status

Implementado conforme `plan.md`/`tasks.md`. `CategoryItemMapper.BuildItem`
grava `GSI2PK = ID#<accountId>#<categoryId>` (antes só
`ID#<categoryId>`), cobrindo tanto `POST /categories` quanto o seed
das 13 categorias padrão (`DynamoDbAccountRepository.CreateAsync`, que
reusa o mesmo mapper). `DynamoDbCategoryRepository.LookupByIdAsync`
ganhou o parâmetro `accountId` e passou a consultar o `GSI2` já
escopado por conta+id — `GetByIdAsync`/`UpdateAsync`/`DeleteAsync`
perderam o post-check `pk != "ACCOUNT#<accountId>"`, redundante agora
que a `Query` nunca devolve item de outra conta. `MapToCategory`
passou a extrair o `id` via `LastIndexOf('#')` em vez de `IndexOf('#')`.

**Sem backfill**: por decisão do usuário, as tabelas
`GastosApp`/`GastosApp-Hom`/`GastosApp-Local` serão zeradas
manualmente em todos os ambientes antes do deploy — cutover simples,
sem estratégia de leitura dupla/transição (ver "Contexto").

**Sem teste integrado**: por decisão do usuário, dado que não existe
ainda infraestrutura de teste integrado para `categories`/
`transactions`/`members` (débito técnico da FEAT-29) e construí-la só
para este bugfix seria desproporcional. A regressão do bug é coberta
no nível de unidade: os três testes que simulavam "`Query` devolvendo
item de outra conta" (cenário que a `Query` real não produz mais)
foram substituídos por testes que capturam o `QueryRequest` enviado e
confirmam que o `GSI2PK` buscado já inclui o `accountId` — não houve
execução end-to-end do repro original (US1) contra a API real.

`backend/docs/data-model.md` atualizado (seção `Category` e "Espaço de
chave compartilhado entre tipos de item de uma conta" — `Category` e
`Transaction` deixaram de compartilhar o mesmo formato de `GSI2PK`).
`backend/docs/openapi.json` regenerado — `git diff` confirma zero
diferença de contrato, como esperado (correção interna).

Suíte de testes: 469 unitários + 205 de componente passando (674/674).
Teste de integração (`AuthFlowTests`, não relacionado a esta mudança)
não rodou nesta sessão por falta da API local ativa no momento da
execução — fora do escopo desta feature de qualquer forma (ver acima).

**Pendência antes do deploy**: o usuário ainda precisa zerar as
tabelas `GastosApp`/`GastosApp-Hom`/`GastosApp-Local` em todos os
ambientes — pré-condição desta correção, fora do que a implementação
em si garante.

## Fora do escopo

- Aplicar o mesmo tratamento ao `GSI2` de `Transação`
  (`DynamoDbTransactionRepository`) — sem risco prático hoje porque
  ids de transação são sempre Guids gerados individualmente, nunca
  compartilhados entre contas (ver "Contexto")
- `DELETE /members` remover em vez de inativar membro — item
  separado do backlog (`backend/docs/backlog.md`), sem relação com
  este bug
- Qualquer mudança de contrato de `/categories` ou `/transactions`
  além da correção interna descrita aqui
- Login não exigir perfil completo quando o usuário é criado
  diretamente no Cognito — outro item separado do backlog, sem
  relação
- Migração/backfill de dado de categoria já gravado — não é
  necessário: o usuário garantirá as tabelas zeradas em todos os
  ambientes antes do deploy desta correção (ver "Contexto")
- Teste integrado do repro (US1) ou de qualquer outro cenário desta
  feature — decisão do usuário, por não existir ainda infraestrutura
  de teste integrado para `categories`/`transactions`/`members`
  (débito técnico já registrado, `backend/specs/FEAT-29-testes-integrados/spec.md`);
  cobertura fica em unit + componente
