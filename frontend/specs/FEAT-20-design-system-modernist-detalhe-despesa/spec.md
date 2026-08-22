# FEAT-20: Migração para o design system Modernist — Detalhe da Despesa e Acertos Finos de Transações

## Objetivo

Fechar a migração da tela de Transações para o design system **Modernist**
(iniciada na FEAT-16), corrigindo três pontos que ainda destoam do
design de referência (`frontend/design-system/jrnexpenses-web.dc.html`):

1. O texto da categoria na tabela não deve ter cor customizada
2. As ações de editar/excluir deixam de ser ícones na linha da tabela
   e passam a viver dentro de um popup de **detalhe da despesa**
   (aberto ao clicar na linha), como no design de referência
3. O conteúdo das páginas já migradas (Transações, Categorias) ganha a
   mesma restrição de largura/respiro do design (`max-width: 920px`,
   centralizado, com padding), em vez de esticar até a borda da tela

## Contexto

Hoje, em `ExpenseList` (tabela de Transações), a categoria é exibida
com `color: category.cor` (cor por categoria) e cada linha tem ícones
de lápis/lixeira que abrem os popups de editar/excluir diretamente;
clicar na própria linha navega para `/expenses/:id`
(`ExpenseDetailPage`), uma página cheia ainda em shadcn/ui, fora do
escopo de todas as migrações anteriores (FEAT-16 a FEAT-18).

O design de referência não tem ícones de ação na linha nem coluna de
"Ações" — a tabela só tem Categoria/Descrição/Data/Valor. Clicar na
linha (`t.open`) abre um popup **"Detalhe da despesa"**
(`isViewingTx`) mostrando valor, data, categoria e observação, com três
botões: "Excluir", "Editar" e "Fechar". "Editar" leva ao mesmo popup de
formulário já usado para cadastro (alternando para modo edição);
"Excluir" aciona a confirmação de exclusão.

Esta feature:
1. Remove a cor customizada do texto da categoria na tabela (mantém
   `.tag.tag-neutral`, sem `style={{ color }}`)
2. Cria um popup de detalhe (Modernist) mostrando valor, data,
   categoria (mesmo tile decorativo neutro já usado em Categorias — a
   inicial do nome, sem cor) e descrição, com os botões "Excluir",
   "Editar" e "Fechar" — reaproveitando os popups de editar
   (`ExpenseFormDialog`, já existente) e excluir (`ExpenseDeleteDialog`,
   já existente) já construídos nas FEAT-16/17/18, em vez de duplicá-los
2. Remove os ícones de editar/excluir e a coluna "Ações" da tabela;
   clicar na linha abre o popup de detalhe em vez de navegar
3. Remove a rota `/expenses/:id` e a página `ExpenseDetailPage`
   (shadcn/ui); `ExpenseNotFound` (`features/expenses/`) e
   `CategoryBadge` (`lib/categories/`, sem mais nenhum consumidor após
   essa remoção) são removidos
4. Aplica `max-width: 920px; margin: 0 auto; padding: 40px` (mesmos
   valores do design de referência) ao conteúdo de `ExpensesListPage` e
   `CategoriesPage` — as duas telas já migradas para o Modernist

Nenhuma regra de negócio ou contrato de API muda: mesmos endpoints,
mesma validação, mesmos erros já tratados hoje.

## Requisitos de negócio

- O texto da categoria na tabela de Transações não usa mais `cor` da
  categoria — só a cor padrão do `.tag.tag-neutral`
- Clicar em qualquer linha da tabela (fora dos elementos internos que
  já têm outra ação) abre o popup "Detalhe da despesa" com os dados
  daquela despesa (sem nova chamada à API — usa o item já carregado na
  listagem)
- O popup de detalhe mostra: valor (destacado), data, categoria (tile
  decorativo neutro + nome, sem cor customizada) e descrição
- Botão "Editar" no popup de detalhe: fecha o popup de detalhe e abre
  o popup de edição já existente (`ExpenseFormDialog`, modo `edit`),
  pré-preenchido com os dados atuais (busca fresca via `GET
  /expenses/{id}`, mesmo comportamento de hoje)
- Botão "Excluir" no popup de detalhe: fecha o popup de detalhe e abre
  a confirmação de exclusão já existente (`ExpenseDeleteDialog`),
  preservando todo o tratamento de erro/sucesso atual
- Botão "Fechar": fecha o popup de detalhe sem nenhuma chamada à API
- A tabela não tem mais coluna de ações nem ícones de editar/excluir
  por linha — editar e excluir só são acessíveis a partir do popup de
  detalhe
