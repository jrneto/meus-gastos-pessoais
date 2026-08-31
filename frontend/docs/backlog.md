# Backlog — Frontend para o Design System (Modernist) e aderência ao backend

Sequência combinada em 2026-08-29 para o frontend alcançar tudo que o
design system (`frontend/design-system/web/screenshots/`) já assume e
tudo que o backend já expõe (`backend/specs/FEAT-19` em diante —
multi-tenant, membros/permissões, categorias com tipo/orçamento,
transações receita/despesa, resumo mensal, relatórios, exportação CSV,
perfil no cadastro). Cada linha vira uma `spec.md` própria em
`frontend/specs/{FEAT-XX-nome}/` via `/specify`, seguindo o fluxo normal
(`/specify` → `/plan` → `/tasks` → implementar → `/review`). Ver
`frontend/docs/README.md` para o processo.

**Como usar este arquivo:** ao terminar uma FEAT (implementada, testada
e revisada), marcar o checkbox correspondente antes de seguir pra
próxima. Não pular a ordem — cada uma depende da anterior (ou do
backend correspondente, já pronto) conforme a coluna "Depende de".

## Estado atual (contexto para o `/plan` de cada FEAT)

- **Já migrado para Modernist e funcional:** login, menu/navegação
  (shell), transações **de despesa** (listagem, filtros, popup nova/
  editar despesa, popup de detalhe), categorias (CRUD, sem tipo/
  orçamento ainda) — FEAT-14 a FEAT-20.
- **Ainda placeholder/fake, sem chamada de API real:**
  - Cadastro (`SignupComingSoonPage`) — só devolve pro login
  - Início/Dashboard (`HomePage`) — "Em breve"
  - Relatórios (`ReportsComingSoonPage`) — "Em breve"
  - Membros/convites — nem existe rota/feature hoje
- **Ainda em shadcn/ui + Tailwind (não migrado):** Ajustes
  (`SettingsPage`)
- **Modelo desatualizado em relação ao backend:** `features/expenses`
  só conhece despesa (não receita), categoria sem `tipo`/
  `orcamentoMensalCents`, nenhuma feature de membros/permissões
- Cada FEAT abaixo migra visual (Modernist) **e** integra com o
  endpoint real do backend na mesma tacada — não faz sentido lançar UI
  nova sobre o contrato antigo (`/expenses`) quando o backend já expõe
  o contrato novo (`/transactions`)

## Sequência

- [x] **FEAT-21 — Cadastro real (substituir `SignupComingSoonPage`)**
  Implementa a tela de "Criar conta" do design (`02-criar-conta.png`,
  `03-criar-conta-preenchida.png`) integrada a `POST /auth/register`,
  incluindo os campos novos exigidos pelo backend: nome, telefone e
  CPF (com validação de dígito verificador no client, espelhando a do
  backend). Remove `SignupComingSoonPage`.
  Depende de: backend FEAT-26 (perfil no cadastro) — já pronto.

- [x] **FEAT-22 — Categorias: tipo (despesa/receita) e orçamento mensal**
  Estende o CRUD de categorias (`13-categorias-orcamentos.png`) com
  campo obrigatório `tipo` e campo opcional de orçamento mensal;
  listagem/filtro por tipo; exibição do orçamento e indicador de
  consumo por categoria como no design.
  Depende de: backend FEAT-21 (categoria tipo/orçamento) — já pronto.

- [x] **FEAT-23 — Transações: generalizar despesa para receita/despesa**
  Renomeia a feature `expenses` → `transactions` (rota `/transactions`
  no lugar de `/expenses`, filtro `?tipo=`), atualiza listagem/popups
  existentes pra trabalhar com os dois tipos e exibir "Lançado por:
  Você" / nome de quem lançou (`11-transacoes.png`). Maior mudança de
  contrato desta leva — outras FEATs de transação (24, 25, 26, 27)
  dependem desta.
  Depende de: backend FEAT-22 (transações) — já pronto; frontend
  FEAT-22 (categoria com tipo, usada no formulário).

- [x] **FEAT-24 — Popup de nova receita**
  Adiciona o fluxo de lançar receita (`10-nova-receita.png`),
  reaproveitando o popup unificado de nova transação da FEAT-23 com o
  seletor de tipo já visível no design.
  Depende de: frontend FEAT-23.

- [x] **FEAT-25 — Detalhe de transação (generalizar p/ receita)**
  Ajusta o popup de detalhe (hoje só despesa, da FEAT-20) para exibir
  receitas também, conforme `19-detalhe-transacao.png`.
  Depende de: frontend FEAT-23.
  **Fechada sem código/spec em 2026-08-30**, durante o `/specify`:
  conferindo `jrnexpenses-web.dc.html` (fonte de verdade), o popup de
  detalhe não tem nenhuma cor/ícone diferente por tipo — o tile de
  categoria é sempre neutro, igual ao que já existia. Título, cor e
  sinal do valor por tipo (tudo que o design realmente pede) já foram
  entregues antecipadamente na FEAT-24 (`TransactionDetailDialog`).
  Gaps remanescentes contra o `.dc.html` (data por extenso, rótulo
  "Observação"/fallback, divisor) não têm relação com despesa/receita
  — registrados como débito técnico separado, abaixo.

