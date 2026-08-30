# FEAT-26: Dashboard (Início) — resumo mensal

## Objetivo

Substituir a tela "Início" (`HomePage`, hoje "Em breve") pelo dashboard
"Resumo" do design system, consumindo `GET /summary?month=` (backend
FEAT-23, já em produção). O usuário passa a ver, ao entrar no app: saldo
do mês, receitas, gasto, orçamento total, restante, onde o dinheiro foi
(por categoria de despesa com orçamento) e os últimos lançamentos —
tudo num único carregamento, sem precisar navegar até Transações ou
Categorias pra ter essa visão.

## Contexto

Hoje `HomePage` é só um placeholder ("Em breve"), a primeira rota que o
usuário vê após o login (`/`, item "Início" do menu). O backend já
expõe tudo que essa tela precisa num único endpoint
(`backend/specs/FEAT-23-resumo-mensal-dashboard/spec.md`, já em
produção): `saldoCents`, `receitasCents`, `gastoCents`,
`orcamentoTotalCents`, `restanteCents`, `porCategoria` (categorias de
despesa com orçamento definido, ordenadas por gasto decrescente) e
`ultimosLancamentos` (até 5 transações do mês, mais recente primeiro,
mesmo formato de item de `GET /transactions`).

Referência visual: `frontend/design-system/web/screenshots/
05-dashboard.png`, e a fonte de verdade
`frontend/design-system/web/jrnexpenses-web.dc.html` (bloco `isDash`).
O design mostra: título "Resumo" + rótulo do mês; botões "+ Nova
receita"/"+ Nova despesa" (mesmo popup já usado em Transações desde a
FEAT-23/24); cinco cartões (Saldo do mês, Receitas no mês, Gasto no
mês, Orçamento total, Restante — este último com barra de progresso);
duas colunas — "Onde o dinheiro foi este mês" (categoria + gasto/
orçamento + barra, link "Ver todas (N) →") e "Últimos lançamentos"
(cada item com categoria, descrição, data, valor com sinal/cor por
tipo — mesmo padrão de `TransactionList`/`TransactionDetailDialog`
desde a FEAT-23/24 —, link "Ver todas →").

**Decisões de escopo fechadas com o usuário durante este `/specify`:**

1. **Sempre o mês corrente, sem navegação para outros meses.** O
   protótipo (`.dc.html`) não modela nenhuma navegação de mês (mostra
   sempre "Agosto de 2026" fixo, sem seta anterior/próximo nem
   seletor) — esta feature busca `GET /summary?month=` com o mês atual
   do dispositivo do usuário, sem nenhum controle para trocar de mês.
   Navegação por outros meses fica para uma feature futura, se pedida.
2. **`restanteCents` negativo é mostrado de verdade**, não escondido.
   O backend permite `restanteCents` negativo quando o gasto ultrapassa
   o orçamento total, e deixa explícito que "o frontend decide como
   exibir o estouro" — esta feature mostra o valor negativo real (ex.:
   "- R$ 50,00") na cor accent, com a barra de progresso do bloco
   "Restante" travada em 100%. O protótipo estático usa
   `Math.max(0, orçamento - gasto)` (nunca mostra negativo), mas isso é
   só porque a massa de dados fake da demo nunca gera um cenário de
   estouro — não é uma decisão de design deliberada de esconder o
   estouro.
3. **"Ver todas" de Últimos lançamentos navega para `/transactions`
   filtrado pelo mês do resumo** (`?yearMonth=YYYY-MM`, filtro que já
   existe em `GET /transactions` desde a FEAT-06) — mantém coerência
   entre o que o usuário viu no resumo e o que vê ao clicar. "Ver
   todas" de "Onde o dinheiro foi" navega para `/categories` sem
   filtro (não existe filtro de mês nessa tela).
