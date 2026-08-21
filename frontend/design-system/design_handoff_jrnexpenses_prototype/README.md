# Handoff: jrnexpenses — Controle de Despesas Pessoais (Protótipo Mobile)

## Visão geral
Um protótipo mobile clicável do jrnexpenses, um app de controle de despesas pessoais (com potencial para crescer em uma ferramenta compartilhada/marketplace). Cobre onboarding, autenticação, dashboard, lista de transações, fluxo de adicionar despesa, detalhe de transação, categorias e orçamentos, relatórios e ajustes.

## Sobre os arquivos de design
Os arquivos deste pacote são **referências de design construídas em HTML** (um único arquivo de protótipo interativo, `jrnexpenses.dc.html`, mais seus scripts/estilos de suporte) — eles demonstram a aparência, o conteúdo e o fluxo de interação pretendidos. **Não são código de produção para copiar diretamente**. A tarefa é **recriar estes designs no ambiente do código-base de destino** (ex.: React Native, Flutter, SwiftUI, Kotlin/Compose, ou uma stack web) usando os próprios padrões de componentes, navegação e gerenciamento de estado desse ambiente — ou, se ainda não existir um app, escolher o framework mais adequado e implementar do zero. Apenas a lógica de interação, o layout e a linguagem visual devem ser portados — não a marcação bruta.

## Fidelidade
**Alta fidelidade.** Cores, tipografia, espaçamento e texto são finais conforme o design system Modernist (ver Tokens de Design abaixo). Recrie a interface de perto usando os componentes/sistema de estilo nativos da plataforma de destino, respeitando esses tokens.

## Telas / Views

Todas as telas são renderizadas dentro de um frame mobile de 402×874 com uma barra de abas inferior persistente (Dashboard / Transações / Relatórios / Ajustes) após onboarding+login, além de um botão de ação flutuante "+" que abre "Adicionar despesa" a partir de qualquer ponto do fluxo de abas.

### 1. Onboarding (3 slides)
- **Propósito**: introdução inicial às propostas de valor do app.
- **Layout**: tela cheia, bloco de texto alinhado à esquerda e centralizado verticalmente; botão ghost "Pular" no canto superior direito (oculto no slide 3); 3 pontos quadrados de progresso acima de um botão primário de bloco no rodapé ("Próximo" nos slides 1–2, "Começar" no slide 3).
- **Conteúdo**:
  1. Kicker "01 / 03", H1 "Registre cada gasto em segundos", corpo "Adicione uma despesa, escolha a categoria e pronto."
  2. Kicker "02 / 03", H1 "Acompanhe por categoria", corpo "Veja para onde vai o seu dinheiro e defina orçamentos mensais."
  3. Kicker "03 / 03", H1 "Relatórios claros, sem enrolação", corpo "Gráficos simples para entender seus hábitos de consumo."
- Sem ilustrações — apenas tipográfico/arquitetônico, conforme a direção do design system.

### 2. Login / Cadastro
- **Propósito**: ponto de entrada de autenticação.
- **Layout**: wordmark alinhada à esquerda "jrn." (ponto final em cor de acento) + subtítulo em versalete "expenses"; um controle segmentado (Entrar / Criar conta) alterna o modo do formulário; campos empilhados (Nome exibido apenas no modo cadastro, E-mail, Senha); link ghost "Esqueceu a senha?" (apenas no modo login); botão primário de bloco rotulado "Entrar" ou "Criar conta"; nota legal pequena no rodapé.
- Submeter (em qualquer modo) navega para a aba Dashboard.

### 3. Dashboard (aba: Início)
- **Propósito**: resumo mensal rápido.
- **Layout**: H1 "Resumo" + rótulo do mês ("Agosto de 2026"). Um card mostra o total gasto (grande, em negrito), "de R$X planejados", uma barra de progresso fina e o valor restante. Seção "Por categoria": uma linha por categoria — selo quadrado com letra, nome, texto gasto/orçamento, barra de progresso fina (a barra e o texto do valor mudam para vermelho de acento quando o orçamento é excedido). Seção "Últimas despesas": últimas 5 transações em linhas (selo com letra, descrição, categoria · data, valor em accent-700), com link "Ver todas →" para a aba Transações.

### 4. Transações (aba: Transações)
- **Propósito**: histórico completo de transações com filtro por categoria.
- **Layout**: H1 "Transações". Chips de filtro com rolagem horizontal: "Todas" + um por categoria (o chip ativo inverte para preenchido escuro/claro). Lista de linhas de transação (selo com letra, descrição, categoria · data, valor, chevron). Texto de estado vazio "Nenhuma despesa nesta categoria." quando um filtro não retorna resultados. Tocar em uma linha abre o Detalhe da Transação.

