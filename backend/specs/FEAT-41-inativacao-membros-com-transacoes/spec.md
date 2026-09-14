# FEAT-41: Inativação de membros com transações lançadas

## Objetivo

Impedir que `DELETE /members/{id}` apague de vez o vínculo de um membro
que já lançou transações na conta. Em vez de remover o `Membership`
incondicionalmente, um membro `Ativo` com pelo menos uma transação
lançada passa a `Status=Inativo` — perde acesso à conta como se tivesse
sido removido, mas o registro permanece, preservando o histórico:
`createdByLabel` das transações que ele criou continua mostrando o
e-mail dele em vez de cair no fallback genérico `"Ex-membro"`.

## Contexto

Débito técnico registrado durante a revisão do `plan.md` da FEAT-22
(`backend/specs/FEAT-22-transacoes-receita-despesa/spec.md`, seção
"Fora do escopo") e mantido em `backend/docs/backlog.md`. Hoje
(FEAT-20/FEAT-22), `DELETE /members/{id}` sempre remove o `Membership`
de fato — pendente ou ativo, com ou sem transações associadas — e
`CreatedByLabelResolver` cai em `"Ex-membro"` sempre que a `Membership`
do autor de uma transação não existe mais. Essa feature fecha esse
débito sem alterar nada do que já funciona para um membro sem nenhuma
transação lançada (continua sendo removido de fato, do mesmo jeito que
hoje).

**Decisões de escopo fechadas nesta spec (revisão com o usuário antes
de detalhar os contratos):**

1. **A inativação só se aplica a um membro `Ativo` com pelo menos uma
   transação lançada por ele na conta.** Um membro `Ativo` sem nenhuma
   transação, e qualquer membro `ConvitePendente` (nunca fez login,
   logo nunca lançou nada), continuam sendo removidos de fato
   (`Membership` apagado) — comportamento inalterado.
2. **`GET /members` sempre lista membros `Inativo`** junto com
   `Ativo`/`ConvitePendente`, sem filtro novo — dá visibilidade de quem
   já fez parte da conta.
3. **Convidar de novo o mesmo e-mail depois que ele virou `Inativo` é
   permitido.** `Inativo` não conta como "já é membro" para a checagem
   de duplicidade de `POST /members` (que continua bloqueando
   `ConvitePendente`/`Ativo`) — do contrário, esse e-mail ficaria
   trancado na conta para sempre, já que um `Inativo` nunca pode ser
   removido de fato. Um novo convite cria um `Membership` novo
   (`id` diferente), que coexiste com o registro `Inativo` antigo.
4. **`PUT`/`DELETE /members/{id}` sobre um membro já `Inativo` retornam
   422** (mesmo padrão de erro já usado para o Titular), em vez de 404
   ou de uma operação silenciosamente sem efeito — sinaliza que a ação
   não se aplica a um membro inativo. Não existe, nesta feature,
   nenhuma ação que reative um `Inativo` (reativação só acontece,
   indiretamente, por um novo convite — item 3).
5. **Um membro inativado perde acesso à conta imediatamente**, mesmo
   com um JWT ainda válido — qualquer chamada autenticada dele contra
   essa conta a partir da inativação se comporta exatamente como já
   acontece hoje para um membro removido de fato (401
   `account-not-found`, comportamento pré-existente de
   `ResolveMembershipQuery`, sem mudança nesta feature).

## Requisitos de negócio

- A inativação (em vez de remoção de fato) só se aplica quando **todas**
  as condições abaixo são verdadeiras: o membro alvo de
  `DELETE /members/{id}` tem `Status=Ativo` **e** existe pelo menos uma
  transação na conta cujo `createdByUserId` é o `userId` desse membro.
  Fora isso (`ConvitePendente`, ou `Ativo` sem nenhuma transação), a
  remoção continua de fato, sem deixar rastro.
- A inativação preserva `email` e `role` do membro (histórico) — só
  `Status` muda para `Inativo`. Nenhuma checagem de autorização volta a
  conceder acesso a partir do `role` armazenado depois disso.
- Só o `Titular` pode inativar/remover um membro — mesma regra de
  autorização já existente (403 para qualquer outro papel), sem mudança.
- O `Titular` nunca pode ser inativado nem removido — regra já existente
  (422 `cannot-remove-titular`), sem mudança.