- A rota `/expenses/:id` deixa de existir; acessar a URL diretamente
  não tem mais destino próprio (mesma decisão já tomada para
  `/expenses/new` e `/expenses/:id/edit` nas FEAT-17/18)
- `ExpensesListPage` e `CategoriesPage` passam a limitar o conteúdo a
  `max-width: 920px`, centralizado (`margin: 0 auto`), com `padding:
  40px` — mesmos valores do design de referência
- Sem mudança de contrato com o backend, sem novo endpoint, sem novo
  recurso AWS

## User stories

### Ver o detalhe de uma despesa

- **Given** um usuário autenticado na tela de Transações, com pelo
  menos uma despesa na tabela
- **When** clica em uma linha
- **Then** vê o popup "Detalhe da despesa" com valor, data, categoria e
  descrição daquela despesa, e os botões "Excluir", "Editar" e
  "Fechar"

### Editar a partir do detalhe

- **Given** o popup de detalhe aberto
- **When** o usuário clica em "Editar"
- **Then** o popup de detalhe fecha e o popup de edição abre
  pré-preenchido com os dados atuais da despesa — mesmo fluxo de
  edição já existente

### Excluir a partir do detalhe

- **Given** o popup de detalhe aberto
- **When** o usuário clica em "Excluir"
- **Then** o popup de detalhe fecha e a confirmação de exclusão abre —
  mesmo fluxo de exclusão já existente

### Fechar o detalhe sem ação

- **Given** o popup de detalhe aberto
- **When** o usuário clica em "Fechar", pressiona Esc ou clica fora do
  popup
- **Then** o popup fecha sem nenhuma chamada à API

### Categoria sem cor customizada

- **Given** a tabela de Transações com despesas de categorias
  diferentes
- **When** a página é renderizada
- **Then** todos os textos de categoria aparecem com a mesma
  aparência neutra (`.tag.tag-neutral`), sem cor diferente por
  categoria

### Conteúdo com largura limitada

- **Given** um usuário com um monitor grande, navegando em Transações
  ou Categorias
- **When** a página carrega
- **Then** o conteúdo fica centralizado numa coluna de até 920px de
  largura, com respiro nas laterais — igual ao design de referência —
  em vez de esticar até a borda da tela

### Rota antiga não existe mais

- **Given** qualquer navegação
- **When** o usuário (ou um link salvo) tenta acessar `/expenses/:id`
- **Then** não encontra mais uma página própria de detalhe — o
  detalhe só é acessível pelo popup dentro de `/expenses`

## Fora do escopo

- Campo "Lançado por" (quem registrou a despesa) mostrado no design —
  não existe conceito de múltiplos usuários/atribuição no app hoje
- Campo de comprovante — mesma exclusão já feita na FEAT-17
- Exibir ID técnico ou timestamp completo de criação no popup de
  detalhe (a página antiga mostrava; o design de referência não) —
  simplifica para os campos que o design realmente mostra
- Aplicar a restrição de largura a outras páginas do app (início,
  ajustes, relatórios) — só nas duas telas já migradas para o
  Modernist
- Qualquer alteração em `backend/`
- Provisionamento ou alteração de infraestrutura AWS

## Critérios de aceite

- [x] Texto da categoria na tabela de Transações sem cor customizada
      (`.tag.tag-neutral` padrão)
- [x] Tabela de Transações sem coluna de ações nem ícones de
      editar/excluir por linha
- [x] Clicar numa linha abre o popup "Detalhe da despesa" (valor,
      data, categoria com tile neutro, descrição), sem nova chamada à
      API
- [x] "Editar" no popup de detalhe fecha o detalhe e abre o popup de
      edição pré-preenchido (mesmo fluxo já existente)
- [x] "Excluir" no popup de detalhe fecha o detalhe e abre a
      confirmação de exclusão (mesmo fluxo já existente)
- [x] "Fechar" (botão, Esc ou backdrop) fecha o popup sem chamar a API
- [x] Rota `/expenses/:id`, `ExpenseDetailPage`, `ExpenseNotFound`
      (`features/expenses/`) e `CategoryBadge` (`lib/categories/`, sem
      mais consumidor) removidos
- [x] `ExpensesListPage` e `CategoriesPage` com conteúdo limitado a
      `max-width: 920px`, centralizado, com `padding: 40px`
- [x] Nenhuma outra tela do app muda visualmente por causa desta
      feature
- [x] 100% dos testes (unitários/componente) de `features/expenses/`
      e `routes/ExpensesListPage` passando após a migração (243/243,
      `tsc -b`, `oxlint` e `npm run build` limpos)
