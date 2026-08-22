# FEAT-18: Migração para o design system Modernist — Popup de Editar Despesa

## Objetivo

Continuar a migração visual iniciada nas FEAT-14 a FEAT-17, transformando
a **edição de despesa** de uma página própria (`/expenses/:id/edit`) em
um popup Modernist — unificado com o popup de cadastro já criado na
FEAT-17 (`NewExpenseDialog`/`ExpenseForm`), como já mostra o design de
referência (`frontend/design-system/jrnexpenses-web.dc.html`, bloco
`showAdd`, que usa o **mesmo** popup para adicionar e editar,
alternando título e rótulo do botão conforme `editingTxId`).

## Contexto

Hoje o ícone de lápis de cada linha da tabela de Transações
(`ExpenseList`, migrada na FEAT-16) navega para `/expenses/:id/edit`
(`EditExpensePage` → `EditExpenseForm`), uma página cheia em shadcn/ui
+ Tailwind que reaproveita o componente compartilhado
`ExpenseFormFields`. Essa página busca a despesa (`useExpense`) antes
de exibir o formulário, e trata 404 com uma tela cheia "Despesa não
encontrada".

Esta feature:
1. Generaliza o popup criado na FEAT-17 (`ExpenseForm`/
   `NewExpenseDialog`) para também **editar** despesas — mesmo popup,
   mesmos campos, diferindo em: título ("Editar despesa" em vez de
   "Nova despesa"), rótulo do botão ("Salvar alterações" em vez de
   "Registrar despesa"), valores iniciais preenchidos, e chamada à API
   de atualização (`PUT /expenses/{id}`) em vez de criação
2. Remove a rota `/expenses/:id/edit` e a página `EditExpensePage`; o
   ícone de editar na tabela passa a abrir o popup já preenchido com
   os dados da despesa clicada
3. O botão "Editar" em `ExpenseDetailPage` (fora do escopo visual desta
   feature — continua shadcn/ui) deixa de apontar para a rota removida
   e passa a navegar para `/expenses` (listagem), já que editar só é
   possível a partir de lá agora — ajuste mecânico mínimo, necessário
   para o link não quebrar, sem migrar o resto da página
4. **Não inclui** o campo de comprovante nem o buscador de categoria
   com ícones/cores do design de referência — mesmas exclusões já
   feitas na FEAT-17 para o cadastro
5. `EditExpenseForm.tsx` e `ExpenseFormFields.tsx` deixam de ter
   consumidores e são removidos (o compartilhamento entre cadastro e
   edição que hoje existe via `ExpenseFormFields` deixa de fazer
   sentido, já que os dois fluxos passam a usar o mesmo popup
   Modernist)

Nenhuma regra de negócio ou contrato de API muda: mesmos endpoints
(`GET /expenses/{id}`, `PUT /expenses/{id}`), mesma validação
(`expenseSchema`), mesmos erros tratados hoje em `useExpense`/
`useUpdateExpense`.

## Requisitos de negócio

- Nenhuma regra de validação da edição de despesa muda: mesmas
  mensagens/limites de `expenseSchema` já usados no cadastro
- Clicar no ícone de editar de uma linha da tabela abre o mesmo popup
  do cadastro, com os campos preenchidos com os dados atuais da
  despesa (busca via `GET /expenses/{id}` antes de exibir), título
  "Editar despesa" e botão "Salvar alterações"
- Enquanto os dados da despesa carregam, o popup mostra um estado de
  carregamento (mesmo texto "Carregando..." já usado hoje em
  `EditExpensePage`)
- Ao **salvar com sucesso**, o popup fecha imediatamente (mesmo
  comportamento definido na FEAT-17 para o cadastro) e a listagem por
  trás é atualizada, refletindo a descrição/valor/categoria/data
  editados
- Se a despesa não for mais encontrada (`404` ao carregar ou ao
  salvar), o popup fecha automaticamente e a listagem é atualizada
  (mesmo tratamento silencioso já usado em `ExpenseDeleteDialog` para
  `NotFoundError` — a despesa já não existe mais, não há nada para o
  usuário confirmar)
- Fechar o popup (backdrop, Esc ou "Cancelar") a qualquer momento não
  chama a API de atualização
- A rota `/expenses/:id/edit` e a página `EditExpensePage` são
  removidas; acessar a URL diretamente deixa de ter destino próprio
  (mesmo tratamento já adotado para `/expenses/new` na FEAT-17)