4. **Os botões "+ Nova receita"/"+ Nova despesa" do design entram
   nesta feature**, reaproveitando o mesmo popup unificado já usado em
   Transações (`TransactionFormDialog`, FEAT-23/24) — ao salvar com
   sucesso, o resumo é recarregado (`GET /summary` refeito), refletindo
   a transação recém-criada sem precisar sair da tela.
5. **Edição/exclusão de uma transação a partir do dashboard não fazem
   parte desta feature.** Clicar num item de "Últimos lançamentos"
   nesta feature não abre o popup de detalhe (isso reaproveitaria
   `TransactionDetailDialog`, mas exigiria trazer a lógica de detail/
   edit/delete pra uma segunda tela — ver "Fora do escopo"); os itens
   são apenas informativos aqui, a interação plena continua em
   Transações.

## Requisitos de negócio

- Ao carregar a tela "Início", o frontend calcula o mês corrente
  (`YYYY-MM`, fuso do dispositivo do usuário) e consulta
  `GET /summary?month=<mês corrente>`
- Os cinco cartões mostram, formatados em reais
  (`formatCentsToCurrency`): Saldo do mês (`saldoCents`, cor positiva
  quando ≥ 0, cor accent quando negativo, sinal `-` quando negativo),
  Receitas no mês (`receitasCents`), Gasto no mês (`gastoCents`),
  Orçamento total (`orcamentoTotalCents`), Restante (`restanteCents`,
  incluindo negativo — decisão 2)
- O cartão "Restante" tem uma barra de progresso: `gastoCents /
  orcamentoTotalCents` (0% quando `orcamentoTotalCents` é 0, travada em
  100% quando o gasto atinge ou ultrapassa o orçamento total)
- "Onde o dinheiro foi este mês" lista os itens de `porCategoria` (já
  vêm ordenados por gasto decrescente da API), cada um mostrando nome
  da categoria, "gasto / orçamento" formatado, e uma barra de progresso
  (`gastoCents / orcamentoMensalCents` da categoria, travada em 100%);
  cor de destaque (texto e barra) muda quando o gasto da categoria
  ultrapassa o orçamento dela (mesmo padrão do cartão "Restante")
- Quando `porCategoria` vem vazio (nenhuma categoria de despesa com
  orçamento definido), a seção mostra uma mensagem de estado vazio em
  vez da lista
- "Últimos lançamentos" lista os itens de `ultimosLancamentos` (já
  vêm ordenados, mais recente primeiro), cada um mostrando categoria
  (nome resolvido via `GET /categories`, mesmo padrão de
  `TransactionList`), descrição, data e valor com sinal (`+`/`-`) e
  cor (positive/accent) conforme `tipo` — mesmo padrão visual já usado
  em `TransactionList`/`TransactionDetailDialog`
- Quando `ultimosLancamentos` vem vazio, a seção mostra uma mensagem de
  estado vazio em vez da lista
- "+ Nova receita"/"+ Nova despesa" abrem o mesmo popup unificado já
  usado em Transações, fixo no tipo correspondente (mesmo mecanismo da
  FEAT-24: o tipo vem de qual botão foi clicado, sem seletor dentro do
  formulário); ao salvar com sucesso, `GET /summary` é refeito para o
  mês corrente
- "Ver todas" de "Onde o dinheiro foi" navega para `/categories`; "Ver
  todas" de "Últimos lançamentos" navega para
  `/transactions?yearMonth=<mês corrente>` (decisão 3)
- Erros de API mapeados de forma nova para esta feature
  (`SessionExpiredError`, `NetworkError`, `UnknownSummaryError`) — não
  há erro de validação esperado em uso normal, já que o `month` é
  sempre calculado pelo client, nunca digitado pelo usuário

## User Stories

**US1 — Ver o resumo do mês corrente ao entrar no app**
- Given um usuário autenticado com receitas, despesas e categorias com
  orçamento definido no mês corrente
