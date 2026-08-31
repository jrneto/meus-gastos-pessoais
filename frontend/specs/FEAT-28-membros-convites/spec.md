# FEAT-28: Membros da conta e convites

## Objetivo

Nova tela "Membros da conta", acessível por um novo item de menu
("Membros"), consumindo os três endpoints novos do backend
(`GET`/`POST`/`PUT`/`DELETE /members`, backend FEAT-20, já em
produção): lista de quem faz parte da conta (com papel e status),
convite de uma nova pessoa por e-mail com nível de acesso, troca de
papel de um membro existente e remoção. Introduz também dois
componentes reutilizáveis que o design já assume em várias telas —
toast de confirmação e overlay de processamento de tela cheia —
resolvendo os dois débitos técnicos registrados no backlog.

## Contexto

Hoje não existe rota/item de menu para "Membros" — é uma feature nova
do zero (`features/members`). O backend já expõe tudo que ela precisa
(`backend/specs/FEAT-20-membros-convites-permissoes/spec.md`, já em
produção): `GET /members` (qualquer papel autenticado consulta),
`POST /members` (convite, só Titular), `PUT /members/{id}` (troca de
papel, só Titular) e `DELETE /members/{id}` (remoção, só Titular). Os
papéis são `Leitura`, `Lancar`, `Total` e `Titular` (fixo, não
atribuível por convite, sempre único por conta, nunca alterável nem
removível).

Referência visual: `frontend/design-system/web/screenshots/
14-membros.png` (lista), `15-convidar-pessoa.png` (popup de convite),
`16-enviando-convite-loading.png` (popup enviando) e
`17-toast-convite-enviado.png` (toast de sucesso); fonte de verdade
`frontend/design-system/web/jrnexpenses-web.dc.html` (bloco `isMem` e
o diálogo `showInviteDialog`). O design mostra: título "Membros da
conta" + botão "+ Convidar pessoa"; uma linha especial pro Titular
("Você (titular)" / "Acesso total · gerencia membros", tag "Titular");
uma linha por membro (e-mail, status "Convite pendente"/"Ativo", um
seletor Leitura/Lançar/Total que troca o papel direto, e um ícone de
remover); popup de convite (e-mail + seletor de papel com descrição de
cada nível) que, ao enviar, mostra um overlay "Enviando convite" sobre
o próprio popup e, ao concluir, fecha e mostra um toast "Convite
enviado para {email}.".

**Decisões de escopo fechadas com o usuário durante este `/specify`:**

1. **Ações de escrita escondidas pra quem não é Titular.** O backend
   libera `GET /members` pra qualquer papel, mas só o Titular pode
   convidar/trocar papel/remover (403 pra qualquer outro). O protótipo
   só modela a visão do Titular — esta feature já resolve isso, sem
   esperar a FEAT-29 (permissões por role): quando o usuário logado não
   é o Titular da conta, a tela mostra a lista somente leitura, sem o
   botão "+ Convidar pessoa", sem o seletor de papel (substituído por
   texto simples do papel de cada um) e sem o ícone de remover.
2. **Toast e overlay de processamento genéricos, implementados agora.**
   Ambos os débitos técnicos do backlog ("Componente de toast
   genérico" e "Overlay de processamento de tela cheia", já adiados nas
   FEAT-24 e FEAT-26) são resolvidos nesta feature, já que o popup de
   convite depende visualmente dos dois. Escopo restrito a esta
   feature: os dois componentes não são retroaplicados a telas já
   existentes (login, salvar despesa/receita) nesta mesma leva — isso
   fica pra quando essas telas forem revisitadas.
3. **Quem é "eu" na lista vem de `GET /auth/me`** (endpoint já
   implementado no client — `authApi.me()` —, hoje sem nenhum
   consumidor): a linha do Titular é identificada pelo `role` (`GET
   /members` sempre tem exatamente um item `Titular`), e "sou eu o
   Titular?" compara o e-mail desse item com o e-mail retornado por
   `GET /auth/me`. A própria linha do usuário logado (seja ele o
   Titular ou não) ganha um indicador "(você)" — pequena extensão do
   "Você (titular)" do design pro caso de quem não é Titular.
