# FEAT-17: Migração para o design system Modernist — Popup de Nova Despesa

## Objetivo

Continuar a migração visual iniciada na FEAT-14 (Login), FEAT-15 (menu)
e FEAT-16 (listagem de Transações), transformando o cadastro de
despesa de uma **página própria** (`/expenses/new`) em um **popup**
aberto de dentro da tela de Transações, com a linguagem visual do
design system **Modernist** — como já mostra o design de referência
(`frontend/design-system/jrnexpenses-web.dc.html`, bloco `showAdd`).

## Contexto

Hoje o botão "+ Nova despesa" da listagem (`ExpensesListPage`, migrada
na FEAT-16) navega para a rota `/expenses/new`
(`RegisterExpensePage` → `ExpenseForm`), uma página cheia em shadcn/ui
+ Tailwind. O design de referência não tem essa rota: "Nova despesa" é
um `.dialog-backdrop`/`.dialog` que abre por cima da própria listagem,
fecha ao salvar e não navega o usuário para lugar nenhum.

Esta feature:
1. Recria o formulário de cadastro (`ExpenseForm`/`ExpenseFormFields`)
   com tokens/classes do Modernist, dentro de um popup
   (`.dialog-backdrop`/`.dialog`), reaproveitando o padrão já
   estabelecido em `ExpenseDeleteDialog` (FEAT-16) e `NavMoreSheet`
   (FEAT-15)
2. Remove a rota `/expenses/new` e a página `RegisterExpensePage`; o
   botão "+ Nova despesa" da listagem passa a abrir o popup em vez de
   navegar
3. **Não inclui** edição de despesa (`/expenses/:id/edit`) nem o
   campo de comprovante (upload de imagem) mostrado no design —
   ambos ficam fora do escopo (ver "Fora do escopo")
4. Mantém a categoria como um campo de seleção única (`Select`), só
   recriado com os tokens do Modernist — sem o buscador de categoria
   com lista de ícones/cores do design de referência

Nenhuma regra de negócio ou contrato de API muda: mesmo endpoint
(`POST /expenses`), mesma validação (`expenseSchema`), mesmos erros
tratados hoje em `useRegisterExpense`.

## Requisitos de negócio

- Nenhuma regra de validação do cadastro de despesa muda: descrição
  (1–200 caracteres), valor (formato `0,00`, maior que zero),
  categoria obrigatória, data obrigatória — mesmas mensagens de erro
  já implementadas em `expenseSchema`
- O botão "+ Nova despesa" em `ExpensesListPage` deixa de ser um
  `<Link to="/expenses/new">` e passa a abrir o popup (estado local
  `isAddOpen`), sem navegação de rota
- A rota `/expenses/new` e a página `RegisterExpensePage` são
  removidas; acessar a URL diretamente deixa de ter destino próprio
  (mesmo tratamento dado a qualquer rota inexistente hoje no app —
  sem criar uma rota nova só para isso)
- Popup usa `.dialog-backdrop`/`.dialog`/`.dialog-title`/
  `.dialog-actions` do Modernist, com `role="dialog"` `aria-modal=
  "true"`, fecha ao clicar no backdrop, pressionar Esc ou clicar em
  "Cancelar" — mesmo padrão de fechamento já usado em
  `ExpenseDeleteDialog`/`NavMoreSheet`
- Ao **submeter com sucesso**, o popup fecha imediatamente (segue o
  design de referência, `saveExpense()` → `showAdd: false`) — diferente
  do comportamento atual de `ExpenseForm` (que limpa e mantém o
  formulário aberto para cadastro em sequência); esta feature substitui
  esse comportamento pelo do design
- Ao fechar o popup por sucesso, a listagem por trás é atualizada
  (reaplicando os filtros/paginação atuais), para que a despesa
  recém-criada já apareça assim que o popup fechar, sem precisar
  recarregar a página
- Fechar o popup (backdrop, Esc ou "Cancelar") a qualquer momento não
  chama a API — mesmo comportamento de cancelamento de hoje