- When ele abre a tela "Início"
- Then a tela mostra os cinco cartões (Saldo, Receitas, Gasto,
  Orçamento total, Restante) com os valores do mês corrente, a lista
  "Onde o dinheiro foi este mês" e a lista "Últimos lançamentos",
  todos preenchidos a partir de `GET /summary?month=<mês corrente>`

**US2 — Saldo negativo é destacado**
- Given um usuário cujo gasto do mês corrente é maior que as receitas
- When ele abre a tela "Início"
- Then o cartão "Saldo do mês" mostra o valor negativo com o sinal
  `-` e na cor accent

**US3 — Restante negativo quando o gasto ultrapassa o orçamento**
- Given um usuário cujo `gastoCents` do mês corrente é maior que
  `orcamentoTotalCents`
- When ele abre a tela "Início"
- Then o cartão "Restante" mostra o valor negativo real (não zerado),
  e a barra de progresso aparece travada em 100%

**US4 — Categoria acima do orçamento é destacada na lista**
- Given uma categoria de despesa com orçamento definido cujo gasto no
  mês corrente ultrapassa o orçamento dela
- When o usuário abre a tela "Início"
- Then essa categoria aparece em "Onde o dinheiro foi este mês" com
  texto e barra na cor de destaque, e a barra travada em 100%

**US5 — Sem categoria com orçamento definido**
- Given um usuário sem nenhuma categoria de despesa com orçamento
  definido
- When ele abre a tela "Início"
- Then a seção "Onde o dinheiro foi este mês" mostra uma mensagem de
  estado vazio, sem quebrar o resto da tela

**US6 — Sem transação no mês corrente**
- Given um usuário sem nenhuma transação registrada no mês corrente
- When ele abre a tela "Início"
- Then os cartões mostram valores zerados e a seção "Últimos
  lançamentos" mostra uma mensagem de estado vazio

**US7 — Últimos lançamentos com sinal e cor por tipo**
- Given um usuário com receitas e despesas registradas no mês corrente
- When ele abre a tela "Início"
- Then cada item de "Últimos lançamentos" mostra o valor com o sinal e
  a cor correspondentes ao seu tipo (`+`/verde para receita, `-`/
  accent para despesa)

**US8 — Registrar despesa a partir do dashboard**
- Given um usuário autenticado com papel de escrita e ao menos uma
  categoria de despesa
- When ele clica em "+ Nova despesa", preenche o formulário e submete
- Then a despesa é criada (`POST /transactions`, mesmo comportamento
  já validado na FEAT-23), e o resumo da tela é atualizado refletindo
  a nova despesa, sem precisar recarregar a página

**US9 — Registrar receita a partir do dashboard**
- Given um usuário autenticado com papel de escrita e ao menos uma
  categoria de receita
- When ele clica em "+ Nova receita", preenche o formulário e submete
- Then a receita é criada (`POST /transactions`, mesmo comportamento
  já validado na FEAT-24), e o resumo da tela é atualizado refletindo
  a nova receita

**US10 — "Ver todas" de últimos lançamentos filtra pelo mês do resumo**
- Given um usuário na tela "Início"
- When ele clica em "Ver todas →" na seção "Últimos lançamentos"
- Then ele é levado para `/transactions` já filtrada pelo mês corrente
  (`?yearMonth=<mês corrente>`)

**US11 — "Ver todas" de categorias navega sem filtro**
- Given um usuário na tela "Início"
- When ele clica em "Ver todas (N) →" na seção "Onde o dinheiro foi
  este mês"
- Then ele é levado para `/categories`

**US12 — Erro de sessão expirada**
- Given um usuário cuja sessão expirou
- When a tela "Início" tenta carregar `GET /summary`
- Then o comportamento já existente de sessão expirada se aplica
  (limpa a sessão, redireciona pro login), mesmo padrão já usado nas
  demais telas

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo em
`backend/specs/FEAT-23-resumo-mensal-dashboard/spec.md`.