### 5. Relatórios (aba: Relatórios)
- **Propósito**: detalhamento de gastos.
- **Layout**: H1 "Relatórios". Controle segmentado: Semana / Mês / Ano (altera o texto de comparação). Card de resumo: total do período + texto de comparação (ex.: "+12% vs mês passado"). Gráfico de barras horizontais: uma linha por categoria, ordenadas por gasto decrescente (nome da categoria, barra dimensionada em relação à categoria com maior gasto, valor). Card "Maior gasto" destacando a categoria principal com % do seu orçamento.

### 6. Ajustes (aba: Ajustes)
- **Propósito**: preferências de conta e do app.
- **Layout**: H1 "Ajustes". Linha de perfil (avatar quadrado com iniciais "VC", rótulo da conta, e-mail). Divisória. Lista de linhas: Moeda (BRL R$, estático), "Categorias e orçamentos" (abre o overlay de Categorias), alternância "Notificações push", alternância "Notificações por e-mail", "Exportar dados" (linha estática com chevron). Botão ghost "Sair" (texto accent-700) no rodapé retorna à tela de Login.

### 7. Adicionar Despesa (overlay em tela cheia)
- **Propósito**: registrar uma nova despesa.
- **Layout**: linha superior: botão ghost "Cancelar", título centralizado "Nova despesa". Campo de valor grande sem borda (34px em negrito, apenas borda inferior) com placeholder "R$ 0,00". Grade de 3 colunas com blocos de categoria (letra + nome; o bloco selecionado ganha borda de acento + tingimento accent-100). Campo de data (input nativo de data). Campo de texto "Observação". "Comprovante (opcional)" — um slot de arrastar/anexar imagem (140px de altura). Botão primário de bloco "Salvar despesa" confirma o lançamento e retorna à aba anterior.

### 8. Detalhe da Transação (overlay em tela cheia)
- **Propósito**: visualizar/gerenciar uma única transação.
- **Layout**: linha superior: botão de ícone de seta para trás (esquerda), título "Detalhe" (centro), botão de ícone de lixeira (direita, accent-700) para excluir. Valor grande (accent-700, 36px em negrito) + data completa. Linha de categoria (selo com letra + nome). Divisória. Rótulo "Observação" + texto (ou "Sem observação"). Rótulo "Comprovante" + slot de imagem (mostra o comprovante anexado ou o placeholder "Sem comprovante"). Botão secundário de bloco "Fechar".

### 9. Categorias e Orçamentos (overlay em tela cheia, aberto a partir de Ajustes)
- **Propósito**: revisar/editar orçamentos mensais por categoria.
- **Layout**: seta para trás + título "Categorias e orçamentos". Um bloco por categoria: selo com letra + nome, texto gasto/orçamento (accent-700 quando o orçamento é excedido), barra de progresso, e um link ghost "Editar orçamento" ou (em edição) um input numérico + botão primário "Salvar" inline. Botão secundário de bloco "Fechar" no rodapé.

## Interações e comportamento
- A barra de abas alterna o conteúdo visível da aba; o ícone/rótulo da aba ativa são renderizados em `--color-text`, os inativos em `--color-neutral-500`.
- O FAB "+" abre o overlay de Adicionar Despesa a partir de qualquer aba (oculto enquanto qualquer overlay estiver aberto).
- Adicionar Despesa: selecionar um bloco de categoria atualiza a borda/preenchimento do bloco ativo; Salvar adiciona uma nova transação à lista, ordenada nas views de transações/dashboard, e fecha o overlay. Valor vazio ou zero é tratado como cancelamento sem efeito.
- Linhas de transação (lista "recentes" do dashboard e lista completa de transações) abrem o overlay de Detalhe da Transação daquele item.
- Excluir uma transação a remove e fecha o overlay de detalhe.
- Os chips de filtro de categoria em Transações alternam ligado/desligado (tocar no chip ativo limpa o filtro); a filtragem é feita no cliente sobre a lista de transações.
- O controle segmentado de período em Relatórios altera apenas o texto de comparação neste protótipo — conecte a agregação real por período em produção.
- As alternâncias de Ajustes (notificações push/e-mail) são simples interruptores ligado/desligado sem persistência além do estado da sessão.
- Editar o orçamento de uma categoria: tocar em "Editar orçamento" revela um input numérico + Salvar inline; Salvar confirma o novo orçamento e recalcula todas as porcentagens de gasto/orçamento e as cores das barras.
- Qualquer barra de progresso ou valor de categoria/relatório cujo gasto exceda o orçamento muda de tinta (`--color-neutral-800` / `--color-text`) para o vermelho de acento (`--color-accent` / `--color-accent-700`) — este é o único sinal de orçamento excedido; nenhum ícone ou selo é usado.
- "Sair" (Ajustes) retorna à tela de Login, reiniciando na aba Dashboard para o próximo login.
- Sem transições animadas entre telas/overlays no protótipo — exibição/ocultação instantânea. Considere transições nativas da plataforma (deslizar para cima em overlays, cross-fade de abas) em produção.

