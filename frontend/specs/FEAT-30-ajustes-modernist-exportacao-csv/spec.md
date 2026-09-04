# FEAT-30: Ajustes (migrar para Modernist + exportação CSV)

## Objetivo

Migrar `SettingsPage` de shadcn/ui + Tailwind para o design system
Modernist (`frontend/design-system/web/screenshots/18-ajustes.png`),
incluindo a ação "Exportar CSV" que consome `GET /transactions/export`
(backend FEAT-25, já em produção). Como parte da mesma migração, mover
a ação de logout ("Sair") do conteúdo da página para o rodapé da
sidebar, reproduzindo o bloco "Sua conta / Sair" do protótipo — algo
que a FEAT-15 (migração do menu) deixou deliberadamente de fora.

## Contexto

Hoje `SettingsPage` (`frontend/app/src/routes/SettingsPage.tsx`) é só
um placeholder shadcn/ui: título "Configurações", botão "Sair" e
`AppVersion` (versão do build publicado, rastreabilidade da FEAT-09,
sem relação com o design). Não existe nenhum botão de exportação.

Referência visual: `frontend/design-system/web/screenshots/
18-ajustes.png`, e a fonte de verdade
`frontend/design-system/web/jrnexpenses-web.dc.html` (bloco `isSet`
para o conteúdo da página, bloco de rodapé da sidebar logo após a lista
de itens de navegação para "Sua conta / Sair"). O design mostra:
título "Ajustes"; linhas "Moeda" (BRL, fixo), "Notificações push" e
"Notificações por e-mail" (toggles); linha "Exportar dados" com o botão
"Exportar CSV". No rodapé da sidebar (fora do conteúdo da página):
avatar com iniciais, "Sua conta" e o link "Sair".

**Decisões de escopo fechadas com o usuário durante este `/specify`:**

1. **Moeda e notificações ficam de fora desta feature.** Nenhum desses
   três elementos do protótipo (`Moeda`, `Notificações push`,
   `Notificações por e-mail`) tem suporte de backend hoje — não existe
   endpoint de preferências de usuário/conta. Em vez de exibi-los como
   elementos estáticos/decorativos, a tela desta feature simplesmente
   não os inclui. Ficam registrados como débito técnico (ver
   `backend/docs/backlog.md`/`frontend/docs/backlog.md`, a confirmar
   com o usuário) para quando existir suporte real.
2. **Logout migra para o rodapé da sidebar.** O protótipo web desenha
   "Sua conta / Sair" fora do bloco de conteúdo "Ajustes" — no rodapé
   da `DesktopSidebar`, abaixo da lista de itens de navegação. Esta
   feature reproduz esse rodapé (avatar com iniciais, "Sua conta",
   "Sair") e remove o botão "Sair" hoje existente dentro de
   `SettingsPage`. Amplia o escopo desta FEAT para tocar
   `components/nav/` (`DesktopSidebar`), algo que a FEAT-15 deixou de
   fora intencionalmente ao migrar só a casca de navegação sem o
   rodapé de conta.
   - **Mobile**: a `MobileBottomNav` não tem uma sidebar persistente —
     o equivalente de "mais opções" é o painel `NavMoreSheet` (aberto
     pelo item "Mais"). Como o protótipo de referência é só o web
     (nenhuma spec anterior deste projeto usa
     `frontend/design-system/mobile/` como fonte), esta feature
     reproduz o mesmo bloco "Sua conta / Sair" no rodapé do
     `NavMoreSheet`, por extensão da decisão acima — necessário para
     não regredir o logout em telas estreitas, já que hoje é o único
     lugar de onde ele é acionado.
3. **Exportação sem seletor de filtro.** O protótipo mostra um único
   botão "Exportar CSV", sem nenhuma UI de filtro (período, categoria,
   tipo). O clique chama `GET /transactions/export` sem nenhum query
   param — sempre exporta todas as transações da conta ativa, mesmo
   comportamento do botão único do design.
4. **Download via chamada autenticada, não link direto.** `GET
   /transactions/export` exige o mesmo `Authorization: Bearer` das
   demais rotas protegidas — como o access token vive em memória (não
   em cookie), um `<a href>` apontando direto pro endpoint não
   enviaria o header. O clique dispara uma chamada autenticada
   (mesmo `httpClient` das demais features), lê a resposta como
   arquivo e aciona o download no navegador com o nome de arquivo
   devolvido pela API (`Content-Disposition`, já fixo em
   `transacoes.csv` conforme o contrato).
5. **Estado de carregamento: botão ocupado, sem overlay.** Durante a
   exportação, o botão "Exportar CSV" segue o padrão mais simples já
   usado em `CategoryForm`/`TransactionForm` (rótulo em gerúndio +
   `disabled`, sem spinner dedicado) — não o `ProcessingOverlay`
   (componente que já existe desde a FEAT-28, resolvendo o débito
   técnico do backlog que constava como pendente até então). O
   protótipo não tem nenhuma tela de "exportando" dedicada (diferente
   de `08-salvando-despesa-loading.png`/`16-enviando-convite-loading.
   png`), então esta ação segue o padrão mais simples de botão ocupado,
   coerente com uma ação de um clique só (sem modal), em vez do overlay
   reservado a fluxos de formulário mais longos.
