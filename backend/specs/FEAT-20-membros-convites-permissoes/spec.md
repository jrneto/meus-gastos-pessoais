# FEAT-20: Membros da conta, convites e permissões

## Objetivo

Permitir que o `Titular` de uma `Account` (FEAT-19) convide outras
pessoas para participar da mesma conta por e-mail, atribuindo um nível
de acesso a cada uma (`Leitura`, `Lancar` ou `Total`), gerencie esses
membros (consulta, troca de nível, remoção) e passe a aplicar esse
nível de acesso como autorização em todos os endpoints já existentes de
despesas e categorias.

## Contexto

A FEAT-19 já criou `Account`/`Membership` e resolve automaticamente a
conta de todo usuário autenticado (`AccountPointer`, ver
`backend/specs/FEAT-19-conta-multi-tenant/plan.md`), mas só existe o
papel fixo `Titular` — nenhum endpoint de gerenciamento de membros
existe, e nenhum endpoint hoje verifica papel algum (qualquer usuário
autenticado com conta resolvida tem acesso total à própria conta).
`Membership` já nasceu com `GSI1PK=USER#<userId>` propositalmente
preparado por essa feature anterior para o access pattern "quais contas
esse usuário integra", que esta feature passa a usar.

Segue `backend/docs/roadmap.md` (item "FEAT-20 — Membros da conta,
convites e permissões") e o mockup do design system
(`frontend/design-system/jrnexpenses-web.dc.html`, tela "Membros da
conta"), que é a referência dos 3 níveis de acesso e do texto exibido
para cada um:
- **Leitura**: "Pode visualizar despesas e relatórios, sem editar
  nada."
- **Lançar** (`Lancar`): "Pode visualizar e lançar novas despesas."
- **Total**: "Pode visualizar, lançar despesas e criar categorias e
  orçamentos. Não pode gerenciar outros membros."
- **Titular** (papel fixo, não atribuível por convite): "Acesso total ·
  gerencia membros."

**Decisões de escopo fechadas nesta spec** (revisão com o usuário antes
de detalhar contratos):

1. **Convite por e-mail, sem envio real de e-mail** — não existe
   infraestrutura de e-mail no projeto ainda (SES é escopo da FEAT-26,
   fora desta leva). "Convidar" aqui significa criar um `Membership`
   com `Status=ConvitePendente` vinculado a um e-mail; a aceitação é
   automática e implícita no primeiro login bem-sucedido de um usuário
   cujo e-mail (claim do JWT) bate com um convite pendente — mesmo
   padrão de resolução "melhor esforço no login" já usado pela FEAT-19,
   sem nenhuma tela de "aceitar convite".
2. **Aceitar um convite troca a conta ativa do usuário convidado.**
   Um usuário sempre tem sua própria `Account` pessoal (criada
   automaticamente pela FEAT-19 na confirmação do cadastro). Ao aceitar
   um convite no login, a resolução de conta (`AccountPointer`) passa a
   apontar para a conta convidada — não existe hoje seletor de conta no
   frontend, então só uma conta pode estar "ativa" por vez. A conta
   pessoal do usuário não é apagada nem perde dados, só deixa de ser a
   conta ativa até uma eventual feature futura de troca de conta (ver
   "Fora do escopo").
3. **Inclui troca de nível de acesso de um membro já existente**
   (`PUT /members/{id}`), além do `GET/POST/DELETE` citados no roadmap
   — o mockup do design system mostra essa troca (radio inline na lista
   de membros) sem exigir remover e reconvidar.
4. **Matriz de permissão por papel nos endpoints existentes**: `Lancar`
   só cria despesas novas (`POST /expenses`), sem editar/excluir
   nenhuma despesa — não é rastreada autoria de despesa nesta feature
   (isso só chega na FEAT-22, que introduz `createdByUserId`). Detalhe
   completo na seção "Autorização por papel nos endpoints existentes".
5. **Somente o `Titular` gerencia membros** (convida, troca papel,
   remove) — inclusive o papel `Total`, apesar do nome, não gerencia
   membros (texto do mockup: "Não pode gerenciar outros membros").
   `GET /members` (consulta) é permitido a qualquer membro da conta,
   independente do papel — é informação de baixo risco (quem faz parte
   da conta) e não uma ação de gerenciamento.

## Requisitos de negócio

- Um convite é sempre `email` + `role` (`Leitura`, `Lancar` ou `Total`
  — `Titular` nunca é atribuível por convite, é papel exclusivo de quem
  criou a conta)
- Não é necessário que o e-mail convidado já tenha uma conta Cognito
  registrada no momento do convite — a aceitação só ocorre quando (e
  se) alguém fizer login com exatamente aquele e-mail
- Não é permitido convidar um e-mail que já é membro da mesma conta
  (papel `ConvitePendente` ou `Ativo`, incluindo o próprio Titular) —
  novo convite pro mesmo e-mail é rejeitado até o existente ser removido
- `userId` de quem chama qualquer endpoint de `/members` é sempre
  extraído do JWT, nunca do body — a autorização (é Titular? qual é o
  papel?) é sempre resolvida a partir da `Membership` do chamador na
  conta ativa, nunca informada pelo cliente
- O `Titular` de uma conta não pode ter seu papel alterado nem ser
  removido por ninguém (nem por si mesmo) — é sempre o único
  `Titular` da conta enquanto ela existir
- Somente o `Titular` pode convidar, trocar o papel de um membro ou
  remover um membro; qualquer outro papel que tente uma dessas três
  ações recebe 403, sem alterar nada
- Qualquer membro autenticado da conta (qualquer papel, incluindo
  `Leitura`) pode consultar `GET /members`
- No login, se o e-mail do usuário (claim do JWT) corresponder a um ou
  mais convites com `Status=ConvitePendente`, cada um desses convites
  passa a `Status=Ativo` nesse momento — sem exigir nenhuma ação
  explícita do usuário. Se houver mais de um convite pendente para o
  mesmo e-mail (contas diferentes), todos são aceitos nesse mesmo
  login, e a conta ativa resultante é a do convite mais recente
  (`createdAt` mais alto)
- Login sem nenhum convite pendente para o e-mail do usuário não altera
  a conta ativa (comportamento da FEAT-19 preservado sem mudança)
- Falha transitória ao processar a aceitação de convites durante o
  login nunca impede o login em si (mesma garantia já dada pela FEAT-19
  para a criação de conta)
- Autorização por papel se aplica a toda operação de escrita em
  `/categories` e `/expenses` já existentes (ver matriz na seção
  "Autorização por papel nos endpoints existentes"); leitura (`GET`)
  nesses dois recursos continua liberada para qualquer papel, inclusive
  `Leitura`

## User Stories

**US1 — Titular convida um novo membro**
- Given um usuário autenticado como `Titular` da conta ativa
- When ele envia `POST /members` com `email` e `role` válidos, para um
  e-mail que ainda não é membro da conta
- Then um `Membership` é criado com `Status=ConvitePendente` para esse
  e-mail e papel, e a API retorna 201 com os dados do convite

**US2 — Impede convite duplicado**
- Given uma conta em que um e-mail já é membro (pendente ou ativo)
- When o `Titular` tenta convidar esse mesmo e-mail novamente
- Then a API retorna 409 e nenhum novo `Membership` é criado

**US3 — Impede convite por quem não é Titular**
- Given um usuário autenticado com papel `Leitura`, `Lancar` ou `Total`
  na conta ativa
- When ele tenta `POST /members`
- Then a API retorna 403 e nenhum convite é criado

**US4 — Qualquer membro consulta a lista de membros**
- Given um usuário autenticado com qualquer papel na conta ativa
  (incluindo `Leitura`)
- When ele chama `GET /members`
- Then a API retorna todos os membros da conta (incluindo o `Titular`),
  com e-mail, papel, status (`ConvitePendente`/`Ativo`) e data de
  criação de cada um

**US5 — Titular troca o papel de um membro**
- Given um usuário autenticado como `Titular`, com um membro (pendente
  ou ativo) na conta com papel `Leitura`
- When ele envia `PUT /members/{id}` com `role: "Total"`
- Then o papel desse membro é atualizado e a API retorna 200 com os
  dados atualizados — o `Status` (`ConvitePendente`/`Ativo`) do membro
  não muda só por isso

**US6 — Impede troca de papel por quem não é Titular**
- Given um usuário autenticado com papel `Lancar` ou `Total`
- When ele tenta `PUT /members/{id}`
- Then a API retorna 403 e nenhum papel é alterado

**US7 — Impede alterar o papel do Titular**
- Given um usuário autenticado como `Titular`
- When ele tenta `PUT /members/{id}` no `id` correspondente ao próprio
  `Titular` da conta
- Then a API retorna 422 e o papel do Titular não muda

**US8 — Titular remove um membro**
- Given um usuário autenticado como `Titular`, com um membro (pendente
  ou ativo) cadastrado
- When ele envia `DELETE /members/{id}` para esse membro
- Then o `Membership` é removido e a API retorna 204 — se o convite
  ainda estava `ConvitePendente`, ele deixa de poder ser aceito em
  login algum (o e-mail deixa de ter qualquer vínculo com essa conta)

**US9 — Impede remoção por quem não é Titular**
- Given um usuário autenticado com papel `Leitura`, `Lancar` ou `Total`
- When ele tenta `DELETE /members/{id}`
- Then a API retorna 403 e nenhum membro é removido

**US10 — Impede remover o Titular**
- Given um usuário autenticado como `Titular`
- When ele tenta `DELETE /members/{id}` no `id` correspondente a si
  mesmo
- Then a API retorna 422 e o Titular não é removido

**US11 — Editar/remover membro inexistente ou de outra conta**
- Given um usuário autenticado como `Titular`
- When ele tenta `PUT`/`DELETE /members/{id}` com um `id` que não
  existe, ou que pertence a outra conta
- Then a API retorna 404 e nenhuma alteração é feita

**US12 — Convite aceito automaticamente no login**
- Given um convite `Status=ConvitePendente` para o e-mail
  `pessoa@email.com` em uma conta, e um usuário Cognito confirmado com
  exatamente esse e-mail
- When esse usuário faz `POST /auth/login` com sucesso
- Then o `Membership` correspondente passa a `Status=Ativo`, a conta
  ativa desse usuário passa a ser a conta do convite, e a resposta de
  login continua no mesmo formato de hoje

**US13 — Login sem convite pendente não muda a conta ativa**
- Given um usuário autenticado sem nenhum convite pendente para o
  próprio e-mail
- When ele faz login com sucesso
- Then a conta ativa dele permanece a mesma de antes (comportamento já
  garantido pela FEAT-19)

**US14 — Múltiplos convites pendentes no mesmo login**
- Given um usuário com convites `ConvitePendente` em duas contas
  diferentes para o mesmo e-mail, criados em momentos diferentes
- When ele faz login com sucesso
- Then ambos os convites passam a `Status=Ativo`, e a conta ativa
  resultante é a do convite mais recente

**US15 — Papel `Leitura` só consulta**
- Given um usuário autenticado com papel `Leitura` na conta ativa
- When ele chama qualquer `GET` de `/categories` ou `/expenses`
- Then a API responde normalmente (200), e qualquer `POST`/`PUT`/
  `DELETE` nesses recursos retorna 403 sem alterar nada

**US16 — Papel `Lancar` só cria despesa nova**
- Given um usuário autenticado com papel `Lancar` na conta ativa
- When ele chama `GET` (qualquer recurso) ou `POST /expenses`
- Then a API responde normalmente; `PUT`/`DELETE /expenses` e qualquer
  escrita em `/categories` retornam 403 sem alterar nada

**US17 — Papel `Total` gerencia despesas e categorias, não membros**
- Given um usuário autenticado com papel `Total` na conta ativa
- When ele chama qualquer operação de `/categories` ou `/expenses`
- Then a API responde normalmente; qualquer `POST`/`PUT`/`DELETE` em
  `/members` retorna 403 sem alterar nada

**US18 — Titular mantém acesso total**
- Given um usuário autenticado como `Titular`
- When ele chama qualquer operação já existente em `/categories`,
  `/expenses`, além de `/members`
- Then a API responde normalmente para todas — nenhuma restrição de
  papel se aplica ao Titular

## Contratos da API

### GET /members

Retorna todos os membros da conta ativa do chamador (qualquer papel
pode consultar).

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
      "email": "convidado@email.com",
      "role": "Leitura",
      "status": "ConvitePendente",
      "createdAt": "2025-06-16T09:00:00Z"
    }
  ]
}
```

### POST /members

Request:
```json
{
  "email": "convidado@email.com",
  "role": "Leitura"
}
```
`role`: `"Leitura"` | `"Lancar"` | `"Total"` (obrigatório; `"Titular"`
nunca é aceito aqui).

Response 201 (Location: /members/{id}):
```json
{
  "id": "...",
  "email": "convidado@email.com",
  "role": "Leitura",
  "status": "ConvitePendente",
  "createdAt": "2025-06-16T09:00:00Z"
}
```

Response 400 (validation-error): `email` ausente/inválido, `role`
ausente ou fora de `Leitura`/`Lancar`/`Total`.
Response 403 (insufficient-permission): quem chama não é Titular.
Response 409 (member-already-exists): e-mail já é membro (pendente ou
ativo) desta conta.

### PUT /members/{id}

Request:
```json
{
  "role": "Total"
}
```

Response 200: dados atualizados do membro (mesmo formato do `POST`).
Response 400 (validation-error): `role` ausente ou fora de
`Leitura`/`Lancar`/`Total`.
Response 403 (insufficient-permission): quem chama não é Titular.
Response 404 (not-found): `id` não existe nesta conta.
Response 422 (cannot-modify-titular): tentativa de alterar o papel do
Titular.

### DELETE /members/{id}

Response 204: membro removido (pendente ou ativo).
Response 403 (insufficient-permission): quem chama não é Titular.
Response 404 (not-found): `id` não existe nesta conta.
Response 422 (cannot-remove-titular): tentativa de remover o Titular.

### Autorização por papel nos endpoints existentes

Toda ação abaixo já existe (FEAT-04/06/07/08/16/17); o que muda nesta
feature é a checagem de papel antes de executá-la. Nenhum request/
response desses endpoints muda de formato.

| Endpoint | Leitura | Lancar | Total | Titular |
|---|:-:|:-:|:-:|:-:|
| `GET /categories`, `GET /categories/{id}` | ✅ | ✅ | ✅ | ✅ |
| `POST`/`PUT`/`DELETE /categories` | 403 | 403 | ✅ | ✅ |
| `GET /expenses`, `GET /expenses/{id}` | ✅ | ✅ | ✅ | ✅ |
| `POST /expenses` | 403 | ✅ | ✅ | ✅ |
| `PUT`/`DELETE /expenses` | 403 | 403 | ✅ | ✅ |
| `GET /members` | ✅ | ✅ | ✅ | ✅ |
| `POST`/`PUT`/`DELETE /members` | 403 | 403 | 403 | ✅ |

Toda célula "403" retorna o mesmo formato abaixo, aplicável a qualquer
rota desta tabela e às três de `/members`:
```json
{
  "type": "https://gastosapp.dev/errors/insufficient-permission",
  "title": "Acesso negado",
  "status": 403,
  "detail": "Seu nível de acesso não permite esta ação."
}
```

### Erros comuns a todas as rotas

Formato padrão de erro do projeto (`ResultHttpExtensions.BuildProblem`):
`title` fixo e genérico por tipo de erro (RFC 9457), mensagem
específica sempre em `detail` (exceto `Failure`/500, que nunca preenche
`detail`). Fonte de verdade exata:
`backend/docs/openapi.json`.

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Papel de acesso inválido."
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

Response 404 (not-found):
```json
{
  "type": "https://gastosapp.dev/errors/not-found",
  "title": "Recurso não encontrado",
  "status": 404,
  "detail": "Membro não encontrado."
}
```

Response 409 (member-already-exists):
```json
{
  "type": "https://gastosapp.dev/errors/member-already-exists",
  "title": "Conflito",
  "status": 409,
  "detail": "Este e-mail já é membro desta conta."
}
```

Response 422 (cannot-remove-titular / cannot-modify-titular):
```json
{
  "type": "https://gastosapp.dev/errors/cannot-remove-titular",
  "title": "Regra de negócio violada",
  "status": 422,
  "detail": "O Titular da conta não pode ser removido."
}
```

## Critérios de aceite

- [x] `POST /members` com `email`/`role` válidos, chamado pelo Titular,
      cria convite `ConvitePendente` e retorna 201
- [x] `POST /members` para e-mail já membro (pendente ou ativo) da
      mesma conta retorna 409
- [x] `POST /members` com campo obrigatório ausente/inválido retorna 400
- [x] `POST /members` chamado por qualquer papel que não seja Titular
      retorna 403
- [x] `GET /members` retorna todos os membros da conta (incluindo o
      Titular) para qualquer papel autenticado
- [x] `PUT /members/{id}` altera o papel de um membro existente e
      retorna 200, quando chamado pelo Titular
- [x] `PUT /members/{id}` chamado por quem não é Titular retorna 403
- [x] `PUT /members/{id}` para o próprio Titular retorna 422
- [x] `DELETE /members/{id}` remove um membro (pendente ou ativo) e
      retorna 204, quando chamado pelo Titular
- [x] `DELETE /members/{id}` chamado por quem não é Titular retorna 403
- [x] `DELETE /members/{id}` para o próprio Titular retorna 422
- [x] `PUT`/`DELETE /members/{id}` com `id` inexistente ou de outra
      conta retorna 404
- [x] Login de um usuário cujo e-mail bate com convite(s)
      `ConvitePendente` aceita todos esses convites (`Status=Ativo`) e
      troca a conta ativa para o convite mais recente
- [x] Login sem convite pendente para o e-mail do usuário não altera a
      conta ativa (comportamento da FEAT-19 preservado)
- [x] Falha transitória ao aceitar convites no login nunca impede o
      login
- [x] `Leitura` só executa `GET` em `/categories`/`/expenses`; qualquer
      escrita retorna 403
- [x] `Lancar` executa `GET` e `POST /expenses`; `PUT`/`DELETE
      /expenses` e qualquer escrita em `/categories` retornam 403
- [x] `Total` executa toda operação de `/categories`/`/expenses`; toda
      escrita em `/members` retorna 403
- [x] `Titular` executa toda operação de `/categories`, `/expenses` e
      `/members` sem restrição
- [x] Todas as rotas sem token continuam retornando 401
- [x] `backend/docs/openapi.json` regenerado refletindo os 3 endpoints
      novos e os novos status 403/409/422 nos endpoints já existentes

## Status

Implementado conforme `plan.md`/`tasks.md`. `Membership` (Domain)
reformado com `Id`/`Email`/`Status` e os papéis
`Leitura`/`Lancar`/`Total` (além de `Titular`), mantendo `SK` estável
(`MEMBER#<membershipId>`) desde a criação — inclusive pro Titular —
pra sobreviver à transição pendente→ativo sem invalidar nenhum `id` já
em uso. Novo `IMembershipRepository`/`DynamoDbMembershipRepository`
(`GastosApp.Infrastructure/Members/`), separado de `IAccountRepository`
(que ganhou `SetActiveAccountAsync`, além de `CreateAsync` passar a
exigir `email`). `ResolveAccountIdQuery` (FEAT-19) substituída por
`ResolveMembershipQuery`, que resolve `AccountId`+`MembershipId`+`Role`
numa única chamada, usada por `ResolveAccountEndpointFilter` (que
também popula `CurrentAccountContext.Role`). Novo
`RoleEndpointFilters.Require(...)` (delegate factory) aplicado nas
rotas de escrita de `/categories`, `/expenses` e nas três de
gerenciamento de `/members`. Novos Commands/Queries em
`GastosApp.Application/Members/`: `InviteMemberCommand`,
`GetMembersQuery`, `UpdateMemberRoleCommand`, `RemoveMemberCommand`,
`AcceptPendingInvitesCommand` (despachado no login, depois de
`EnsureAccountCommand`, ambos best-effort). Novo `ErrorType.Forbidden`
mapeado para 403 em `ResultHttpExtensions`. `GastosApp.CognitoTriggers`
(`AccountTriggerHandler`) atualizado pra extrair `email` do evento
Cognito, exigido pelo novo `EnsureAccountCommand`.