- Estado "sem categoria cadastrada" (hoje um link para
  `/categories/new`) continua existindo dentro do popup, recriado no
  Modernist
- Sem mudança de contrato com o backend, sem novo endpoint, sem novo
  recurso AWS

## User stories

### Abrir o popup de nova despesa

- **Given** um usuário autenticado na tela de Transações
- **When** clica em "+ Nova despesa"
- **Then** vê o popup do Modernist com os campos Descrição, Valor,
  Categoria e Data, sem sair da tela de Transações

### Cadastrar uma despesa com sucesso

- **Given** o popup de nova despesa aberto, preenchido com dados
  válidos
- **When** o usuário submete
- **Then** a despesa é criada via `POST /expenses`, o popup fecha
  imediatamente, e a listagem por trás já reflete a nova despesa

### Validação client-side

- **Given** o popup aberto
- **When** o usuário submete com campos vazios/inválidos
- **Then** vê os erros inline (mesmas mensagens de hoje), sem chamar a
  API

### Erro da API

- **Given** o popup aberto com dados válidos
- **When** a API responde com erro (ex.: 400)
- **Then** vê a mensagem "Não foi possível registrar" dentro do popup,
  com os dados preenchidos preservados, sem fechar o popup

### Cancelar o cadastro

- **Given** o popup de nova despesa aberto
- **When** o usuário clica em "Cancelar", pressiona Esc ou clica fora
  do popup
- **Then** o popup fecha sem chamar a API, e a listagem permanece como
  estava (sem alteração)

### Sem categoria cadastrada

- **Given** o usuário não tem nenhuma categoria cadastrada
- **When** abre o popup de nova despesa
- **Then** vê a orientação para criar uma categoria primeiro, com um
  link para `/categories/new`, recriado no Modernist

### Rota antiga não existe mais

- **Given** qualquer navegação
- **When** o usuário (ou um link salvo) tenta acessar `/expenses/new`
- **Then** não encontra mais uma página própria para cadastro — o
  cadastro só é acessível pelo popup dentro de `/expenses`

## Fora do escopo

- Migrar a edição de despesa (`/expenses/:id/edit`,
  `EditExpensePage`) para o popup — continua rota própria em
  shadcn/ui, migra em spec futura
- Migrar o detalhe de despesa (`/expenses/:id`,
  `ExpenseDetailPage`) — fora do escopo
- Campo de upload de comprovante (imagem) — não existe suporte no
  backend hoje; fica para quando essa funcionalidade for implementada
  de verdade
- Buscador de categoria com lista de ícones/cores (o campo continua
  um `Select` de categoria única, só restilizado)
- Qualquer alteração em `backend/`
- Provisionamento ou alteração de infraestrutura AWS

## Critérios de aceite

- [x] Botão "+ Nova despesa" em `ExpensesListPage` abre um popup em
      vez de navegar para `/expenses/new`
- [x] Rota `/expenses/new` e `RegisterExpensePage` removidas do
      roteador
- [x] Popup usa `.dialog-backdrop`/`.dialog` do Modernist
      (`role="dialog"`), fecha em backdrop/Esc/"Cancelar"
- [x] `ExpenseForm` recriado com tokens/classes do Modernist (campos
      próprios, não `ExpenseFormFields` — esse componente é
      compartilhado com `EditExpenseForm`, fora do escopo, e continua
      shadcn/ui), sem nenhuma classe Tailwind/shadcn remanescente
- [x] Validação client-side (mensagens inline) e tratamento de erro da
      API preservados exatamente como hoje
- [x] Submissão com sucesso fecha o popup imediatamente e atualiza a
      listagem por trás com a despesa recém-criada
- [x] Estado "sem categoria cadastrada" preservado, recriado no
      Modernist
- [x] Nenhuma outra tela do app (edição/detalhe de despesa,
      categorias, ajustes, início, menu) muda visualmente por causa
      desta feature
- [x] 100% dos testes (unitários/componente) de `features/expenses/`
      e `routes/ExpensesListPage` passando após a migração (234/234,
      `tsc -b`, `oxlint` e `npm run build` limpos)