## Gerenciamento de estado
Formato de estado sugerido:
- `screen`: 'onboarding' | 'login' | 'app'
- `onboardStep`: 0–2
- `authMode`: 'login' | 'signup'; campos do formulário de autenticação (nome, e-mail, senha) — sem autenticação real conectada, apenas um stub de submissão que navega
- `tab`: 'dashboard' | 'transactions' | 'reports' | 'settings'
- Flags de overlay: `showAdd`, `viewingTransactionId`, `showCategories`, `editingCategoryId`
- `categories`: array de `{ id, name, letter, budget }`
- `transactions`: array de `{ id, date, categoryId, note, amount, receiptImage? }`
- `txFilter`: id de categoria ou null (tela de Transações)
- `period`: 'semana' | 'mes' | 'ano' (tela de Relatórios)
- `notifPush`, `notifEmail`: booleanos
- Campos de rascunho de nova despesa: `amount, categoryId, date, note, receiptImage`
- Derivados/computados (recalcular a cada mudança de transações/categorias): totais gastos por categoria, percentual do orçamento, flag de orçamento excedido, totais gerais, categoria principal, listas ordenadas.

## Tokens de design (design system Modernist)
- **Cores**: `--color-bg #f3f2f2`, `--color-surface #eae9e9`, `--color-text #201e1d`, `--color-accent #ec3013`, `--color-divider` (mistura de 40% de `--color-text`).
  - Rampa neutra: 100 `#f8f4f4` → 900 `#2d2b2b` (100/200/300 para tingimentos e bordas, 800/900 para tinta forte).
  - Rampa de acento: 100 `#fff2ef` → 900 `#4d170e` (100 para tingimento de seleção, 600 hover, 700 para texto em cor de acento, 800 para texto de tag sobre tingimento).
- **Tipografia**: Archivo tanto para títulos quanto para corpo (`--font-heading`, `--font-body`, peso 800 para títulos). H1 ≈ 28–32px, cabeçalhos de seção 15px, corpo 13–15px, legendas 11–12px.
- **Escala de espaçamento**: `--space-1` 4px, `--space-2` 8px, `--space-3` 12px, `--space-4` 16px, `--space-6` 24px, `--space-8` 32px.
- **Raio**: 0px em todo lugar (`--radius-sm/md/lg` todos 0) — sem cantos arredondados, em nenhum lugar, nunca.
- **Sombras**: `--shadow-sm/md/lg` — sombras suaves tingidas de tinta, usadas com moderação (ex.: o FAB, cards de resumo).
- **Regras/divisórias**: 2px sólido `--color-divider` para quebras estruturais de seção; 1px para separadores de linha em listas.
- **Componentes usados**: `.btn` (`.btn-primary`, `.btn-secondary`, `.btn-ghost`, `.btn-icon`, `.btn-block`), `.tag`, `.field` / `.input`, `.seg` / `.seg-opt` (controle segmentado), `.card` (`.card-kicker`, `.card-title`), `.hr`. O código-fonte completo dos componentes está na pasta anexa `_ds/` do design system (`styles.css`).
- Tom do texto: simples, funcional, português brasileiro (pt-BR). Moeda formatada como `R$ 1.234,56`.

## Assets
- Nenhum asset fotográfico — o design é inteiramente tipográfico/geométrico conforme o sistema Modernist (quadrados/letras planos para selos de categoria, sem ícones nas tags de categoria).
- Pequeno conjunto de ícones de linha desenhados à mão (estilo Lucide, 24×24, baseados em traço) usados apenas em: barra de abas inferior (início/lista/gráfico de barras/ajustes), FAB (mais), chrome de overlay (seta para trás, lixeira, fechar). Devem ser substituídos pelo conjunto real de ícones Lucide em produção (https://lucide.dev), usados como estão no espírito deste protótipo.
- Dois alvos de "anexar uma foto" (campo de comprovante em Adicionar Despesa, visualizador de comprovante em Detalhe da Transação) são placeholders para um fluxo real de seleção/upload de imagem ou arquivo.

## Screenshots
`screenshots/` contém um PNG de referência por tela/overlay, na ordem do fluxo: 01-onboarding, 02-login, 03-dashboard, 04-transactions, 05-reports, 06-settings, 07-add-expense, 08-transaction-detail, 09-categories-budgets.

## Arquivos
- `jrnexpenses.dc.html` — o protótipo interativo completo (todas as telas/overlays, populado com dados de exemplo de categorias/transações).
- `_ds/` — o design system Modernist vinculado: `styles.css` (todos os tokens + classes de componentes) e `_ds_bundle.js`.
- `ios-frame.jsx` — o chrome de bezel/barra de status do iPhone que envolve o protótipo (apenas apresentação, não faz parte da UI do app).
- `image-slot.js` — o componente placeholder de arrastar e soltar imagem usado nos dois pontos de anexar comprovante.