### GET /summary?month=YYYY-MM

Response 200:
```json
{
  "month": "2026-08",
  "saldoCents": 394720,
  "receitasCents": 520000,
  "gastoCents": 125280,
  "orcamentoTotalCents": 299000,
  "restanteCents": 173720,
  "porCategoria": [
    {
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "nome": "Alimentação",
      "gastoCents": 30670,
      "orcamentoMensalCents": 80000
    }
  ],
  "ultimosLancamentos": [
    {
      "id": "...",
      "description": "Supermercado Pão de Açúcar",
      "amountInCents": 18790,
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "tipo": "despesa",
      "date": "2026-08-24",
      "createdByUserId": "a1b2c3d4-...",
      "createdByLabel": "Você",
      "createdAt": "2026-08-24T18:12:00Z"
    }
  ]
}
```
Erros: `400` (`validation-error`, não esperado em uso normal — `month`
sempre calculado pelo client), `401` (`unauthorized`).

### POST /transactions, PUT/DELETE /transactions/{id}

Sem mudança — já documentados nas FEAT-23/24. Reaproveitados via
`TransactionFormDialog` (decisão 4).

### GET /categories

Sem mudança — já documentado na FEAT-22. Usado para resolver nome da
categoria dos itens de "Últimos lançamentos" (mesmo padrão de
`TransactionList`).

## Critérios de aceite

- [ ] Tela "Início" busca `GET /summary?month=<mês corrente>` ao
      carregar, sem nenhum controle de navegação de mês
- [ ] Os cinco cartões (Saldo, Receitas, Gasto, Orçamento total,
      Restante) mostram os valores corretos, formatados em reais
- [ ] Saldo negativo aparece com sinal `-` e cor accent
- [ ] Restante negativo aparece com o valor real (não zerado), cor
      accent, e a barra de progresso travada em 100%
- [ ] "Onde o dinheiro foi este mês" lista as categorias de
      `porCategoria` na ordem recebida, com gasto/orçamento formatado e
      barra de progresso; categoria acima do orçamento aparece
      destacada (texto e barra na cor de destaque)
- [ ] Sem categoria com orçamento definido, a seção mostra estado
      vazio
- [ ] "Últimos lançamentos" lista os itens de `ultimosLancamentos` com
      categoria, descrição, data, e valor com sinal/cor por tipo
- [ ] Sem transação no mês, a seção mostra estado vazio
- [ ] "+ Nova despesa"/"+ Nova receita" abrem o popup já existente
      (fixo no tipo do botão clicado) e, ao salvar, o resumo é
      recarregado refletindo a nova transação
- [ ] "Ver todas" de últimos lançamentos navega para
      `/transactions?yearMonth=<mês corrente>`
- [ ] "Ver todas" de categorias navega para `/categories`
- [ ] Sessão expirada ao carregar o resumo segue o comportamento já
      existente nas demais telas
- [ ] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando

## Fora do escopo

- Navegação entre meses (anterior/próximo, seletor) — decisão 1,
  sempre o mês corrente nesta feature
- Popup de detalhe/edição/exclusão a partir de um item de "Últimos
  lançamentos" no dashboard — decisão 5; a interação plena continua
  na tela Transações
- Qualquer mudança no contrato do backend — `GET /summary` já
  implementa tudo que esta feature consome (backend FEAT-23, já em
  produção)
- Indicador/gráfico além do que o design já mostra (barras de
  progresso simples) — relatórios com gráfico são escopo da FEAT-27
  (primeira feature a introduzir Tremor)
- Tratamento dedicado de `403` — `GET /summary` é liberado pra
  qualquer papel autenticado (sem 403 possível nesta rota, conforme o
  próprio contrato do backend)
- Toast de confirmação ao criar transação pelo dashboard — segue o
  mesmo padrão inline já usado hoje (débito técnico já registrado no
  backlog, "Componente de toast genérico")