- [x] **FEAT-26 — Dashboard (Início)**
  Substitui `HomePage` pela tela de resumo mensal (`05-dashboard.png`):
  saldo, receitas, gastos, orçamento total, restante, gasto por
  categoria, últimos lançamentos — consumindo `GET /summary?month=`.
  Depende de: backend FEAT-23 (resumo mensal) — já pronto; frontend
  FEAT-23.

- [ ] **FEAT-27 — Relatórios**
  Substitui `ReportsComingSoonPage` pela tela de relatórios por período
  (`12-relatorios.png`): gasto por categoria, total do período,
  variação vs período anterior, maior gasto — consumindo `GET
  /reports?period=`. Primeira feature do frontend a precisar de
  gráfico — introduzir Tremor aqui (ver `constitution.md`).
  Depende de: backend FEAT-24 (relatórios) — já pronto; frontend
  FEAT-23.

- [ ] **FEAT-28 — Membros da conta e convites**
  Nova feature `members`: listagem de membros (`14-membros.png`),
  convite por e-mail (`15-convidar-pessoa.png`, com loading
  `16-enviando-convite-loading.png` e toast de sucesso
  `17-toast-convite-enviado.png`), remoção de membro, exibição do nível
  de acesso (`Leitura`/`Lançar`/`Total`).
  Depende de: backend FEAT-20 (membros/convites/permissões) — já
  pronto.

- [ ] **FEAT-29 — Aplicar permissões por role na UI**
  Com FEAT-28 trazendo o nível de acesso do usuário logado, esconder/
  desabilitar ações conforme role: `Leitura` sem botões de
  lançar/editar/excluir; `Lançar` só edita/exclui o que criou (já
  refletido pelo backend, mas a UI hoje não trata o erro 403 nem
  esconde a ação de antemão); `Total` sem restrição, incluindo edição
  de orçamento de categoria (FEAT-22).
  Depende de: frontend FEAT-22, FEAT-23, FEAT-28.

- [ ] **FEAT-30 — Ajustes (migrar para Modernist + exportação CSV)**
  Migra `SettingsPage` de shadcn/ui para Modernist (`18-ajustes.png`),
  incluindo o botão "Exportar CSV" consumindo `GET
  /transactions/export`.
  Depende de: backend FEAT-25 (exportação CSV) — já pronto; frontend
  FEAT-23.

## Débitos técnicos e melhorias futuras

- **Componente de toast genérico (Modernist)** — levantado durante o
  `/plan` da FEAT-21 (cadastro real). O design já assume toasts de
  confirmação em pelo menos 3 telas
  (`design-system/web/screenshots/09-toast-despesa-lancada.png`,
  `17-toast-convite-enviado.png`, e o próprio fluxo de cadastro), mas
  nenhum componente de toast existe hoje no código — cada tela usa o
  padrão inline de sucesso/erro (`<p role="alert">`). A FEAT-21 seguiu
  com o padrão inline por decisão do usuário, para não aumentar escopo.
  Relevante para FEAT-24 (nova receita) e FEAT-28 (membros/convite) do
  backlog abaixo, que também assumem toast no design — vale resolver
  uma vez, de forma genérica, antes delas.
- **Overlay de processamento de tela cheia (Modernist)** — levantado no
  mesmo `/plan` da FEAT-21. O design mostra um véu sobre o fundo com
  spinner e barra de progresso indeterminada para ações mais longas
  (`04-login-processando.png`, `08-salvando-despesa-loading.png`,
  `16-enviando-convite-loading.png`), também inexistente no código hoje
  — só o padrão de botão ocupado (spinner + label em gerúndio +
  `disabled`) é usado. Mesma relevância futura que o item acima.
- **Fidelidade visual do popup de detalhe de transação** (`Transaction
  DetailDialog`) — levantado durante o `/specify` da FEAT-25, ao
  conferir `jrnexpenses-web.dc.html` (bloco `isViewingTx`) contra a
  implementação atual (título/cor/sinal por tipo já corretos desde a
  FEAT-24). Três gaps sem relação com despesa/receita, válidos pros
  dois tipos:
  - Data exibida crua (`2025-06-15`) em vez de formatada por extenso
    (`this.formatDateLong`, ex.: "15 de junho de 2025")
  - Campo rotulado "Descrição" em vez de "Observação", sem fallback
    "Sem observação" quando vazio (hoje não é um problema prático
    porque `description` é obrigatório no schema, mas o rótulo diverge
    do design)
  - Falta o divisor (`<div class="hr">`) entre a seção "Lançado por" e
    a de observação/descrição
  Nenhum desses existe desde a FEAT-20 (popup de detalhe original, só
  despesa) — não é regressão de nenhuma feature recente.

