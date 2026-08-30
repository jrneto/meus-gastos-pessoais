# FEAT-24: Popup de nova receita

## Objetivo

Habilitar o lançamento de receita pela UI, reaproveitando o popup
unificado de transação já migrado na FEAT-23 (hoje restrito a
despesa). Ao final desta feature, a tela de Transações tem os dois
botões do design ("+ Nova despesa" e "+ Nova receita"), cada um
abrindo o mesmo popup já fixo no tipo correspondente, e o fluxo
completo (criar, editar, excluir, ver detalhe) funciona para os dois
tipos.

## Contexto

A FEAT-23 generalizou a listagem e o contrato de API para
`/transactions` (despesa e receita misturadas), mas deixou o
cadastro/edição restrito a despesa — decisão fechada naquela spec,
justamente para ser retomada aqui. Hoje:

- `TransactionsListPage` só tem o botão "+ Nova despesa"
- `TransactionForm`/`TransactionFormDialog` sempre enviam
  `tipo: "despesa"` (fixo, sem campo no formulário) e o dropdown de
  categoria só lista categorias de tipo despesa
- `TransactionDetailDialog`/`TransactionDeleteDialog` mostram título e
  cor sempre de despesa, independente do tipo real da transação
  (na FEAT-23 isso não tinha efeito prático, já que não existia forma
  de criar receita pela UI)