Aceitação de convite no login usa `GSI1` como índice dual-purpose
(`GSI1PK=USER#<userId>` pra membros ativos, `GSI1PK=EMAIL#<email>` pra
convites pendentes) — reaproveitando o índice já provisionado pela
FEAT-19, sem nenhum recurso AWS novo. A troca de `Status`/`UserId`/
`GSI1PK` na aceitação é um `UpdateItem` simples (sem
`TransactWriteItems`), possível justamente porque o `SK` nunca muda.

`backend/docs/openapi.json` regenerado localmente (API rodando contra
`local-init.sh`/LocalStack/cognito-local) — `git diff` confirma só
adições (3 endpoints novos de `/members` + novos status/schemas nos
endpoints já existentes), sem remoção de contrato.

Suíte completa (`dotnet test` na solução) passa: 409/409 (1
IntegrationTests placeholder + 139 ComponentTests + 269 UnitTests).

## Fora do escopo

- Envio real de e-mail de convite (SES ou similar) — infraestrutura
  ainda inexistente, escopo da FEAT-26; o convidado só fica sabendo do
  convite por fora da aplicação (ex.: combinado verbalmente)
- Seletor/troca manual entre múltiplas contas de um mesmo usuário —
  hoje a troca de conta ativa só acontece como efeito colateral de
  aceitar um convite no login; navegar entre contas já pertencentes
  (sem novo convite) fica para uma feature futura
- Rastrear quem lançou cada despesa (`createdByUserId`) — introduzido
  na FEAT-22; por isso `Lancar` não pode editar/excluir nem a própria
  despesa nesta feature
- Reenvio de convite / expiração de convite pendente — um convite
  pendente vale indefinidamente até ser aceito no login ou removido via
  `DELETE /members/{id}`
- Orçamento por categoria e campo `tipo` (`despesa`/`receita`) — FEAT-21
- Qualquer mudança em `POST`/`GET/PUT/DELETE /auth/*` além do efeito
  colateral de aceitar convites pendentes no login