- O botão "Editar" em `ExpenseDetailPage` passa a navegar para
  `/expenses` em vez de `/expenses/:id/edit`
- Sem mudança de contrato com o backend, sem novo endpoint, sem novo
  recurso AWS

## User stories

### Abrir o popup de edição a partir da tabela

- **Given** um usuário autenticado na tela de Transações, com pelo
  menos uma despesa na tabela
- **When** clica no ícone de editar de uma linha
- **Then** vê o popup Modernist com título "Editar despesa", os campos
  preenchidos com os dados atuais daquela despesa, e o botão "Salvar
  alterações"

### Editar uma despesa com sucesso

- **Given** o popup de edição aberto e preenchido com dados válidos
- **When** o usuário altera algum campo e submete
- **Then** a despesa é atualizada via `PUT /expenses/{id}`, o popup
  fecha imediatamente, e a listagem por trás reflete os dados
  atualizados

### Validação client-side

- **Given** o popup de edição aberto
- **When** o usuário limpa um campo obrigatório e submete
- **Then** vê o erro inline correspondente (mesmas mensagens de
  `expenseSchema`), sem chamar a API

### Erro ao salvar

- **Given** o popup de edição aberto com dados válidos
- **When** a API responde com erro (ex.: 400)
- **Then** vê a mensagem de erro dentro do popup, com os dados
  preenchidos preservados, sem fechar o popup

### Despesa não encontrada

- **Given** o usuário clica em editar uma despesa que, entre a
  listagem e o clique, já foi excluída por outra sessão
- **When** o popup tenta carregar ou salvar os dados (`404`)
- **Then** o popup fecha automaticamente e a listagem se atualiza, sem
  mostrar erro para o usuário

### Cancelar a edição

- **Given** o popup de edição aberto
- **When** o usuário clica em "Cancelar", pressiona Esc ou clica fora
  do popup
- **Then** o popup fecha sem chamar a API, e a despesa permanece
  inalterada

### Link de edição no detalhe da despesa

- **Given** um usuário na tela de detalhe de uma despesa
  (`/expenses/:id`, fora do escopo visual desta feature)
- **When** clica em "Editar"
- **Then** é levado para a listagem de Transações (`/expenses`), onde
  pode abrir o popup de edição pelo ícone da linha

### Rota antiga não existe mais

- **Given** qualquer navegação
- **When** o usuário (ou um link salvo) tenta acessar
  `/expenses/:id/edit`
- **Then** não encontra mais uma página própria para edição — a edição
  só é acessível pelo popup dentro de `/expenses`

## Fora do escopo

- Migrar `ExpenseDetailPage` (`/expenses/:id`) para o Modernist — só o
  destino do link "Editar" é ajustado, o resto da página continua
  shadcn/ui, migra em spec futura
- Campo de upload de comprovante — mesma exclusão da FEAT-17
- Buscador de categoria com lista de ícones/cores — mesma exclusão da
  FEAT-17 (continua `<select>` simples)
- Qualquer alteração em `backend/`
- Provisionamento ou alteração de infraestrutura AWS

## Critérios de aceite

- [x] Ícone de editar na tabela de Transações abre o popup Modernist
      preenchido com os dados da despesa, em vez de navegar para
      `/expenses/:id/edit`
- [x] Rota `/expenses/:id/edit`, `EditExpensePage`,
      `EditExpenseForm` e `ExpenseFormFields` removidos
- [x] Popup mostra "Editar despesa"/"Salvar alterações" no modo edição
      e "Nova despesa"/"Registrar despesa" no modo cadastro, sem
      duplicar o componente
- [x] Estado de carregamento exibido enquanto os dados da despesa
      carregam
- [x] Submissão com sucesso fecha o popup imediatamente e atualiza a
      listagem por trás com os dados editados
- [x] 404 (despesa não encontrada, ao carregar ou salvar) fecha o
      popup automaticamente e atualiza a listagem, sem mostrar erro
- [x] Validação client-side e erro de API preservados exatamente como
      hoje
- [x] Botão "Editar" em `ExpenseDetailPage` navega para `/expenses`
- [x] Nenhuma outra tela do app (detalhe de despesa, categorias,
      ajustes, início, menu) muda visualmente por causa desta feature
- [x] 100% dos testes (unitários/componente) de `features/expenses/`
      e `routes/ExpensesListPage` passando após a migração (234/234,
      `tsc -b`, `oxlint` e `npm run build` limpos)
