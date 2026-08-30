# FEAT-23: Transações — generalizar despesa para receita/despesa (frontend)

## Objetivo

Migrar a feature `expenses` do frontend para consumir `/transactions`
(backend já implementado, `backend/specs/FEAT-22-transacoes-receita-
despesa`) no lugar de `/expenses`, que deixou de existir. A tela de
listagem (hoje "Despesas" por baixo dos panos, menu já rotulado
"Transações") passa a exibir despesas e receitas misturadas — mesmo
que, ao final desta feature, só seja possível **criar/editar despesa**
pela UI — e o popup de detalhe passa a mostrar quem lançou cada
transação ("Lançado por: Você"/e-mail de outro membro). É a maior
mudança de contrato desta leva do roadmap: `FEAT-24` (popup de nova
receita), `FEAT-25` (detalhe generalizado), `FEAT-26` (dashboard) e
`FEAT-27` (relatórios) dependem desta.

## Contexto

Hoje `features/expenses` só conhece despesa: chama `/expenses`
(`GET`/`POST`/`PUT`/`DELETE`), com campo de data `expenseDate`, sem
`tipo` nem informação de quem lançou. O menu já mostra o rótulo
"Transações" apontando para a rota `/expenses`
(`frontend/app/src/components/nav/navConfig.ts`) desde a FEAT-15 — só
a rota e o contrato de API estão desatualizados.

O backend já implementou tudo que este frontend consome nesta feature
(`backend/specs/FEAT-22-transacoes-receita-despesa/spec.md`, já em
produção): `/expenses` foi substituído integralmente por
`/transactions`, com `tipo` (`despesa`\|`receita`) obrigatório,
`date` no lugar de `expenseDate`, e `createdByUserId`/`createdByLabel`
("Você" para o próprio autor, e-mail do autor para os demais) em toda
resposta. `categoryId` deve apontar para uma categoria do mesmo `tipo`
da transação (regra do backend, `tipo` divergente retorna 400) — desde
a FEAT-22 do frontend, toda categoria já carrega `tipo`
(`despesa`\|`receita`), mas o formulário de despesa hoje lista **todas**
as categorias sem filtrar por tipo (`ExpenseForm.tsx`, via
`useCategories()`) — com o backend agora validando o `tipo`, isso
passaria a gerar 400 ao escolher uma categoria de receita.

Referência visual: `frontend/design-system/web/screenshots/
11-transacoes.png` e `19-detalhe-transacao.png`, e a fonte de verdade
`frontend/design-system/web/jrnexpenses-web.dc.html` (telas `isTx` e
`isViewingTx`) — a listagem mostra despesas e receitas na mesma tabela,
ordenadas cronologicamente, com o valor prefixado `+`/`-` e colorido
(verde para receita, vermelho/accent para despesa); filtro por chip de
categoria + "Filtros avançados" (data/valor), sem toggle dedicado de
tipo; o popup de detalhe tem uma seção "Lançado por".

**Decisões de escopo fechadas com o usuário durante este `/specify`:**

1. **Nesta feature, criar/editar transação continua restrito a
   despesa** — sem seletor de tipo no formulário e sem o botão "+ Nova
   receita" funcional ainda (o `.dc.html` já os mostra, mas ligá-los é
   escopo da `FEAT-24`, que "reaproveita o popup unificado desta
   FEAT-23"). O formulário sempre envia `tipo: "despesa"` para a API,
   de forma implícita (não é um campo do formulário).
2. **O dropdown de categoria do formulário de criar/editar passa a
   listar somente categorias de tipo despesa** (antes listava todas,
   sem filtro) — consequência direta da decisão 1 e da nova validação
   cruzada do backend (evita 400 ao tentar salvar com categoria de
   receita selecionada).