4. **Remover um membro exige confirmação**, mesmo o protótipo estático
   não mostrando isso — mesmo padrão já usado em Transações
   (`TransactionDeleteDialog`) e Categorias (`CategoryDeleteDialog`)
   pra qualquer ação destrutiva, e remover um membro revoga o acesso
   dele (convite pendente deixa de poder ser aceito; membro ativo perde
   acesso imediatamente).
5. **Troca de papel é direta, sem confirmação** — ao clicar numa opção
   do seletor Leitura/Lançar/Total de um membro, `PUT /members/{id}` é
   chamado imediatamente (mesmo comportamento do protótipo). Reversível
   (basta trocar de novo), diferente de remover.
6. **Papel padrão do convite: "Lançar"** — mesmo valor inicial do
   protótipo (`inviteRole: 'lancamento'`), meio-termo entre os dois
   extremos (`Leitura`/`Total`).

## Requisitos de negócio

- Ao carregar a tela, o frontend busca `GET /members` e `GET /auth/me`
  (decisão 3) em paralelo
- A linha do Titular é sempre destacada separadamente (não entra na
  lista comum de membros), com a tag "Titular" e a descrição "Acesso
  total · gerencia membros"; as demais linhas mostram e-mail e status
  (`Convite pendente` quando `status="ConvitePendente"`, `Ativo` quando
  `status="Ativo"`)
- Quando o usuário logado é o Titular: cada linha (exceto a do próprio
  Titular) mostra um seletor de papel que dispara `PUT /members/{id}`
  ao trocar (decisão 5), e um ícone de remover que abre a confirmação
  (decisão 4); o botão "+ Convidar pessoa" fica visível
- Quando o usuário logado não é o Titular: nenhuma linha mostra
  seletor de papel (só o nome do papel como texto) nem ícone de
  remover; o botão "+ Convidar pessoa" não aparece (decisão 1)
- A linha correspondente ao e-mail do usuário logado (Titular ou não)
  ganha o indicador "(você)" (decisão 3)
