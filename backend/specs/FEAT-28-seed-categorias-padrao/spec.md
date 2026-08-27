# FEAT-28: Seed de categorias padrão

## Objetivo

Criar automaticamente 13 categorias padrão (tipo `despesa`, sem
orçamento definido) para toda `Account` nova, com ids fixos e iguais em
todos os ambientes, reaproveitando o mesmo gatilho de criação de conta
da FEAT-19 (trigger `Post Confirmation` do Cognito, com fallback
idempotente no primeiro login). Assim o usuário já enxerga um ponto de
partida útil em `GET /categories` assim que loga, sem precisar cadastrar
nada manualmente.

## Contexto

Já tinha sido cogitado na FEAT-16 e conscientemente adiado (ver "Fora do
escopo" de `backend/specs/FEAT-16-crud-categorias/spec.md`), e de novo
deixado fora da FEAT-19 por depender dela como gatilho (ver
`backend/specs/FEAT-19-conta-multi-tenant/spec.md`). Retomado agora —
fora da ordem original do roadmap (`backend/docs/roadmap.md`), a pedido
do usuário, antes da FEAT-27.

**Decisões de escopo fechadas com o usuário nesta spec:**

1. **Só os 13 grupos da lista viram categoria — sem subcategoria.** A
   lista original trazia grupos com itens dentro (ex.: "Moradia —
   Aluguel/Financiamento, Condomínio, Água/Luz/Gás, Internet/Telefone,
   Manutenção da casa"). Hoje `Category` (FEAT-16/FEAT-21) é totalmente
   plana, sem noção de categoria pai/subcategoria. Decidido que apenas
   os 13 nomes de grupo viram categoria; os itens internos de cada grupo
   não viram categoria nem qualquer outro dado persistido por esta
   feature — servem só de contexto da lista original. Se uma hierarquia
   real (categoria + subcategoria) for necessária no futuro, é escopo de
   uma feature própria que introduz esse conceito no domínio.
2. **Todas nascem com `tipo="despesa"`.** A lista informada é
   inteiramente de despesas; nenhuma categoria de receita padrão (ex.:
   "Salário") é criada por esta feature.
3. **Sem proteção especial.** Categoria padrão é só um ponto de partida:
   o usuário pode editar (nome/tipo/orçamento) ou excluir livremente,
   com a mesma regra já existente de bloqueio de exclusão quando há
   transação associada (`backend/specs/FEAT-16-crud-categorias/spec.md`).
   Se excluída ou renomeada, não é recriada — o seed roda uma única vez
   por conta, não é uma sincronização contínua.
4. **Ids fixos = GUID literal hardcoded no código**, igual em
   dev/hom/prod, listado nesta spec. Não muda o tipo de `Category.Id`
   (continua string) — só fixa o valor gravado para estas 13 categorias
   específicas, em vez do `Guid.NewGuid()` aleatório usado hoje para
   categorias criadas pelo usuário.
5. **Mesmo gatilho da FEAT-19, criado atomicamente junto de `Account`/
   `Membership`**: o seed roda na mesma operação que cria a conta
   (trigger `Post Confirmation`, com fallback no primeiro login quando
   o trigger não rodou) — conta, titular e as 13 categorias nascem
   juntos ou nenhum deles nasce (tudo ou nada). Falha transitória em
   qualquer parte dessa criação nunca impede a confirmação do cadastro
   nem bloqueia o login — a criação inteira é re-tentada por completo
   no próximo login, sem duplicar o que já existir (mesma postura de
   resiliência da FEAT-19, agora cobrindo também as categorias).
6. **Sem backfill.** Só contas criadas a partir do deploy desta feature
   recebem o seed automaticamente; nenhuma conta já existente antes
   desse deploy é populada retroativamente por esta feature.

### Categorias padrão e seus ids fixos

| Nome | Id (fixo, igual em todo ambiente) |
|---|---|
| Moradia | `862d8a7c-c3ef-412b-b4d3-88c1b4d317d9` |
| Alimentação | `369a308a-f96e-4ba9-ac43-3c9e8696141f` |
| Transporte | `a95ac718-1608-4c64-96da-4eefdc33e3e9` |
| Saúde | `2644f155-1215-4936-8f9a-606e0ba58315` |
| Educação | `ceb83cec-9ca0-4ec0-a58f-adac83574faf` |
| Filhos e Dependentes | `f2d554c0-16d6-4fee-bef1-3364d9bb8ec3` |
| Lazer e Entretenimento | `24ef9ebc-58b3-4197-b9ac-1f203b79f07b` |
| Vestuário e Cuidados Pessoais | `0af4581d-37bf-4636-9805-ce2302403330` |
| Pets | `319ddec7-f867-427f-997a-66cd4ed9d8e1` |
| Dívidas e Financiamentos | `89bfe4ec-8747-44d3-92ba-4266960dd00f` |
| Impostos, Taxas e Seguros | `961a8b3c-d210-4bd5-a470-1ef15c3549c3` |
| Doações e Presentes | `d8865733-b002-4b11-b160-94237b2391c1` |
| Outros | `e9b32f2d-3eb7-4318-a268-438bb2d72f44` |

Todas com `tipo="despesa"` e `orcamentoMensalCents=null`.

## Requisitos de negócio

- Toda vez que uma `Account` é criada (trigger de confirmação ou
  fallback no primeiro login), o sistema também cria as 13 categorias
  padrão da tabela acima, vinculadas a essa conta, cada uma com o `id`
  fixo correspondente
- O seed é idempotente: uma conta que já tem as categorias padrão (no
  todo ou em parte) nunca recebe duplicata — inclusive sob concorrência
  (mesma garantia já exigida para `Account`/`Membership` na FEAT-19,
  ex.: trigger e login quase simultâneos, ou múltiplos logins em
  paralelo)
- A criação da conta (`Account`/`Membership`/13 categorias padrão) é
  atômica: ou tudo é criado numa única operação, ou nada é — não existe
  conta criada com só parte das categorias padrão. Se qualquer parte
  falhar (incluindo o cenário de borda em que já existisse uma
  categoria com o mesmo slug de uma categoria padrão — só possível com
  dado corrompido/manual, já que a conta é recém-criada), a criação
  inteira é desfeita e re-tentada do zero no próximo login, sem
  duplicar o que já existir
- Falha transitória durante essa criação nunca impede a confirmação do
  cadastro nem o login — é sempre re-tentada por completo assim que
  possível (ex.: próximo login)
- Categoria padrão não tem nenhuma restrição adicional em relação a uma
  categoria criada manualmente: pode ser editada (`PUT
  /categories/{id}`) ou excluída (`DELETE /categories/{id}`) livremente,
  respeitando a regra já existente de bloqueio de exclusão quando há
  transação associada
- Uma categoria padrão editada ou excluída não é recriada automaticamente
  — o seed roda uma única vez, no momento da criação da conta
- Contas criadas antes do deploy desta feature não recebem o seed
  retroativamente

## User Stories

**US1 — Confirmação de cadastro cria a conta com as categorias padrão**
- Given um usuário que confirma o cadastro pela primeira vez
- When `Account`/`Membership` são criados pelo trigger de confirmação
  (FEAT-19)
- Then as 13 categorias padrão também são criadas para essa conta, cada
  uma com o `id` fixo correspondente

**US2 — Login sem trigger cria conta e categorias padrão**
- Given um usuário confirmado no Cognito, sem `Account` ainda (trigger
  não rodou)
- When ele faz `POST /auth/login` com sucesso pela primeira vez
- Then `Account`/`Membership` e as 13 categorias padrão são criadas
  nesse momento, e o login retorna 200 normalmente

**US3 — GET /categories reflete o seed sem nenhuma ação manual**
- Given uma conta recém-criada, com o seed já aplicado
- When o usuário chama `GET /categories` antes de criar qualquer
  categoria própria
- Then a API retorna as 13 categorias padrão, com `nome`/`tipo`/`id`
  conforme a tabela desta spec (em vez de lista vazia)

**US4 — Seed não duplica em logins subsequentes**
- Given uma conta cujo seed já foi aplicado
- When o usuário faz login novamente (ou múltiplos logins em paralelo)
- Then nenhuma categoria padrão duplicada é criada — `GET /categories`
  continua retornando as mesmas 13, mais o que o usuário tiver criado ou
  alterado

**US5 — Categoria padrão pode ser editada livremente**
- Given uma conta com uma categoria padrão (ex.: "Lazer e
  Entretenimento")
- When o usuário chama `PUT /categories/{id}` alterando nome, tipo e/ou
  orçamento
- Then a categoria é atualizada normalmente (200), sem nenhuma
  restrição além das já existentes para qualquer categoria

**US6 — Categoria padrão pode ser excluída livremente**
- Given uma conta com uma categoria padrão sem nenhuma transação
  associada
- When o usuário chama `DELETE /categories/{id}`
- Then a categoria é excluída (204) e não é recriada automaticamente
  depois

**US7 — Categoria padrão com transações associadas não pode ser excluída**
- Given uma conta com uma categoria padrão referenciada por ao menos
  uma transação
- When o usuário chama `DELETE /categories/{id}`
- Then a API retorna 422, mesma regra já existente para qualquer
  categoria (`backend/specs/FEAT-16-crud-categorias/spec.md`)

**US8 — Ids fixos são iguais em qualquer ambiente**
- Given a mesma categoria padrão (ex.: "Moradia") em contas de dev, hom
  e prod
- When qualquer uma dessas contas é consultada via `GET /categories`
- Then o `id` retornado para "Moradia" é sempre
  `862d8a7c-c3ef-412b-b4d3-88c1b4d317d9`, independente do ambiente

**US9 — Falha transitória na criação não bloqueia confirmação/login**
- Given uma falha transitória durante a criação atômica de `Account`/
  `Membership`/categorias padrão (na confirmação de cadastro ou no
  login)
- When o restante do fluxo de confirmação/login prossegue
- Then a confirmação/login não falha por causa disso, e a criação
  inteira (conta, titular e as 13 categorias) é re-tentada por completo
  assim que possível (ex.: próximo login), sem duplicar o que já
  existir — nunca fica com a conta criada e só parte das categorias

## Contratos da API

Esta feature **não introduz nem altera nenhum endpoint**. O único efeito
observável é que `GET /categories` para uma conta recém-criada deixa de
retornar lista vazia por padrão — passa a refletir as 13 categorias
padrão até o usuário alterá-las.

### GET /categories (comportamento após esta feature)

Response 200 para uma conta recém-criada, antes de qualquer categoria
manual:
```json
{
  "items": [
    {
      "id": "862d8a7c-c3ef-412b-b4d3-88c1b4d317d9",
      "nome": "Moradia",
      "tipo": "despesa",
      "orcamentoMensalCents": null,
      "createdAt": "2025-06-15T12:34:56Z"
    },
    {
      "id": "369a308a-f96e-4ba9-ac43-3c9e8696141f",
      "nome": "Alimentação",
      "tipo": "despesa",
      "orcamentoMensalCents": null,
      "createdAt": "2025-06-15T12:34:56Z"
    }
  ]
}
```
(demais 11 categorias padrão seguem o mesmo formato, ver tabela de ids
fixos acima)

`POST`/`PUT`/`DELETE /categories` continuam com o contrato exato de
`backend/specs/FEAT-21-categoria-tipo-orcamento/spec.md` — uma categoria
padrão é indistinguível de uma criada manualmente em qualquer resposta
da API.

## Critérios de aceite

- [ ] Confirmar o cadastro no Cognito cria, além de `Account`/
      `Membership` (FEAT-19), as 13 categorias padrão com os ids fixos
      desta spec
- [ ] Login bem-sucedido de um usuário sem `Account` ainda (trigger não
      rodou) cria `Account`/`Membership` e as 13 categorias padrão nesse
      momento
- [ ] `GET /categories` de uma conta recém-criada retorna as 13
      categorias padrão (nome/tipo/id conforme a tabela), sem nenhuma
      ação manual do usuário
- [ ] Login bem-sucedido de uma conta cujo seed já foi aplicado não cria
      categorias duplicadas
- [ ] Criação concorrente (trigger + login, ou múltiplos logins em
      paralelo) nunca resulta em categorias padrão duplicadas para a
      mesma conta
- [ ] `PUT /categories/{id}` de uma categoria padrão atualiza
      normalmente (200), sem restrição adicional
- [ ] `DELETE /categories/{id}` de uma categoria padrão sem transações
      associadas exclui normalmente (204) e não é recriada num login
      seguinte
- [ ] `DELETE /categories/{id}` de uma categoria padrão com transações
      associadas retorna 422, mesma regra já existente
- [ ] O `id` de cada categoria padrão é sempre o GUID fixo listado nesta
      spec, igual em qualquer ambiente
- [ ] Falha transitória no seed não impede confirmação de cadastro nem
      login
- [ ] Nenhum contrato de API existente muda —
      `backend/docs/openapi.json` regenerado sem diffs de contrato

## Fora do escopo

- Subcategorias / hierarquia de categorias (`parentId`) — os itens
  internos de cada grupo da lista original (ex.: "Aluguel/Financiamento"
  dentro de "Moradia") não viram categoria nem qualquer outro dado
  persistido nesta feature; decisão fechada com o usuário nesta spec
- Categorias padrão de receita (ex.: "Salário") — nenhuma é criada por
  esta feature
- Proteção contra edição/exclusão de categoria padrão (ex.: flag
  `isPadrao`) — usuário tem controle total, igual a qualquer categoria
  criada manualmente
- Sincronização contínua entre o catálogo de categorias padrão e as já
  seedadas — renomear/excluir uma categoria padrão não é revertido nem
  recriado depois
- Backfill/retroativo para `Account` já existente antes do deploy desta
  feature
- Qualquer endpoint novo (ex.: "restaurar categorias padrão") — fica
  para uma feature própria, se necessário no futuro
- Catálogo de ícones/cores para categoria — removido na FEAT-21, sem
  reintrodução aqui