6. **Sucesso em toast, erro inline — mesmo padrão da FEAT-28.** O
   componente `Toast` genérico já existe (FEAT-28, resolveu o débito
   técnico do backlog) e é usado em `MembersPage` só para sucesso,
   nunca para erro. Esta feature segue o mesmo padrão: exportação
   concluída mostra o toast "Transações exportadas." (auto-some depois
   de alguns segundos); sessão expirada aciona o mesmo fluxo já usado
   nas demais telas (limpa sessão, redireciona pro login); falha de
   rede mostra mensagem de erro inline (`role="alert"`), sem toast.
   Como o client nunca envia filtro nenhum, não há cenário esperado de
   erro de validação (`400`) em uso normal.

## Requisitos de negócio

- A rota de Ajustes usa o design system Modernist (`.ds-modernist`),
  substituindo shadcn/ui + Tailwind nessa tela
- A tela mostra o título "Ajustes" e a linha "Exportar dados" com o
  botão "Exportar CSV" — sem as linhas de Moeda/Notificações (decisão
  1)
- `AppVersion` (versão do build publicado) continua visível na tela,
  migrada visualmente para o Modernist — não faz parte do design, mas
  é uma exigência de rastreabilidade já existente (FEAT-09), não
  removida por esta feature
- Clicar em "Exportar CSV" chama `GET /transactions/export` sem
  nenhum query param, via chamada autenticada (`Authorization: Bearer`
  do token em memória)
- Uma resposta de sucesso (`200`, `text/csv`) é salva como arquivo no
  navegador do usuário, com o nome de arquivo indicado pela API
  (`transacoes.csv`), e mostra o toast "Transações exportadas."
  (componente `Toast` genérico, mesmo padrão de sucesso já usado em
  `MembersPage`)
- Enquanto a exportação está em andamento, o botão mostra o estado de
  "ocupado" (rótulo em gerúndio, ex.: "Exportando…", sem spinner — ver
  decisão 5) e fica desabilitado, sem permitir novo clique concorrente
- Sessão expirada (`401` sem refresh possível) durante a exportação
  aciona o mesmo comportamento já existente nas demais telas (limpa
  sessão, redireciona pro login)
- Falha de rede durante a exportação mostra uma mensagem de erro
  inline, sem interromper o restante da tela
- A `DesktopSidebar` passa a ter um rodapé "Sua conta / Sair" (avatar
  com iniciais do usuário, nome/label "Sua conta", ação "Sair"),
  reproduzindo o mesmo botão de logout já existente hoje (limpa
  sessão, redireciona pro login)
- O `NavMoreSheet` (painel "Mais" do mobile) passa a ter o mesmo
  rodapé "Sua conta / Sair", com o mesmo comportamento de logout
- `SettingsPage` deixa de ter um botão "Sair" próprio — a única ação
  de logout do app passa a viver no rodapé da sidebar (desktop) e no
  `NavMoreSheet` (mobile)

## User Stories

**US1 — Ver a tela Ajustes migrada**
- Given um usuário autenticado
- When ele navega para "Ajustes"
- Then a tela mostra o título "Ajustes" e a linha "Exportar dados" com
  o botão "Exportar CSV", com a aparência visual do Modernist (sem
  nenhum componente shadcn/ui/Tailwind remanescente)

**US2 — Exportar CSV com sucesso**
- Given um usuário autenticado na tela "Ajustes"
- When ele clica em "Exportar CSV" e a API responde `200` com o CSV
- Then o navegador salva um arquivo `transacoes.csv` com o conteúdo
  retornado pela API, a tela mostra o toast "Transações exportadas.",
  e o botão volta ao estado normal

**US3 — Estado de carregamento durante a exportação**
- Given um usuário que acabou de clicar em "Exportar CSV"
- When a requisição a `GET /transactions/export` ainda está em
  andamento
- Then o botão mostra rótulo em gerúndio ("Exportando...") e fica
  desabilitado, impedindo um segundo clique até a requisição terminar

**US4 — Sessão expirada ao exportar**
- Given um usuário cuja sessão expirou
- When ele clica em "Exportar CSV"
- Then o comportamento já existente de sessão expirada se aplica
  (limpa a sessão, redireciona pro login), mesmo padrão já usado nas
  demais telas

**US5 — Erro de rede ao exportar**
- Given um usuário autenticado na tela "Ajustes"
- When ele clica em "Exportar CSV" e a requisição falha por erro de
  rede
- Then a tela mostra uma mensagem de erro inline, sem baixar nenhum
  arquivo, e o botão volta ao estado normal (permite tentar de novo)