**Correção de entendimento em relação ao backlog:** a descrição da
FEAT-24 no backlog ("reaproveitando o popup unificado de nova
transação da FEAT-23 com o seletor de tipo já visível no design")
sugere um seletor dentro do formulário. Conferindo a fonte de verdade
(`frontend/design-system/web/jrnexpenses-web.dc.html`), o mecanismo
real é outro: **o tipo é definido por qual botão abre o popup**
("+ Nova despesa" abre com `newTxType: 'expense'`, "+ Nova receita"
com `newTxType: 'income'`), sem nenhum campo de tipo visível ou
editável dentro do formulário — nem ao criar, nem ao editar (editar
preserva o tipo original da transação, sem opção de trocar). O popup
já filtra a lista de categorias pelo tipo em uso
(`addCategories = catsComputed.filter(c => c.type === newTxType)`) e
troca título/rótulo do botão de salvar de acordo (`Nova receita`/
`Nova despesa`, `Salvar receita`/`Salvar despesa`). Esta spec segue o
mecanismo real do `.dc.html`, confirmado com o usuário durante o
`/specify`.

Referência visual: `frontend/design-system/web/screenshots/
08-nova-receita.png`, e a fonte de verdade
`frontend/design-system/web/jrnexpenses-web.dc.html` (bloco
`showAdd`).

**Decisões de escopo fechadas com o usuário durante este `/specify`:**

1. **Seletor de tipo = qual botão abre o popup**, confirmado acima —
   não há toggle/seletor dentro do formulário em nenhum momento.
2. **`TransactionDetailDialog` e `TransactionDeleteDialog` ganham
   título e cor do valor dinâmicos por tipo** ("Detalhe da despesa"/
   "Detalhe da receita", cor accent/positive; "Excluir despesa"/
   "Excluir receita") nesta própria feature — evita a inconsistência
   óbvia de abrir uma receita recém-criada e ver "despesa" na tela.
   Qualquer generalização visual **além** disso (ícone, demais ajustes
   finos de `19-detalhe-transacao.png`) continua sendo escopo da
   `FEAT-25`.
3. **Sem o picker de categoria com busca e painel de orçamento** que o
   `.dc.html` mostra no popup (`addCategories` com busca +
   "Teto do mês"/"Já gasto"/barra de consumo) — mantém o `<select>`
   simples já usado desde a FEAT-17, só filtrado por tipo. Esse painel
   de consumo depende de `GET /summary` (backend FEAT-23), cuja
   integração no frontend já foi explicitamente adiada pela FEAT-22 de
   categorias ("indicador de consumo... retomado quando o frontend
   tiver sua própria feature de resumo/transações") — mesma lógica se
   aplica aqui, sem motivo para abrir exceção nesta feature.
4. **Rótulo do botão de salvar ao criar continua "Registrar
   despesa"/"Registrar receita"** (verbo já estabelecido desde a
   FEAT-17), não o "Salvar despesa"/"Salvar receita" do `.dc.html` —
   mantém o vocabulário já testado e em uso, a diferença é só o verbo
   ("Registrar" em vez de "Salvar"), sem mudança de comportamento. O
   rótulo ao editar continua "Salvar alterações" (genérico, igual hoje,
   igual ao `.dc.html`).
5. **Tipo de uma transação nunca muda depois de criada** — editar
   preserva o tipo original (a categoria só pode ser trocada por outra
   do mesmo tipo); não há campo nem ação na UI para mudar o tipo de
   uma transação existente.

## Requisitos de negócio

- A tela de Transações tem dois botões: "+ Nova despesa" (já existe,
  estilo primário) e "+ Nova receita" (novo, estilo secundário, à
  esquerda dele — mesma ordem do design)
- Cada botão abre o popup já fixo no tipo correspondente: título
  "Nova despesa"/"Nova receita", dropdown de categoria mostrando
  somente categorias daquele tipo, botão de ação "Registrar despesa"/
  "Registrar receita"
- `POST /transactions` é chamado com `tipo` igual ao botão que abriu o
  popup (`"despesa"` ou `"receita"`), nunca vindo de um campo do
  formulário
- Ao editar uma transação existente (a partir do popup de detalhe), o
  popup abre com título "Editar despesa"/"Editar receita" conforme o
  tipo real da transação, dropdown de categoria filtrado por esse
  mesmo tipo, e `PUT /transactions/{id}` é chamado com esse tipo
  preservado (nunca alterado pela edição)
- Quando a conta não tem nenhuma categoria do tipo sendo lançado, o
  formulário mostra a mesma orientação de hoje ("Você ainda não tem
  nenhuma categoria de despesa/receita cadastrada.", com link para
  criar uma), com o texto ajustado ao tipo
- `TransactionDetailDialog` mostra título "Detalhe da despesa"/
  "Detalhe da receita" e a cor do valor (accent/positive) conforme
  `transaction.tipo` — mesmo comportamento para qualquer transação já
  existente na conta (inclusive as criadas antes desta feature, via
  API)
- `TransactionDeleteDialog` mostra título "Excluir despesa"/"Excluir
  receita" conforme `transaction.tipo`
- Erros de API mapeados como já são hoje (`ValidationError`,
  `SessionExpiredError`, `NetworkError`, `NotFoundError`,
  `UpdateValidationError`, `UnknownTransactionError`), sem necessidade
  de erro tipado novo — `tipo` sempre vem de um valor interno
  controlado pela UI (nunca de input livre do usuário), então o client
  não tem como enviar um `tipo` inválido

## User Stories

**US1 — Ver o botão "+ Nova receita" na tela de Transações**
- Given o usuário autenticado com papel de escrita
- When ele abre a tela de Transações
- Then os dois botões aparecem: "+ Nova despesa" e "+ Nova receita"

**US2 — Registrar nova receita**
- Given o usuário com ao menos uma categoria de tipo receita
  cadastrada
- When ele clica em "+ Nova receita", preenche o formulário (descrição,
  valor, categoria de receita, data) e submete
- Then `POST /transactions` é chamado com `tipo: "receita"`, a API
  retorna 201, e a nova receita aparece na listagem com sinal `+` e
  cor verde (já implementado na FEAT-23)

**US3 — Dropdown de categoria do popup de receita só lista categoria de receita**
- Given o usuário com categorias de despesa e de receita cadastradas
- When ele abre o popup "+ Nova receita"
- Then o campo de categoria mostra somente as categorias de tipo
  receita

**US4 — Sem categoria de receita, é orientado a criar uma**
- Given o usuário sem nenhuma categoria de tipo receita cadastrada
  (mas com categorias de despesa)
- When ele abre o popup "+ Nova receita"
- Then vê a orientação para criar uma categoria (com link para
  `/categories`), sem o formulário

**US5 — Editar receita existente**
- Given uma receita existente na conta (criada por esta feature ou já
  presente via API)
- When o usuário abre o detalhe dela e clica em "Editar"
- Then o popup abre com título "Editar receita", campos pré-preenchidos
  e dropdown de categoria mostrando só categorias de receita; ao
  submeter, `PUT /transactions/{id}` é chamado com `tipo: "receita"`
  preservado

**US6 — Excluir receita existente**
- Given uma receita existente na conta
- When o usuário abre o detalhe dela e clica em "Excluir", depois
  confirma
- Then o popup de confirmação mostra "Excluir receita", e
  `DELETE /transactions/{id}` é chamado normalmente

**US7 — Detalhe de receita mostra título e cor corretos**
- Given uma receita existente na conta
- When o usuário clica na linha dela na listagem
- Then o popup de detalhe mostra o título "Detalhe da receita" e o
  valor na cor verde (positive), não mais o "Detalhe da despesa"/cor
  vermelha fixos da FEAT-23

**US8 — Detalhe de despesa continua mostrando título e cor de despesa**
- Given uma despesa existente na conta
- When o usuário clica na linha dela na listagem
- Then o popup de detalhe mostra "Detalhe da despesa" e a cor de
  destaque (accent) — sem regressão do comportamento já existente

**US9 — Registrar despesa continua funcionando sem mudança**
- Given o usuário com papel de escrita
- When ele clica em "+ Nova despesa" e completa o fluxo já existente
- Then o comportamento é idêntico ao da FEAT-23 (`tipo: "despesa"`,
  dropdown só com categoria de despesa, título "Nova despesa")

**US10 — Tipo de uma transação não pode ser alterado na edição**
- Given uma despesa ou receita existente
- When o usuário abre o popup de edição dela
- Then não há nenhum campo ou controle para trocar o tipo da
  transação — só é possível editar descrição, valor, categoria (do
  mesmo tipo) e data

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo em
`backend/specs/FEAT-22-transacoes-receita-despesa/spec.md` — esta
feature apenas passa a exercitar `tipo: "receita"` nas mesmas chamadas
já usadas desde a FEAT-23:

### POST /transactions (tipo receita)

Request:
```json
{
  "description": "Salário",
  "amountInCents": 500000,
  "categoryId": "<id de categoria de tipo receita>",
  "tipo": "receita",
  "date": "2025-06-05"
}
```
Response 201: mesmo formato já documentado na FEAT-23, com
`tipo: "receita"`.
Erros: `400` (`validation-error`, inclui categoria de tipo divergente),
`401`, `403` (papel `Leitura`).

### PUT /transactions/{id} (tipo receita, preservado da criação)

Mesmo corpo do `POST`, com `tipo: "receita"` — nunca outro valor,
já que a UI não oferece como trocar. Erros: `400`, `401`, `403`, `404`.

### DELETE /transactions/{id}

Sem mudança — já documentado na FEAT-23.

## Critérios de aceite

- [ ] Botão "+ Nova receita" aparece na tela de Transações, ao lado do
      "+ Nova despesa" já existente
- [ ] "+ Nova receita" abre o popup com título "Nova receita",
      dropdown de categoria só com categorias de tipo receita, e botão
      "Registrar receita"
- [ ] Submeter o formulário de nova receita chama `POST /transactions`
      com `tipo: "receita"` e os demais campos preenchidos
- [ ] Sem categoria de receita cadastrada, o popup mostra a orientação
      para criar uma (com o texto ajustado a "categoria de receita"),
      em vez do formulário
- [ ] Editar uma receita existente abre o popup com título "Editar
      receita", dropdown filtrado por receita, e `PUT /transactions/{id}`
      preserva `tipo: "receita"`
- [ ] Excluir uma receita existente mostra "Excluir receita" no popup
      de confirmação e funciona normalmente (`DELETE`)
- [ ] Popup de detalhe de uma receita mostra "Detalhe da receita" e a
      cor do valor em verde (positive)
- [ ] Popup de detalhe de uma despesa continua mostrando "Detalhe da
      despesa" e a cor accent, sem regressão
- [ ] Fluxo de despesa (criar/editar/excluir/detalhe) continua
      funcionando exatamente como na FEAT-23, sem regressão
- [ ] Não existe nenhum campo ou controle na UI para trocar o tipo de
      uma transação existente
- [ ] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando

## Fora do escopo

- Picker de categoria com busca e painel de orçamento/consumo
  (`addCategories` com busca, "Teto do mês"/"Já gasto"/barra) do
  `.dc.html` — mantém o `<select>` simples já em uso, filtrado por
  tipo (decisão 3); depende de integração futura com `GET /summary`
- Generalização visual completa do popup de detalhe além de título/cor
  (ícone, demais ajustes finos de `19-detalhe-transacao.png`) —
  `FEAT-25`
- Tratamento dedicado de `403` para o papel `Lancar` — `FEAT-29`
  (sem mudança introduzida por esta feature)
- Qualquer mudança no contrato do backend — `/transactions` já
  implementa tudo que esta feature consome (backend FEAT-22, já em
  produção)
- Toggle/filtro de tipo na listagem de Transações — fora do design
  desta tela (só chip de categoria + "Todas"), sem mudança nesta
  feature
- Dashboard/resumo mensal (`FEAT-26`), relatórios (`FEAT-27`) —
  seguem no backlog do frontend
