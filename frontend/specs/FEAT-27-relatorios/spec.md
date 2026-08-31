# FEAT-27: Relatórios

## Objetivo

Substituir a tela "Relatórios" (`ReportsComingSoonPage`, hoje "Em
breve") pela tela real do design system, consumindo `GET
/reports?period=&date=` (backend FEAT-24, já em produção). O usuário
passa a poder consultar o gasto por categoria, o total e a variação
percentual, e a categoria de maior gasto, num período à sua escolha
(semana/mês/ano), sempre ancorado na data atual.

## Contexto

Hoje `ReportsComingSoonPage` é só um placeholder ("Em breve"), acessível
pelo item "Relatórios" do menu. O backend já expõe tudo que essa tela
precisa num único endpoint
(`backend/specs/FEAT-24-relatorios-por-periodo/spec.md`, já em
produção): `totalCents`, `variacaoPercentual`, `porCategoria`
(categorias de despesa com gasto no período, ordenadas por gasto
decrescente) e `maiorGasto` (categoria de maior gasto, com
`percentualOrcamento` sobre o orçamento dela).

Referência visual: `frontend/design-system/web/screenshots/
12-relatorios.png`, e a fonte de verdade
`frontend/design-system/web/jrnexpenses-web.dc.html` (bloco `isRep`). O
design mostra: título "Relatórios" + um seletor Semana/Mês/Ano; duas
colunas — "Gasto por categoria" (nome + barra proporcional + valor,
ordenada do maior pro menor gasto) e dois cards, "Total no período"
(valor + texto de comparação com o período anterior, ex.: "+12% vs mês
passado") e "Maior gasto" (categoria + valor + "X% do orçamento").

**Decisões de escopo fechadas com o usuário durante este `/specify`:**

1. **Sempre a data atual do dispositivo, sem seletor de data.** Mesma
   decisão já fechada no backend (`backend/specs/
   FEAT-24-relatorios-por-periodo/spec.md`, seção "Fora do escopo"): o
   protótipo não modela um seletor de data, só o de período
   (Semana/Mês/Ano). O usuário troca `period` pelo seletor; `date` é
   sempre a data atual, recalculada a cada consulta. Período inicial ao
   abrir a tela: `month`.
2. **Barra de "Gasto por categoria" é proporcional ao maior gasto da
   lista** (`gastoCents da categoria / gastoCents da categoria no topo
   de porCategoria`, já que a lista vem ordenada decrescente pela API),
   **sempre na cor neutra** (`--color-neutral-800`). Diferente do
   padrão de "Onde o dinheiro foi" do dashboard (FEAT-26), que colore a
   barra por acima/abaixo do orçamento: `porCategoria` deste endpoint
   não traz orçamento por categoria (só `categoryId`/`nome`/
   `gastoCents`), então não há como replicar esse destaque aqui sem
   cruzar com `GET /categories` — decisão explícita de não fazer essa
   segunda chamada só por esse detalhe visual. O destaque de
   acima-do-orçamento fica restrito ao card "Maior gasto", que já
   recebe `percentualOrcamento` pronto do backend.
3. **Comparação com o período anterior some quando não é computável.**
   Quando `variacaoPercentual` vem `null` (período anterior sem gasto,
   período atual com gasto — o backend explicita que não é computável
   matematicamente), o card "Total no período" mostra só o valor total,
   sem a linha de comparação. Quando vem um número (positivo, negativo
   ou zero), mostra "`+`/`-`X% vs {rótulo do período anterior}", com o
   rótulo dependendo do período selecionado: "semana passada" (`week`),
   "mês passado" (`month`), "ano passado" (`year`).
4. **Estado vazio com mensagens genéricas.** Quando o período consultado
   não tem nenhuma despesa (`totalCents=0`, `porCategoria=[]`,
   `maiorGasto=null`), a lista "Gasto por categoria" mostra "Nenhuma
   despesa neste período." (mesmo padrão de estado vazio já usado em
   `RecentTransactionsList`/`CategorySpendingList` da FEAT-26) e o card
   "Maior gasto" mostra um texto genérico ("Nenhum gasto registrado")
   no lugar do nome da categoria, sem quebrar o card "Total no período"
   (que continua mostrando o total zerado normalmente).
5. **Sem introduzir a lib Tremor.** O backlog original previa esta
   feature como a primeira a precisar de gráfico, mas o `.dc.html`
   (fonte de verdade) mostra a tela só com barras proporcionais simples
   (`.je-track`/`.je-fill`), o mesmo padrão visual já implementado sem
   biblioteca externa nas FEAT-22/FEAT-26. Não há gráfico de linha,
   pizza ou similar no protótipo — a introdução do Tremor fica adiada
   para quando uma tela realmente exigir esse tipo de visualização
   (backlog atualizado para refletir isso).
6. **Sem interação nos itens da lista.** Igual à decisão 5 da FEAT-26
   para "Últimos lançamentos": os itens de "Gasto por categoria" são
   puramente informativos, sem `onClick`/navegação — clicar numa
   categoria não abre nada.

## Requisitos de negócio

- Ao carregar a tela "Relatórios", o frontend calcula a data atual
  (`YYYY-MM-DD`, fuso do dispositivo do usuário) e consulta `GET
  /reports?period=month&date=<data atual>` (período inicial `month`)
- O seletor Semana/Mês/Ano troca o `period` da consulta (`week`,
  `month`, `year`); a cada troca, uma nova consulta é feita com o
  `period` selecionado e a mesma data atual
- "Gasto por categoria" lista os itens de `porCategoria` na ordem
  recebida (já decrescente por gasto), cada um com nome da categoria,
  valor formatado em reais e uma barra proporcional ao maior gasto da
  lista (decisão 2), sempre na cor neutra
- Quando `porCategoria` vem vazio, a lista mostra a mensagem de estado
  vazio (decisão 4)
- O card "Total no período" mostra `totalCents` formatado em reais, e a
  linha de comparação com o período anterior conforme decisão 3
  (escondida quando `variacaoPercentual` é `null`)
- O card "Maior gasto" mostra o `nome` e `gastoCents` (formatado) da
  categoria de `maiorGasto`, e "X% do orçamento" quando
  `percentualOrcamento` não é `null`; quando `percentualOrcamento` é
  `null` (categoria sem orçamento definido), mostra só nome e valor,
  sem o percentual; quando `maiorGasto` é `null` (decisão 4), mostra a
  mensagem de estado vazio no lugar
- Erros de API mapeados de forma nova para esta feature
  (`SessionExpiredError`, `NetworkError`, `UnknownReportsError`) — não
  há erro de validação esperado em uso normal, já que `period` vem de
  um seletor fechado (3 valores possíveis) e `date` é sempre calculada
  pelo client, nunca digitada pelo usuário

## User Stories

**US1 — Ver o relatório do mês corrente ao entrar na tela**
- Given um usuário autenticado com despesas em várias categorias no mês
  corrente
- When ele abre a tela "Relatórios"
- Then a tela mostra "Gasto por categoria" (ordenada por gasto
  decrescente), o card "Total no período" e o card "Maior gasto",
  todos preenchidos a partir de `GET /reports?period=month&date=<data
  atual>`

**US2 — Trocar para o período Semana**
- Given um usuário na tela "Relatórios" com o período "Mês" selecionado
- When ele seleciona "Semana"
- Then a tela refaz a consulta com `period=week` (mesma data atual) e
  atualiza a lista e os cards com os dados da semana ISO corrente

**US3 — Trocar para o período Ano**
- Given um usuário na tela "Relatórios"
- When ele seleciona "Ano"
- Then a tela refaz a consulta com `period=year` (mesma data atual) e
  atualiza a lista e os cards com os dados do ano corrente

**US4 — Barra proporcional ao maior gasto da lista**
- Given um usuário com três categorias de despesa com gasto no período
  consultado, em valores diferentes
- When ele abre a tela "Relatórios"
- Then a categoria com maior gasto mostra a barra em 100% de largura, e
  as demais mostram a barra proporcional ao gasto dela em relação à
  primeira

**US5 — Sem despesa no período consultado**
- Given um usuário sem nenhuma despesa registrada no período consultado
- When ele abre a tela "Relatórios" (ou troca de período)
- Then "Gasto por categoria" mostra a mensagem de estado vazio, o card
  "Total no período" mostra o valor zerado, e o card "Maior gasto"
  mostra a mensagem de estado vazio, sem quebrar a tela

**US6 — Comparação com período anterior positiva**
- Given um usuário cujo total de despesas do período consultado é maior
  que o do período anterior
- When ele abre a tela "Relatórios"
- Then o card "Total no período" mostra a comparação com sinal `+` e o
  rótulo do período anterior correspondente (ex.: "+12% vs mês
  passado")

**US7 — Comparação com período anterior negativa**
- Given um usuário cujo total de despesas do período consultado é menor
  que o do período anterior
- When ele abre a tela "Relatórios"
- Then o card "Total no período" mostra a comparação com sinal `-`
  (ex.: "-4% vs semana passada")

**US8 — Comparação escondida quando não computável**
- Given um usuário sem nenhuma despesa no período anterior ao
  consultado, mas com despesas no período consultado
- When ele abre a tela "Relatórios"
- Then o card "Total no período" mostra só o valor total, sem nenhuma
  linha de comparação

**US9 — Maior gasto com percentual do orçamento**
- Given um usuário cuja categoria de maior gasto no período consultado
  tem orçamento mensal definido
- When ele abre a tela "Relatórios"
- Then o card "Maior gasto" mostra a categoria, o valor gasto e "X% do
  orçamento"

**US10 — Maior gasto sem orçamento definido**
- Given um usuário cuja categoria de maior gasto no período consultado
  não tem orçamento mensal definido
- When ele abre a tela "Relatórios"
- Then o card "Maior gasto" mostra a categoria e o valor gasto, sem
  nenhum percentual

**US11 — Erro de sessão expirada**
- Given um usuário cuja sessão expirou
- When a tela "Relatórios" tenta carregar `GET /reports`
- Then o comportamento já existente de sessão expirada se aplica
  (limpa a sessão, redireciona pro login), mesmo padrão já usado nas
  demais telas

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo em
`backend/specs/FEAT-24-relatorios-por-periodo/spec.md`.

### GET /reports?period=week|month|year&date=YYYY-MM-DD

Response 200:
```json
{
  "period": "month",
  "startDate": "2026-08-01",
  "endDate": "2026-08-31",
  "totalCents": 138120,
  "variacaoPercentual": 12.0,
  "porCategoria": [
    {
      "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
      "nome": "Alimentacao",
      "gastoCents": 43510
    },
    {
      "categoryId": "8a4f0b21-5c3d-4e2b-9f8a-3d2c4b5e6f70",
      "nome": "Moradia",
      "gastoCents": 31020
    }
  ],
  "maiorGasto": {
    "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
    "nome": "Alimentacao",
    "gastoCents": 43510,
    "percentualOrcamento": 54.4
  }
}
```
`variacaoPercentual` pode ser `null` (período anterior sem gasto,
período atual com gasto). `porCategoria` pode ser `[]` e `maiorGasto`
pode ser `null` (nenhuma despesa no período). `maiorGasto.
percentualOrcamento` pode ser `null` (categoria sem orçamento
definido).

Erros: `400` (`validation-error`, não esperado em uso normal — `period`
vem de um seletor fechado e `date` é sempre calculada pelo client),
`401` (`unauthorized`).

## Critérios de aceite

- [x] Tela "Relatórios" busca `GET /reports?period=month&date=<data
      atual>` ao carregar
- [x] Seletor Semana/Mês/Ano troca o `period` da consulta, mantendo a
      data atual
- [x] "Gasto por categoria" lista os itens de `porCategoria` na ordem
      recebida, com valor formatado e barra proporcional ao maior gasto
      da lista, sempre na cor neutra
- [x] Sem despesa no período, "Gasto por categoria" mostra estado vazio
- [x] Card "Total no período" mostra o valor total formatado
- [x] Comparação com período anterior aparece com sinal e rótulo
      corretos (`+`/`-`X% vs semana/mês/ano passado) quando
      `variacaoPercentual` não é `null`
- [x] Comparação some quando `variacaoPercentual` é `null`
- [x] Card "Maior gasto" mostra categoria, valor e "X% do orçamento"
      quando `percentualOrcamento` não é `null`
- [x] Card "Maior gasto" mostra categoria e valor sem percentual quando
      `percentualOrcamento` é `null`
- [x] Card "Maior gasto" mostra estado vazio quando `maiorGasto` é
      `null`
- [x] Sessão expirada ao carregar o relatório segue o comportamento já
      existente nas demais telas
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando

## Fora do escopo

- Seletor de data (o usuário nunca escolhe uma data de referência
  diferente da atual) — decisão 1, mesma decisão já fechada no
  contrato do backend
- Introdução da lib Tremor / qualquer gráfico além de barras
  proporcionais simples — decisão 5
- Destaque de acima-do-orçamento na barra de "Gasto por categoria" —
  decisão 2, o contrato não traz orçamento por categoria nesta lista
- Interação/navegação a partir de um item de "Gasto por categoria" —
  decisão 6
- Exportação (CSV) do relatório — FEAT-30
- Histórico de múltiplos períodos numa mesma tela (ex.: série de 12
  meses) — o backend só calcula o período consultado e o imediatamente
  anterior (para a variação); fora do escopo tanto do backend quanto
  desta feature
- Qualquer mudança no contrato do backend — `GET /reports` já
  implementa tudo que esta feature consome (backend FEAT-24, já em
  produção)
- Tratamento dedicado de `403` — `GET /reports` é liberado pra
  qualquer papel autenticado (sem 403 possível nesta rota, conforme o
  próprio contrato do backend)