- `createdByLabel` (derivado, nunca persistido) de uma transação lançada
  por um membro hoje `Inativo` resolve para o e-mail desse membro, do
  mesmo jeito que resolveria se ele continuasse `Ativo` — o fallback
  `"Ex-membro"` só se aplica quando não existe `Membership` alguma para
  aquele `createdByUserId` (situação que deixa de ocorrer para toda
  inativação feita a partir desta feature; transações de membros já
  removidos de fato **antes** desta feature entrar em produção
  continuam caindo em `"Ex-membro"` para sempre — não há reconstituição
  retroativa de um `Membership` que já foi apagado).
- `POST /members` (convite) não é bloqueado por um `Inativo` existente
  com o mesmo e-mail — a checagem de duplicidade (409
  `member-already-exists`) continua valendo só para `ConvitePendente`/
  `Ativo`. Um novo convite aceito cria um `Membership` novo, distinto do
  `Inativo` antigo (que permanece intacto, preservando o
  `createdByLabel` das transações já lançadas por ele).
- `PUT /members/{id}` (trocar papel) sobre um membro `Inativo` retorna
  422 (`cannot-modify-inactive-member`) e não altera nada.
- `DELETE /members/{id}` sobre um membro já `Inativo` retorna 422
  (`member-already-inactive`) e não altera nada.
- `GET /members` inclui membros `Inativo` na listagem, com
  `status: "Inativo"`, sem necessidade de nenhum filtro adicional.

## User Stories

**US1 — Remover membro sem nenhuma transação continua removendo de fato**
- Given um usuário autenticado como `Titular`, com um membro `Ativo`
  que nunca lançou nenhuma transação na conta
- When ele envia `DELETE /members/{id}` para esse membro
- Then o `Membership` é removido de fato e a API retorna 204 (mesmo
  comportamento de hoje)

**US2 — Remover convite pendente continua removendo de fato**
- Given um usuário autenticado como `Titular`, com um membro
  `ConvitePendente`
- When ele envia `DELETE /members/{id}` para esse convite
- Then o `Membership` é removido de fato e a API retorna 204 (mesmo
  comportamento de hoje)

**US3 — Remover membro com transações lançadas inativa em vez de apagar**
- Given um usuário autenticado como `Titular`, com um membro `Ativo`
  que já lançou pelo menos uma transação na conta
- When ele envia `DELETE /members/{id}` para esse membro
- Then a API retorna 204, o `Membership` passa a `Status=Inativo` (não
  é apagado), e esse membro deixa de ter qualquer acesso à conta a
  partir desse momento

**US4 — Membro inativado some da autorização, não do histórico**
- Given um membro que acabou de ser inativado (US3)
- When ele tenta chamar qualquer endpoint autenticado da conta (ex.:
  `GET /transactions`) com o mesmo JWT de antes
- Then a API retorna 401 (`account-not-found`), do mesmo jeito que já
  aconteceria hoje para um membro removido de fato

**US5 — createdByLabel de um membro inativado mostra o e-mail dele**
- Given uma transação lançada por um membro que depois foi inativado
  (US3)
- When outro membro da conta consulta essa transação (`GET
  /transactions/{id}` ou `GET /transactions`)
- Then `createdByLabel` mostra o e-mail do membro inativado, **não**
  `"Ex-membro"`

**US6 — GET /members lista membros inativos**
- Given uma conta com um membro `Ativo`, um `ConvitePendente` e um
  `Inativo`
- When qualquer membro autenticado da conta chama `GET /members`
- Then a resposta inclui os três, cada um com seu `status` correto
  (`Ativo`, `ConvitePendente`, `Inativo`)

**US7 — Reconvidar o e-mail de um membro inativado é permitido**
- Given um membro `Inativo` com e-mail `pessoa@email.com`
- When o `Titular` envia `POST /members` com `email: "pessoa@email.com"`
  e um `role` válido
- Then a API retorna 201 com um novo `Membership`
  (`Status=ConvitePendente`), e o registro `Inativo` anterior permanece
  intacto

**US8 — Impede trocar o papel de um membro inativo**
- Given um membro `Inativo`
- When o `Titular` envia `PUT /members/{id}` para esse membro
- Then a API retorna 422 (`cannot-modify-inactive-member`) e o
  `Membership` não é alterado

**US9 — Impede "remover" de novo um membro já inativo**
- Given um membro `Inativo`
- When o `Titular` envia `DELETE /members/{id}` para esse membro
- Then a API retorna 422 (`member-already-inactive`) e o `Membership`
  não é alterado

