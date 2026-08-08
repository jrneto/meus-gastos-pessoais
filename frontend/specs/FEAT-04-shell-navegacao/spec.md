# FEAT-04: Navegação e menu (shell da aplicação)

## Objetivo
Criar a estrutura de navegação (menu) do GastosApp: um shell estrutural
compartilhado por todas as telas pós-login, que organiza o acesso aos
módulos do sistema (Despesas hoje; Relatórios, Categorias e outros no
futuro) em uma hierarquia única, apresentada como sidebar colapsável em
telas largas e bottom navigation em telas estreitas — mesma árvore de
navegação, duas apresentações responsivas da mesma SPA (não há app
mobile nativo).

## Contexto
Hoje não existe navegação estruturada: `RegisterExpensePage` (FEAT-02) e
`ExpensesListPage` (FEAT-03) têm cada uma seu próprio cabeçalho, com um
link direto para a outra tela e um botão "Sair" duplicado — solução
deliberadamente temporária, registrada nos planos daquelas features,
até esta feature chegar (ver `frontend/specs/FEAT-03-listagem-despesas/plan.md`).

Esta feature substitui esses cabeçalhos por um shell único. Ela cobre
somente a estrutura de navegação em si — a tela de "Início" existe como
item de menu e placeholder vazio, mas seu conteúdo real (visão geral,
resumos, atalhos) é escopo de uma feature futura separada. Da mesma
forma, "Relatórios" e "Categorias" aparecem no menu como módulos
futuros ainda não implementados.

Como parte desta mudança, a rota raiz (`/`) deixa de mostrar o
formulário de cadastro de despesas e passa a mostrar o placeholder de
Início; o cadastro de despesas (FEAT-02) migra para uma rota própria
dentro do módulo Despesas. A listagem de despesas (FEAT-03) continua
em `/expenses`, agora alcançada pelo menu em vez de um link avulso.

## Requisitos de negócio
- A navegação é organizada por **módulos**, não por telas soltas. A
  hierarquia desta feature:
  - **Início** — placeholder, sem funcionalidade própria ainda
  - **Despesas** (módulo com subitens)
    - Nova despesa — formulário de cadastro (FEAT-02), migrado de `/`
    - Listagem / Filtros — consulta de despesas (FEAT-03), inalterada
      em `/expenses`
  - **Relatórios** — módulo futuro, visível porém **desabilitado** (não
    clicável, sem navegação)
  - **Categorias** — módulo futuro, visível porém **desabilitado** (não
    clicável, sem navegação)
  - **Configurações** — placeholder navegável; é também onde vive a
    ação de logout ("Sair"), substituindo o botão que hoje existe
    duplicado nos cabeçalhos de Despesas
- Itens desabilitados (Relatórios, Categorias) são visualmente
  diferenciados dos itens ativos (ex.: menor contraste/opacidade) e não
  respondem a clique/toque — nenhuma navegação ocorre
- Itens placeholder navegáveis (Início, Configurações) levam a uma tela
  mínima própria (não uma tela em branco sem contexto, nem reaproveitam
  o conteúdo de outra tela)
- O item correspondente à rota atual é destacado visualmente no menu
- **Apresentação web** (tela larga): sidebar lateral, com opção de
  colapsar/expandir; colapsada, mantém os itens navegáveis (ícones),
  sem exigir expandir para trocar de tela
- **Apresentação mobile** (tela estreita): bottom navigation bar com
  até 4–5 itens principais mais visíveis; itens que não cabem ficam
  agrupados sob um item "Mais", que dá acesso ao restante da hierarquia
- A troca entre apresentação web e mobile responde ao tamanho da tela
  (mesma SPA, mesma sessão, sem recarregar ou perder estado de
  navegação)
- O shell só é renderizado com sessão válida — mesma proteção já
  existente (`ProtectedRoute`, FEAT-01); sem sessão, redireciona para
  `/login`
- Logout a partir de Configurações encerra a sessão e redireciona para
  `/login`, mesmo comportamento já estabelecido nas features anteriores
- Sessão expirada durante o uso de uma tela (ex.: 401 ao listar
  despesas) continua tratada pela própria feature (FEAT-02/FEAT-03),
  sem mudança de comportamento nesta feature — o shell não introduz
  nem substitui esse tratamento

## User stories

### Visualizar o menu em tela larga (web)
Given um usuário autenticado em uma tela larga (desktop)
When ele acessa qualquer tela pós-login
Then vê uma sidebar lateral com a hierarquia completa (Início, Despesas
com seus subitens, Relatórios, Categorias, Configurações), com
Relatórios e Categorias visivelmente desabilitados

### Visualizar o menu em tela estreita (mobile)
Given um usuário autenticado em uma tela estreita (mobile)
When ele acessa qualquer tela pós-login
Then vê uma bottom navigation com os itens principais e um item "Mais"
agregando o restante da hierarquia

### Colapsar e expandir a sidebar (web)
Given um usuário autenticado em tela larga com a sidebar expandida
When ele aciona o controle de colapsar
Then a sidebar reduz para exibir só os ícones, permanecendo navegável;
acionar novamente expande de volta