3. **A listagem já exibe despesas e receitas misturadas** (o `GET
   /transactions` já retorna os dois tipos, mesmo que só existam
   despesas lançadas pela UI até aqui) — valor com sinal e cor por
   tipo, igual ao design, e o filtro por categoria (chips) continua
   funcionando para categoria de qualquer tipo.
4. **O popup de detalhe ganha a seção "Lançado por"** (Você/e-mail do
   autor), mas mantém texto e cor fixos como despesa (título "Detalhe
   da despesa", valor sempre na cor de despesa) mesmo que a transação
   clicada seja uma receita — generalizar totalmente o popup (título/
   cor por tipo, conforme `19-detalhe-transacao.png`) é escopo da
   `FEAT-25`. Como não há forma de criar receita pela UI ainda (decisão
   1), esse caso só ocorreria com dado inserido fora da UI.
5. **Sem tratamento dedicado de `403`** — o backend agora retorna 403
   para o papel `Lancar` tentando editar/excluir transação de outro
   membro (novidade da FEAT-22 do backend); nesta feature esse caso cai
   no tratamento genérico de erro já existente (mensagem inespecífica),
   sem esconder botões nem mensagem própria. Tratamento fino por role é
   a `FEAT-29` do backlog.
6. **`tipo` fica disponível como filtro na função de busca da API**
   (`GetTransactionsParams`), mesmo sem nenhum controle de UI dedicado
   nesta feature para acioná-lo — plumbing alinhada ao restante dos
   filtros já existentes (`categoryId`, `yearMonth`, etc.), pronta para
   quando uma feature futura precisar (ex.: dashboard, relatórios).

## Requisitos de negócio

- Toda chamada de API desta feature usa `/transactions` — `/expenses`
  não existe mais no backend
- `description`, `amountInCents`, `categoryId`, `date`: obrigatórios em
  criar/editar (mesmas regras já existentes, só o nome do campo de data
  muda de `expenseDate` para `date`); `tipo: "despesa"` é sempre
  enviado, fixo, sem campo correspondente no formulário
- O dropdown de categoria do formulário de criar/editar mostra somente
  categorias de tipo despesa (`tipo === 'despesa'`)
- A listagem consome `GET /transactions` e exibe todos os itens
  retornados (despesa e receita), sem filtrar por tipo — cada item
  mostra o valor prefixado com `-` (despesa) ou `+` (receita) e colorido
  de acordo (mesma paleta do design: verde para receita, vermelho/
  accent para despesa)
- Filtros existentes (chip de categoria, mês, intervalo de data,
  intervalo de valor) continuam funcionando sobre a lista mista, sem
  mudança de comportamento — um chip de categoria de receita filtra
  para mostrar só as transações daquela categoria, igual antes
- O popup de detalhe mostra uma seção "Lançado por" com "Você" quando
  `createdByUserId` é o usuário logado, ou o e-mail do autor caso
  contrário
- Editar uma transação preserva `id`, `createdAt` e `createdByUserId`
  originais (já garantido pelo backend; o frontend não precisa
  reenviar nem validar isso, só refletir o que a API retorna)
- Erros de API mapeados como já são hoje (renomeados de `Expense*` para
  `Transaction*`): `ValidationError`, `SessionExpiredError`,
  `NetworkError`, `NotFoundError`, `InvalidFilterError`,
  `UnknownTransactionError`/`UnknownTransactionQueryError`,
  `UpdateValidationError` — `403` cai no tratamento genérico existente
  (sem classe dedicada, ver decisão 5)

## User Stories

**US1 — Ver listagem mista de despesas e receitas**
- Given uma conta com despesas e receitas registradas (ex.: via seed/
  API diretamente, já que a UI só cria despesa nesta feature)
- When o usuário abre a tela de Transações
- Then a lista mostra os itens dos dois tipos, ordenados da mais
  recente para a mais antiga, cada um com valor prefixado (`-`/`+`) e
  colorido conforme o tipo

**US2 — Registrar nova despesa continua funcionando**
- Given o usuário autenticado com papel de escrita
- When ele preenche o formulário de nova despesa e submete
- Then `POST /transactions` é chamado com `tipo: "despesa"` (implícito,
  sem campo no formulário), a API retorna 201, e a nova despesa aparece
  na listagem

**US3 — Dropdown de categoria só lista categorias de despesa**
- Given o usuário com categorias de despesa e de receita cadastradas
- When ele abre o formulário de nova/editar despesa
- Then o campo de categoria mostra somente as categorias de tipo
  despesa

**US4 — Editar despesa existente continua funcionando**
- Given uma despesa existente do usuário
- When ele edita descrição, valor, categoria (de despesa) ou data, e
  submete
- Then `PUT /transactions/{id}` é chamado com `tipo: "despesa"`, a API
  retorna 200, e a lista reflete os novos dados

**US5 — Excluir despesa continua funcionando**
- Given uma despesa existente do usuário
- When ele confirma a exclusão no popup correspondente
- Then `DELETE /transactions/{id}` é chamado, a API retorna 204, e o
  item some da listagem

**US6 — Detalhe mostra "Lançado por: Você"**
- Given uma transação criada pelo próprio usuário logado
- When ele clica na linha da transação na listagem
- Then o popup de detalhe mostra a seção "Lançado por" com o texto
  "Você"

**US7 — Detalhe mostra o e-mail de quem lançou, quando é outro membro**
- Given uma transação criada por outro membro da mesma conta
- When o usuário clica na linha dessa transação
- Then o popup de detalhe mostra a seção "Lançado por" com o e-mail
  desse outro membro

**US8 — Filtro por categoria de receita funciona**
- Given uma conta com transações de receita numa categoria de receita
- When o usuário aplica o chip dessa categoria na tela de Transações
- Then a lista mostra somente as transações daquela categoria (mesmo
  comportamento já existente para categoria de despesa)

**US9 — Filtros avançados continuam funcionando sobre a lista mista**
- Given uma conta com despesas e receitas em datas/valores variados
- When o usuário aplica filtro de mês, intervalo de data e/ou intervalo
  de valor
- Then a lista mostra apenas as transações (de qualquer tipo) que
  satisfazem todos os filtros combinados, mesmo comportamento já
  coberto hoje para despesas

**US10 — Formulário bloqueia categoria de receita antes de chamar a API**
- Given o formulário de nova/editar despesa
- When o usuário tenta submeter sem selecionar categoria (ou com uma
  categoria que deixou de existir na lista filtrada)
- Then o submit é bloqueado no client com a mensagem já existente
  ("Selecione uma categoria."), sem chamar a API — não há como
  selecionar uma categoria de receita, pois ela nem aparece no dropdown

**US11 — Sessão expirada e erro de rede continuam tratados**
- Given qualquer chamada desta feature (`GET`/`POST`/`PUT`/
  `DELETE /transactions`)
- When a API retorna 401, ou a chamada falha por rede
- Then o erro correspondente já existente hoje é exibido (sessão
  expirada / erro de conexão), sem mudança de comportamento

**US12 — Rota `/transactions` substitui `/expenses`**
- Given o usuário autenticado navegando pelo menu
- When ele clica no item "Transações" do menu (rótulo já existente)
- Then a URL da SPA é `/transactions` (não mais `/expenses`), e a
  tela carrega normalmente

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo em
`backend/specs/FEAT-22-transacoes-receita-despesa/spec.md`. Resumo do
que esta feature passa a enviar/receber (substitui integralmente o uso
de `/expenses`):

### POST /transactions

Request (sempre `tipo: "despesa"` nesta feature):
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "tipo": "despesa",
  "date": "2025-06-15"
}
```
Response 201:
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
Erros: `400` (`validation-error`), `401` (`unauthorized`), `403`
(`insufficient-permission`, papel `Leitura`).

### GET /transactions

Query params usados por esta feature: `categoryId`, `yearMonth`,
`dateFrom`, `dateTo`, `minAmountInCents`, `maxAmountInCents`, `cursor`
(mesmos de hoje) — `tipo` fica disponível na função da API, sem uso por
nenhum controle de UI ainda (ver decisão 6).

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
Erros: `400` (`validation-error`), `401` (`unauthorized`).

### GET /transactions/{id}

Response 200: mesmo formato de item acima (usado para popular o popup
de detalhe, hoje via item já carregado na listagem, sem chamada extra —
sem mudança de padrão).
Erros: `401`, `404` (`not-found`).

### PUT /transactions/{id}

Request: mesmo corpo do `POST`, com `tipo: "despesa"` fixo.
Response 200: mesmo formato, preservando `id`/`createdAt`/
`createdByUserId` originais.
Erros: `400`, `401`, `403` (`insufficient-permission`), `404`
(`not-found`).

### DELETE /transactions/{id}

Sem request body. Response 204.
Erros: `401`, `403` (`insufficient-permission`), `404` (`not-found`).

## Critérios de aceite

- [x] Todas as chamadas desta feature usam `/transactions` — nenhuma
      chamada a `/expenses` permanece no código
- [x] Registrar despesa envia `tipo: "despesa"` implicitamente (sem
      campo correspondente no formulário) e continua funcionando (201)
- [x] Editar despesa envia `tipo: "despesa"` e continua funcionando
      (200), preservando os dados retornados pela API
- [x] Excluir despesa continua funcionando (204)
- [x] Dropdown de categoria do formulário mostra somente categorias de
      tipo despesa
- [x] Listagem exibe despesas e receitas (quando existirem) misturadas,
      ordenadas por data, cada uma com sinal (`-`/`+`) e cor conforme o
      tipo
- [x] Filtro por chip de categoria funciona para categoria de despesa e
      de receita
- [x] Filtros avançados (mês, intervalo de data, intervalo de valor)
      continuam funcionando sobre a lista mista
- [x] Popup de detalhe mostra a seção "Lançado por" com "Você" (autor é
      o próprio usuário) ou o e-mail do autor (outro membro)
- [x] Erros 400/401/404 continuam tratados com as mensagens já
      existentes (classes renomeadas de `Expense*` para `Transaction*`)
- [x] Rota da SPA é `/transactions` (não mais `/expenses`); o item
      "Transações" do menu aponta para ela
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando

## Fora do escopo

- Seletor de tipo (despesa/receita) no formulário de criar/editar e
  botão "+ Nova receita" funcional — `FEAT-24` (que reaproveita o
  popup criado/generalizado nesta feature)
- Generalização visual completa do popup de detalhe (título dinâmico
  "Detalhe da receita"/"Detalhe da despesa" e cor do valor por tipo,
  conforme `19-detalhe-transacao.png`) — `FEAT-25`; nesta feature o
  popup mantém título e cor fixos como despesa (ver decisão 4)
- Tratamento dedicado de `403` (esconder ações, mensagem específica)
  para o papel `Lancar` numa transação de outro membro — `FEAT-29`
- Toggle/filtro de tipo (despesa/receita) na UI da listagem — o design
  não tem esse controle nesta tela (só chip de categoria + "Todas");
  `tipo` fica só como parâmetro disponível na função da API (decisão 6)
- Qualquer mudança no contrato do backend — `/transactions` já
  implementa tudo que esta feature consome (backend FEAT-22, já em
  produção)
- Dashboard/resumo mensal (`FEAT-26`), relatórios (`FEAT-27`) e
  exportação CSV — ficam para as features de transação seguintes do
  backlog do frontend
- Aplicar `tipo` da categoria como filtro do próprio seletor de
  categoria de outras telas (ex.: popup de nova receita) — resolvido
  quando essas telas forem implementadas (`FEAT-24`)