**US10 — Autorização por papel continua valendo para as ações desta feature**
- Given um usuário autenticado com papel `Leitura`, `Lancar` ou `Total`
- When ele tenta `DELETE /members/{id}` de um membro com transações
  lançadas
- Then a API retorna 403 e nenhum `Membership` é alterado (mesma regra
  já existente da FEAT-20, sem mudança)

## Contratos da API

### GET /members

Sem mudança de formato — `status` passa a poder ser também `"Inativo"`,
sempre incluído na listagem (sem filtro novo).

Response 200:
```json
{
  "items": [
    {
      "id": "...",
      "email": "titular@email.com",
      "role": "Titular",
      "status": "Ativo",
      "createdAt": "2025-06-15T12:34:56Z"
    },
    {
      "id": "...",
      "email": "ex-colaborador@email.com",
      "role": "Lancar",
      "status": "Inativo",
      "createdAt": "2025-05-01T09:00:00Z"
    }
  ]
}
```

### POST /members

Sem mudança de request/response (ver `backend/specs/FEAT-20-membros-convites-permissoes/spec.md`).
Muda só a regra por trás do 409: `Inativo` não conta como "já é membro"
para essa checagem — só `ConvitePendente`/`Ativo` bloqueiam.

### PUT /members/{id}

Sem mudança de request/response para os casos já existentes. Novo caso:

Response 422 (cannot-modify-inactive-member): membro alvo está
`Inativo`.
```json
{
  "type": "https://gastosapp.dev/errors/cannot-modify-inactive-member",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Não é possível alterar o papel de um membro inativo."
}
```

### DELETE /members/{id}

Response 204: membro removido de fato (`ConvitePendente`, ou `Ativo`
sem nenhuma transação lançada) **ou** membro `Ativo` com transações
lançadas passou a `Status=Inativo` — nos dois casos, sem corpo de
resposta, sem diferença observável na resposta em si.

Response 403 (insufficient-permission): quem chama não é Titular
(inalterado).
Response 404 (not-found): `id` não existe nesta conta (inalterado).
Response 422 (cannot-remove-titular): tentativa de remover o Titular
(inalterado).

Novo caso:

Response 422 (member-already-inactive): membro alvo já está `Inativo`.
```json
{
  "type": "https://gastosapp.dev/errors/member-already-inactive",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "Este membro já está inativo."
}
```

### Erros comuns a todas as rotas

Formato padrão de erro do projeto (`ResultHttpExtensions.BuildProblem`):
`title` fixo e genérico por tipo de erro (RFC 9457), mensagem
específica sempre em `detail`. Fonte de verdade exata:
`backend/docs/openapi.json`.

## Critérios de aceite

- [x] `DELETE /members/{id}` de um membro `Ativo` sem nenhuma transação
      lançada continua removendo o `Membership` de fato e retornando 204
- [x] `DELETE /members/{id}` de um membro `ConvitePendente` continua
      removendo de fato e retornando 204
- [x] `DELETE /members/{id}` de um membro `Ativo` com pelo menos uma
      transação lançada retorna 204, mas transforma o `Membership` em
      `Status=Inativo` em vez de apagá-lo
- [x] Um membro recém-inativado perde acesso à conta: qualquer chamada
      autenticada dele contra a conta retorna 401 a partir da inativação
- [x] `createdByLabel` de transações lançadas por um membro inativado
      mostra o e-mail dele, não `"Ex-membro"`
- [x] `GET /members` inclui membros `Inativo` na listagem, sem filtro
      adicional necessário
- [x] `POST /members` para o e-mail de um membro `Inativo` é aceito
      (201), criando um `Membership` novo (`ConvitePendente`) que
      coexiste com o `Inativo` antigo
- [x] `PUT /members/{id}` sobre um membro `Inativo` retorna 422
      (`cannot-modify-inactive-member`) e não altera nada
- [x] `DELETE /members/{id}` sobre um membro já `Inativo` retorna 422
      (`member-already-inactive`) e não altera nada
- [x] Autorização por papel (403 para quem não é Titular) continua
      valendo para `DELETE /members/{id}` de um membro com transações
      lançadas
- [x] `DELETE /members/{id}` do próprio Titular continua retornando 422
      (`cannot-remove-titular`), inclusive quando o Titular tem
      transações lançadas