### Navegar para Nova despesa
Given um usuário autenticado em qualquer tela do shell
When ele seleciona "Nova despesa" no menu (ou, no mobile, dentro de
"Mais" se não estiver entre os itens principais)
Then a aplicação navega para o formulário de cadastro de despesas
(FEAT-02) e o item correspondente é destacado no menu

### Navegar para Listagem de despesas
Given um usuário autenticado em qualquer tela do shell
When ele seleciona "Listagem / Filtros" no menu
Then a aplicação navega para `/expenses` (FEAT-03) e o item
correspondente é destacado no menu

### Tentar acessar um módulo futuro desabilitado
Given um usuário autenticado vendo o menu
When ele tenta clicar/tocar em "Relatórios" ou "Categorias"
Then nada acontece — nenhuma navegação ocorre, o item permanece
visivelmente desabilitado

### Acessar Início
Given um usuário autenticado
When ele seleciona "Início" no menu
Then a aplicação navega para `/` e exibe o placeholder de Início (sem
funcionalidade própria ainda), com o item destacado no menu

### Acessar Configurações e sair
Given um usuário autenticado
When ele seleciona "Configurações" no menu e aciona "Sair"
Then a sessão é encerrada e a aplicação redireciona para `/login`

### Acesso sem sessão válida
Given um usuário sem sessão válida
When ele tenta acessar qualquer rota protegida pelo shell
Then a aplicação redireciona para `/login` (comportamento herdado de
`ProtectedRoute`, FEAT-01, inalterado)

## Contratos da API observáveis
Nenhum. Esta feature é exclusivamente de navegação/UI no frontend — não
introduz nem altera chamadas HTTP. Reaproveita as telas e integrações
já existentes (`POST /expenses`, `GET /expenses`, FEAT-02/FEAT-03).

## Critérios de aceite
- [x] Menu exibe a hierarquia completa: Início, Despesas (Nova despesa
      + Listagem/Filtros), Relatórios, Categorias, Configurações
- [x] Relatórios e Categorias aparecem visivelmente desabilitados e não
      respondem a clique/toque
- [x] Item correspondente à rota atual é destacado no menu
- [x] Em tela larga, sidebar lateral colapsável, navegável tanto
      expandida quanto colapsada
- [x] Em tela estreita, bottom navigation com itens principais + item
      "Mais" agregando o restante da hierarquia
- [x] Rota raiz (`/`) exibe o placeholder de Início; cadastro de
      despesas passa a viver em rota própria dentro de Despesas
- [x] Listagem de despesas continua acessível em `/expenses`, agora via
      menu
- [x] Configurações é navegável e contém a ação "Sair", que encerra a
      sessão e redireciona para `/login`
- [x] Cabeçalhos duplicados de `RegisterExpensePage`/`ExpensesListPage`
      (links de navegação cruzada + botão "Sair" locais) são removidos,
      substituídos pelo shell
- [x] Acesso sem sessão válida a qualquer rota do shell redireciona
      para `/login`
- [x] Comportamento de sessão expirada durante o uso (401) continua
      funcionando como antes, sem regressão

## Fora do escopo
- Conteúdo real da tela de Início (resumos, atalhos, gráficos) —
  feature futura separada
- Implementação de Relatórios e Categorias — só o item de menu
  desabilitado nesta feature
- Preferências reais em Configurações — nesta feature, Configurações
  só contém a ação de logout
- App mobile nativo — "mobile" aqui é a mesma SPA web em tela estreita
- Persistência do estado de colapso da sidebar entre sessões/reloads
- Novos módulos (Receitas, Metas, Cartões etc.) — a extensibilidade da
  estrutura de navegação para recebê-los é um requisito técnico deste
  shell (detalhado em `plan.md`), mas nenhum módulo novo é criado nesta
  feature além dos listados acima

## Status

Implementado. `navConfig.ts` (`NAV_TREE`/`flattenNavItems`),
`NavItemRow`, `DesktopSidebar`, `MobileBottomNav`, `NavMoreSheet`,
`AppShell` (rota de layout), `routes/HomePage.tsx`,
`routes/SettingsPage.tsx` implementados conforme `plan.md`.
`RegisterExpensePage`/`ExpensesListPage` simplificados (perderam
cabeçalho/logout/link próprios); `app/router.tsx` reestruturado com
`/` → Início, `/expenses/new` → cadastro, `/expenses` → listagem,
`/settings` → configurações + logout, todos dentro de `AppShell`.
Único componente novo instalado: shadcn `sheet`.

Suíte completa (`npm test`) passa: 82/82 testes. `tsc -b`, `vite build`
e `oxlint` sem erros novos (mesmos dois warnings pré-existentes/aceitos
de antes desta feature).

Validação manual: fluxo completo (sidebar em tela larga, colapsar/
expandir, bottom nav + "Mais" em tela estreita, navegação entre
Início/Nova despesa/Listagem/Configurações, item ativo destacado,
Relatórios/Categorias não-clicáveis, logout) validado pelo usuário com
o backend real — feature confirmada funcionando ponta a ponta.