**US6 — Logout a partir da sidebar (desktop)**
- Given um usuário autenticado em qualquer tela do app, em uma janela
  larga o suficiente para mostrar a `DesktopSidebar`
- When ele clica em "Sair" no rodapé da sidebar
- Then a sessão é limpa e ele é redirecionado pro login, mesmo
  comportamento hoje disparado pelo botão "Sair" de `SettingsPage`

**US7 — Logout a partir do painel "Mais" (mobile)**
- Given um usuário autenticado em uma janela estreita (layout mobile,
  `MobileBottomNav`)
- When ele abre o painel "Mais" e clica em "Sair"
- Then a sessão é limpa e ele é redirecionado pro login, mesmo
  comportamento do item anterior

**US8 — Versão do app continua visível**
- Given um usuário autenticado na tela "Ajustes"
- When a tela termina de carregar
- Then a versão do build publicado continua visível na tela (mesmo
  texto/formato já usado por `AppVersion` antes desta feature)

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo em
`backend/specs/FEAT-25-exportacao-csv-transacoes/spec.md`. Esta feature
consome sempre sem filtro (decisão 3) — os query params opcionais do
contrato (`tipo`, `categoryId`, `yearMonth`, `dateFrom`, `dateTo`,
`minAmountInCents`, `maxAmountInCents`) não são usados por esta tela.

### GET /transactions/export

Response `200`:
- `Content-Type: text/csv; charset=utf-8`
- `Content-Disposition: attachment; filename="transacoes.csv"`
- Corpo (UTF-8 com BOM, delimitador `;`, quebra de linha `\r\n`):
```csv
data;descricao;categoria;tipo;valor;lancadoPor
2026-08-15;Almoço no restaurante;Alimentacao;despesa;45,90;Você
2026-08-10;Salário;Renda;receita;5000,00;Você
```

Sem filtro nenhum (decisão 3), não há cenário esperado de `400`
(`validation-error`) em uso normal. `401` (`unauthorized`) segue o
comportamento padrão já existente (US4).

Endpoint de logout (`POST /auth/logout`) já existente desde a
FEAT-01/FEAT-12, sem nenhuma mudança de contrato — esta feature só move
de onde ele é acionado na UI (decisão 2).

## Critérios de aceite

- [x] Tela "Ajustes" migrada para o Modernist, com título "Ajustes" e
      a linha "Exportar dados" / botão "Exportar CSV" (sem
      Moeda/Notificações)
- [x] `AppVersion` continua visível na tela, sem quebrar o teste já
      existente de rastreabilidade (FEAT-09)
- [x] Clicar em "Exportar CSV" chama `GET /transactions/export` sem
      query params, salva o arquivo retornado no navegador e mostra o
      toast "Transações exportadas."
- [x] Botão "Exportar CSV" mostra estado de carregamento (rótulo em
      gerúndio "Exportando..." + desabilitado, sem spinner — mesmo
      padrão mais simples de `CategoryForm`/`TransactionForm`, ver
      decisão 5 e `plan.md`) durante a requisição
- [x] Sessão expirada durante a exportação aciona o fluxo padrão já
      existente (limpa sessão; redirect pro login é global, via
      `ProtectedRoute`, não algo que a página aciona sozinha)
- [x] Erro de rede durante a exportação mostra mensagem inline, sem
      travar a tela, permitindo nova tentativa
- [x] `DesktopSidebar` tem um rodapé "Sua conta / Sair" funcional
      (mesmo efeito de logout do botão removido de `SettingsPage`)
- [x] `NavMoreSheet` (mobile) tem o mesmo rodapé "Sua conta / Sair"
      funcional
- [x] `SettingsPage` não tem mais nenhum botão "Sair" próprio
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima,
      100% dos testes passando (542/542 — 1 falha isolada de
      `InviteMemberDialog.test.tsx` confirmada como flaky
      pré-existente, arquivo não tocado por esta feature)
- [ ] Revisão manual/visual no app real (task 12 do `tasks.md`) —
      pendente, o usuário confere quando subir o ambiente local

## Fora do escopo

- Linhas "Moeda", "Notificações push" e "Notificações por e-mail" do
  protótipo — decisão 1, sem suporte de backend hoje
- Qualquer seletor de filtro (período, categoria, tipo) na exportação
  — decisão 3, sempre exporta tudo, mesmo botão único do protótipo
- `ProcessingOverlay` na exportação — decisão 5, o componente já existe
  (FEAT-28) mas não se aplica a uma ação de um clique só sem modal
- Atalhos "Categorias e orçamentos" / "Membros e convites" dentro de
  Ajustes — existem só no protótipo **mobile** (`design-system/
  mobile/`), que não é referência usada por nenhuma spec deste
  projeto (só o protótipo web é fonte de verdade)
- Qualquer mudança no contrato do backend — `GET /transactions/export`
  já implementa tudo que esta feature consome (backend FEAT-25, já em
  produção)
- Preferências de conta persistidas (moeda, notificações) — não existe
  endpoint de backend para isso hoje
