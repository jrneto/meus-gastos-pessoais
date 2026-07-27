# FEAT-05: Edição de despesa

## Objetivo
Permitir que o usuário autenticado edite uma despesa já cadastrada
(FEAT-02), a partir da listagem (FEAT-03), consumindo os contratos já
existentes e implementados no backend (`GET /expenses/{id}` e
`PUT /expenses/{id}`, documentados em
`backend/specs/FEAT-08-atualizacao-despesa/spec.md`). Fecha a lacuna
deixada deliberadamente fora do escopo da FEAT-03
("Edição de despesa diretamente a partir da listagem — feature futura
separada").

## Contexto
Hoje a listagem de despesas (FEAT-03) é somente leitura — não há como
corrigir uma despesa cadastrada com dado errado sem excluir e
recadastrar (e a exclusão nem tem tela própria ainda, só o endpoint no
backend, FEAT-07). Esta feature cobre exclusivamente a edição: a partir
de um item da listagem, o usuário acessa uma tela de edição pré-
preenchida com os dados atuais da despesa, corrige o que for necessário
e salva.

## Requisitos de negócio
- Cada despesa na listagem (`ExpensesListPage`, FEAT-03) ganha uma ação
  de editar, que leva a uma tela de edição dedicada para aquela despesa
- A tela de edição carrega os dados atuais da despesa (descrição,
  valor, categoria, data) e os exibe já preenchidos no formulário —
  o usuário não precisa redigitar o que não vai mudar
- Mesmos campos e regras de validação do cadastro (FEAT-02): descrição
  (obrigatória, até 200 caracteres), valor (obrigatório, positivo,
  digitado em formato monetário legível e convertido para centavos),
  categoria (obrigatória, enum fechado), data da despesa (obrigatória,
  retroativa ou futura permitidas)
- Atualização é sempre uma substituição completa dos 4 campos — não há
  edição parcial de um campo isolado (mesma regra do backend, FEAT-08)
- Validação client-side espelha as regras acima (Zod), evitando
  round-trip desnecessário à API para erros óbvios
- Após salvar com sucesso, o usuário retorna à listagem, que reflete os
  dados atualizados
- O usuário pode cancelar a edição a qualquer momento e voltar à
  listagem sem salvar nada
- Erros de validação são exibidos inline, por campo, sem chamar a API
- Se a despesa não existir mais ou não pertencer ao usuário autenticado
  (404) — seja ao carregar a tela, seja ao salvar — a aplicação exibe
  uma mensagem clara e oferece um caminho de volta à listagem, sem
  expor se o `id` pertence a outro usuário
- Erros inesperados da API (400) ao salvar são tratados e exibidos de
  forma amigável, sem expor detalhes técnicos da resposta, sem perder
  os dados já preenchidos no formulário
- Se qualquer chamada retornar 401 (sessão expirada durante o uso), o
  usuário é informado e reconduzido à tela de login — mesmo
  comportamento já estabelecido nas features anteriores
- A tela só é acessível com sessão válida (`ProtectedRoute`, FEAT-01)
  — sem sessão, redireciona para `/login`

## User stories

### Acessar edição a partir da listagem
Given um usuário autenticado vendo a listagem de despesas com pelo
menos uma despesa
When ele aciona a ação de editar em uma despesa
Then a aplicação navega para a tela de edição daquela despesa, com o
formulário já preenchido com os dados atuais

### Editar despesa com sucesso
Given um usuário autenticado na tela de edição de uma despesa própria
When ele altera um ou mais campos com dados válidos e salva
Then a aplicação chama `PUT /expenses/{id}`, e o usuário retorna à
listagem, que reflete os dados atualizados

### Validação de campos obrigatórios
Given um usuário autenticado na tela de edição de uma despesa
When ele apaga um campo obrigatório (descrição, valor, categoria ou
data) e tenta salvar
Then a aplicação exibe o erro correspondente inline, por campo, e não
chama a API

### Cancelar edição
Given um usuário autenticado na tela de edição de uma despesa, com
alterações ainda não salvas
When ele aciona cancelar
Then a aplicação volta à listagem sem chamar `PUT /expenses/{id}` e sem
alterar a despesa

### Editar despesa que não existe mais ou não pertence ao usuário
Given um usuário autenticado
When ele acessa a tela de edição de um `id` que não existe (ou tenta
salvar uma despesa que foi removida entre o carregamento da tela e o
envio)
Then a aplicação exibe uma mensagem clara de despesa não encontrada e
oferece um caminho de volta à listagem, sem revelar se o `id` pertence
a outro usuário

### Erro inesperado da API ao salvar (400)
Given um usuário autenticado que já passou pela validação client-side
When a API ainda assim retorna 400 ao salvar (divergência de regra
client/API)
Then a aplicação exibe uma mensagem de erro genérica, sem perder os
dados já preenchidos no formulário

### Sessão expirada durante o uso
Given um usuário com sessão expirada usando a tela de edição
When a API retorna 401 (seja ao carregar os dados, seja ao salvar)
Then a aplicação informa que a sessão expirou e redireciona para
`/login`

### Acesso à tela sem sessão válida
Given um usuário sem sessão válida
When ele tenta acessar a tela de edição de uma despesa diretamente pela
URL
Then a aplicação redireciona para `/login` (comportamento herdado da
rota protegida, FEAT-01)

## Contratos da API observáveis
Esta feature consome os contratos já definidos e implementados no
backend (`backend/specs/FEAT-08-atualizacao-despesa/spec.md` /
`backend/docs/openapi.json`), reproduzidos aqui apenas como referência
de integração.

### GET /expenses/{id}
Header: `Authorization: Bearer <token>`

Response 200:
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "category": "Alimentacao",
  "expenseDate": "2025-06-15",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 401 / 404: mesmo formato de `ProblemDetails` já usado nas
demais features (`title`, `status`, `detail`).

### PUT /expenses/{id}
Header: `Authorization: Bearer <token>`

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 5290,
  "category": "Alimentacao",
  "expenseDate": "2025-06-16"
}
```

Response 200:
```json
{
  "id": "...",
  "description": "Almoço no restaurante",
  "amountInCents": 5290,
  "category": "Alimentacao",
  "expenseDate": "2025-06-16",
  "createdAt": "2025-06-15T12:34:56Z"
}
```

Response 400 (validation-error), 401 (unauthorized) e 404 (not-found):
mesmo formato de `ProblemDetails` já usado nas demais features.

## Critérios de aceite
- [x] Cada despesa na listagem tem uma ação de editar, que leva à tela
      de edição daquela despesa
- [x] Tela de edição carrega e preenche o formulário com os dados
      atuais da despesa (`GET /expenses/{id}`)
- [x] Formulário de edição usa os mesmos campos e validações do
      cadastro (FEAT-02)
- [x] Salvar com sucesso chama `PUT /expenses/{id}` e retorna o usuário
      à listagem, refletindo os dados atualizados
- [x] Validação client-side impede submissão com campo obrigatório
      ausente/inválido, com erro inline por campo
- [x] Cancelar volta à listagem sem chamar a API e sem alterar a
      despesa
- [x] Despesa não encontrada ou de outro usuário (404) exibe mensagem
      clara e caminho de volta à listagem, tanto ao carregar quanto ao
      salvar
- [x] Erro 400 da API ao salvar exibe mensagem genérica sem perder os
      dados preenchidos
- [x] Erro 401 (ao carregar ou ao salvar) exibe aviso de sessão
      expirada e redireciona para `/login`
- [x] Acesso à tela sem sessão válida redireciona para `/login`

## Status

Implementado. `httpClient.put`, `centsToAmountInput`, `NotFoundError`/
`UpdateValidationError`, `expensesApi.getExpenseById`/`updateExpense`,
`useExpense`, `useUpdateExpense`, `ExpenseFormFields` (extraído,
compartilhado com `ExpenseForm` sem alterar seu comportamento),
`ExpenseNotFound`, `EditExpenseForm`, `routes/EditExpensePage.tsx` e a
rota `expenses/:id/edit` implementados conforme `plan.md`. Cada item da
listagem (`ExpenseList`) ganhou um link de editar (ícone `Pencil`).
Nenhuma dependência nova.

Suíte completa (`npm test`) passa: 103/103 testes (incluindo os testes
já existentes de `ExpenseForm`, que continuam verdes após a extração de
`ExpenseFormFields`). `tsc -b`, `vite build` e `oxlint` sem erros novos
(mesmos dois warnings pré-existentes de antes desta feature).

Validação manual: fluxo completo (editar a partir da listagem,
formulário pré-preenchido, salvar com sucesso, cancelar, validação
inline, despesa inexistente) validado pelo usuário com o backend real
— feature confirmada funcionando ponta a ponta.

## Fora do escopo
- Exclusão de despesa a partir da listagem — endpoint já existe no
  backend (FEAT-07), mas a tela é uma feature futura separada
- Edição em lote (múltiplas despesas de uma vez) — mesma limitação do
  backend (FEAT-08)
- Histórico de alterações / auditoria de edições
- Edição inline diretamente na listagem (sem navegar para outra tela)