- O popup "Convidar pessoa" tem campo de e-mail e seletor de papel
  (`Leitura`/`Lancar`/`Total`, inicial `Lancar` — decisão 6), com a
  descrição de cada papel (mesmos três textos do backend FEAT-20:
  "Pode visualizar despesas e relatórios, sem editar nada." /
  "Pode visualizar e lançar novas despesas." / "Pode visualizar,
  lançar despesas e criar categorias e orçamentos. Não pode gerenciar
  outros membros.")
- Ao confirmar o convite, `POST /members` é chamado; enquanto pendente,
  o popup mostra o overlay de processamento (decisão 2), com os botões
  "Cancelar"/"Enviar convite" desabilitados; ao suceder, o popup fecha,
  a lista passa a incluir o novo convite (via estado local, sem um novo
  `GET /members` — ver plan.md) e um toast "Convite enviado para
  {email}." aparece (decisão 2)
- Convite pra e-mail já membro da conta (pendente ou ativo) retorna 409
  — o popup permanece aberto com uma mensagem de erro inline (o toast é
  só pra sucesso)
- Trocar o papel de um membro chama `PUT /members/{id}` imediatamente;
  falha (rede, erro inesperado) reverte visualmente o seletor pro papel
  anterior e mostra uma mensagem de erro inline na linha do membro
- Remover um membro exige confirmar num diálogo (decisão 4); ao
  confirmar, `DELETE /members/{id}` é chamado e o membro sai da lista
  (via estado local, sem um novo `GET /members` — ver plan.md)
- Erros de API mapeados em classes tipadas próprias desta feature
  (`SessionExpiredError`, `NetworkError`, `ValidationError`,
  `ConflictError`, `NotFoundError`, `ForbiddenError`,
  `CannotModifyTitularError`, `CannotRemoveTitularError`,
  `UnknownMemberError`) — os três últimos (`Forbidden`,
  `CannotModifyTitular`, `CannotRemoveTitular`) não são alcançáveis em
  uso normal dado o comportamento da UI (decisões 1 e 4), mas são
  tratados defensivamente (ex.: outra sessão do Titular removeu o
  membro entre a lista carregar e a ação ser tentada)

## User Stories

**US1 — Titular vê a lista completa com ações**
- Given um usuário autenticado como Titular da conta ativa, com um
  membro ativo e um convite pendente
- When ele abre a tela "Membros da conta"
- Then vê sua própria linha destacada ("Você (titular)"), a linha do
  membro ativo com seletor de papel e ícone de remover, a linha do
  convite pendente com status "Convite pendente", e o botão "+
  Convidar pessoa"

**US2 — Não-Titular vê a lista sem ações de escrita**
- Given um usuário autenticado com papel `Leitura`, `Lancar` ou `Total`
  na conta ativa
- When ele abre a tela "Membros da conta"
- Then vê a lista completa (incluindo o Titular e os demais membros),
  sua própria linha com "(você)", mas sem seletor de papel, sem ícone
  de remover em nenhuma linha, e sem o botão "+ Convidar pessoa"

**US3 — Titular convida um novo membro com sucesso**
- Given um usuário autenticado como Titular
- When ele clica em "+ Convidar pessoa", preenche e-mail e papel, e
  confirma
- Then o popup mostra o overlay "Enviando convite", e ao suceder fecha,
  a lista passa a incluir o novo convite (`Convite pendente`), e um
  toast "Convite enviado para {email}." aparece

**US4 — Convite para e-mail já membro é rejeitado**
- Given um usuário autenticado como Titular, e um e-mail que já é
  membro (pendente ou ativo) da conta
- When ele tenta convidar esse mesmo e-mail
- Then o popup permanece aberto com uma mensagem de erro inline, e
  nenhum toast aparece

**US5 — Titular troca o papel de um membro**
- Given um usuário autenticado como Titular, com um membro de papel
  `Leitura`
- When ele seleciona "Total" no seletor de papel desse membro
- Then `PUT /members/{id}` é chamado e o seletor passa a refletir
  "Total" imediatamente

**US6 — Falha ao trocar o papel reverte o seletor**
- Given um usuário autenticado como Titular
- When ele troca o papel de um membro e a chamada falha (erro de rede
  ou inesperado)
- Then o seletor volta a refletir o papel anterior, e uma mensagem de
  erro aparece na linha desse membro

**US7 — Titular remove um membro com confirmação**
- Given um usuário autenticado como Titular, com um membro cadastrado
- When ele clica no ícone de remover, confirma no diálogo
- Then `DELETE /members/{id}` é chamado e o membro some da lista

**US8 — Cancelar a remoção não altera nada**
- Given um usuário autenticado como Titular, com o diálogo de
  confirmação de remoção aberto
- When ele clica em cancelar
- Then o diálogo fecha e o membro continua na lista, sem nenhuma
  chamada à API

**US9 — Erro de sessão expirada**
- Given um usuário cuja sessão expirou
- When a tela "Membros da conta" tenta carregar `GET /members` ou `GET
  /auth/me`
- Then o comportamento já existente de sessão expirada se aplica
  (limpa a sessão, redireciona pro login), mesmo padrão já usado nas
  demais telas

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo em
`backend/specs/FEAT-20-membros-convites-permissoes/spec.md`.

### GET /members

Response 200:
```json
{
  "items": [
    { "id": "...", "email": "titular@email.com", "role": "Titular", "status": "Ativo", "createdAt": "2025-06-15T12:34:56Z" },
    { "id": "...", "email": "convidado@email.com", "role": "Leitura", "status": "ConvitePendente", "createdAt": "2025-06-16T09:00:00Z" }
  ]
}
```
Qualquer papel autenticado pode consultar. Erros: `401` (`unauthorized`).

### POST /members

Request: `{ "email": "convidado@email.com", "role": "Leitura" }`
(`role`: `Leitura`\|`Lancar`\|`Total`).

Response 201: dados do convite criado (mesmo formato de um item de
`GET /members`, `status="ConvitePendente"`).
Response 400 (`validation-error`): `email`/`role` ausente ou inválido.
Response 403 (`insufficient-permission`): quem chama não é Titular.
Response 409 (`member-already-exists`): e-mail já é membro da conta.

### PUT /members/{id}

Request: `{ "role": "Total" }`

Response 200: dados atualizados do membro.
Response 400 (`validation-error`): `role` ausente ou inválido.
Response 403 (`insufficient-permission`): quem chama não é Titular.
Response 404 (`not-found`): `id` não existe nesta conta.
Response 422 (`cannot-modify-titular`): tentativa de alterar o Titular.

### DELETE /members/{id}

Response 204: membro removido.
Response 403 (`insufficient-permission`): quem chama não é Titular.
Response 404 (`not-found`): `id` não existe nesta conta.
Response 422 (`cannot-remove-titular`): tentativa de remover o Titular.

### GET /auth/me

Já consumido por nenhuma tela hoje (`authApi.me()` existe no client,
sem uso). Response 200: `{ "userId": "...", "email": "...", "name":
"..." }`. Erros: `401` (`unauthorized`).

### Erros comuns a todas as rotas

Formato padrão do projeto (`ResultHttpExtensions.BuildProblem`,
`title` fixo por tipo de erro, mensagem específica em `detail`):

Response 400:
```json
{ "type": "https://gastosapp.dev/errors/validation-error", "title": "Parâmetros inválidos", "status": 400, "detail": "Papel de acesso inválido." }
```
Response 401: `{ "type": "https://gastosapp.dev/errors/unauthorized", "title": "Não autorizado", "status": 401 }`
Response 403: `{ "type": "https://gastosapp.dev/errors/insufficient-permission", "title": "Acesso negado", "status": 403, "detail": "Seu nível de acesso não permite esta ação." }`
Response 404: `{ "type": "https://gastosapp.dev/errors/not-found", "title": "Recurso não encontrado", "status": 404, "detail": "Membro não encontrado." }`
Response 409: `{ "type": "https://gastosapp.dev/errors/member-already-exists", "title": "Conflito", "status": 409, "detail": "Este e-mail já é membro desta conta." }`
Response 422: `{ "type": "https://gastosapp.dev/errors/cannot-remove-titular", "title": "Regra de negócio violada", "status": 422, "detail": "..." }` (ou `cannot-modify-titular`)

## Critérios de aceite

- [x] Tela "Membros da conta" acessível por um novo item de menu,
      buscando `GET /members` e `GET /auth/me` ao carregar
- [x] Linha do Titular sempre destacada separadamente, com tag
      "Titular" e descrição "Acesso total · gerencia membros"
- [x] Titular vê seletor de papel e ícone de remover em cada membro
      (exceto na própria linha), e o botão "+ Convidar pessoa"
- [x] Não-Titular vê a lista completa, mas sem seletor de papel, sem
      ícone de remover e sem o botão "+ Convidar pessoa"
- [x] Linha do usuário logado (Titular ou não) mostra o indicador
      "(você)"
- [x] Convite com sucesso mostra overlay de processamento, fecha o
      popup, recarrega a lista e mostra toast de confirmação
- [x] Convite para e-mail já membro mostra erro inline no popup, sem
      toast
- [x] Trocar o papel de um membro chama `PUT` imediatamente e reflete
      no seletor
- [x] Falha ao trocar o papel reverte o seletor e mostra erro inline
- [x] Remover um membro exige confirmação; confirmar chama `DELETE` e
      atualiza a lista; cancelar não chama a API
- [x] Sessão expirada ao carregar segue o comportamento já existente
      nas demais telas
- [x] Componentes de toast e overlay de processamento são genéricos
      (reutilizáveis por outras features no futuro), documentados como
      tal no `plan.md`
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando

## Fora do escopo

- Envio real de e-mail de convite — o backend não envia e-mail
  (decisão já fechada na FEAT-20); a tela só mostra que o convite foi
  registrado
- Reenvio de convite pendente ou indicação de expiração — convite
  pendente vale indefinidamente até ser aceito ou removido (mesma
  decisão do backend)
- Aceitar um convite / trocar de conta ativa — acontece só como efeito
  colateral do login (backend FEAT-20), sem nenhuma tela dedicada
- Retroaplicar o toast/overlay genéricos (decisão 2) a telas já
  existentes (login, salvar despesa/receita, salvar categoria) — só a
  nova tela de Membros os usa nesta feature
- Tratamento fino de permissão por role nas telas de Transações/
  Categorias (esconder botões de editar/excluir conforme o papel do
  usuário) — escopo da FEAT-29
- Qualquer mudança no contrato do backend — os quatro endpoints
  consumidos já estão prontos (backend FEAT-20, já em produção)