- [x] `backend/docs/openapi.json` regenerado refletindo o novo valor
      `"Inativo"` de `status` em `/members` e os dois novos 422
      (`cannot-modify-inactive-member`, `member-already-inactive`) —
      diff confirmado vazio (nenhum campo/status code novo no schema,
      só valores livres de `string`, ver `plan.md`)

## Status

Implementado conforme `plan.md`/`tasks.md`. `MembershipStatus` (Domain)
ganhou `Inativo`, sem nenhum factory method novo — a transição
`Ativo → Inativo` é uma mutação de atributo feita direto pelo
repositório (`DynamoDbMembershipRepository.InactivateAsync`, `UpdateItem`
condicionado a `Status=Ativo`), mesmo padrão já usado por
`AcceptPendingInvitesByEmailAsync` para `ConvitePendente → Ativo`.
`RemoveMemberCommandHandler` passou a depender também de
`ITransactionRepository` (novo `ExistsByCreatedByUserIdAsync`, `Query`
por `PK` + `FilterExpression CreatedByUserId`, sem GSI dedicado — ver
"Decisões técnicas" do `plan.md`) para decidir inativar em vez de
remover. `UpdateMemberRoleCommandHandler` bloqueia (422
`cannot-modify-inactive-member`) qualquer tentativa de trocar o papel de
um `Inativo`. `DynamoDbMembershipRepository.CreateInviteAsync` passou a
ignorar membros `Inativo` na checagem de duplicidade de convite.

`GSI1PK` de um membro inativado **permanece** `USER#<userId>` (não muda
na inativação) — quem passa a rejeitar esse membro é
`ResolveMembershipQueryHandler` (só resolve `Status=Ativo`, senão 401
`account-not-found`, mesmo erro que já existia para `Membership`
ausente). Como o índice não muda, `CreatedByLabelResolver` (Transações,
FEAT-22) encontra o `Membership` `Inativo` normalmente e resolve
`createdByLabel` para o e-mail dele, sem precisar de nenhuma mudança de
código — só o comentário do arquivo foi atualizado.

`MemberResult`/`GetMembersResult` não precisaram de nenhuma mudança:
`Status.ToString()` já expõe `"Inativo"` automaticamente, e
`GetMembersQueryHandler`/`ListAsync` já listavam todo item
`MEMBER#*` da partição sem filtro de status.

`backend/docs/openapi.json` regenerado localmente (API rodando contra
`local-init.sh`/LocalStack/cognito-local) — `git diff` confirma **zero
mudança**, como previsto no `plan.md` (nenhum campo novo, nenhum status
code novo — `status` de `MemberResult` já era `string` livre, e 422 já
era documentado em `PUT`/`DELETE /members/{id}` desde a FEAT-20).

Nenhum recurso AWS novo: a checagem "esse membro já lançou alguma
transação?" usa `Query` na partição já existente
(`PK=ACCOUNT#<accountId>`), sem GSI adicional.

Suíte completa (`dotnet test` na solução, fora de `Category=Integration`)
passa: 805/805 (565 UnitTests + 240 ComponentTests), sem quebrar nenhum
teste existente. Suíte de integração local (`run-local.sh`, binário
Native AOT via RIE) passa: 37/37, incluindo o novo fluxo completo de
`MembersFlowTests` (convidar → aceitar via login → lançar transação
como o membro → Titular remove → `Status=Inativo` → membro perde
acesso à conta).

## Fora do escopo

- Qualquer ação de reativação de um membro `Inativo` (ex.: um endpoint
  que volte `Status` para `Ativo` sobre o mesmo `Membership`) — a única
  forma de um e-mail voltar a ter acesso é um novo convite (`POST
  /members`), que cria um `Membership` novo e distinto (ver US7)
- Expor na API o motivo/momento da inativação (ex.: `inactivatedAt`) —
  fica só o `status="Inativo"`, sem metadado adicional
- Filtro de `GET /members` por `status` — a listagem sempre inclui
  todos os status, sem necessidade de filtro (decisão fechada nesta
  spec)
- Reconstituir o `createdByLabel` de transações cujo autor já havia
  sido removido de fato **antes** desta feature entrar em produção —
  essas continuam mostrando `"Ex-membro"` para sempre, pois o
  `Membership` original já não existe
- Qualquer mudança em `/categories` ou `/transactions` além da resolução
  de `createdByLabel` já descrita (nenhuma mudança de contrato nesses
  dois recursos)
- Papel do Titular, matriz de autorização por papel, ou qualquer outra
  regra da FEAT-20/FEAT-22 não mencionada explicitamente aqui
