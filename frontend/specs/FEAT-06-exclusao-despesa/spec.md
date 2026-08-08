# FEAT-06: Exclusão de despesa

## Objetivo
Permitir que o usuário autenticado exclua uma despesa já cadastrada
(FEAT-02), a partir da listagem (FEAT-03), consumindo o contrato já
existente e implementado no backend (`DELETE /expenses/{id}`,
documentado em `backend/specs/FEAT-07-exclusao-despesa/spec.md`).
Fecha, junto com a FEAT-05 (edição), o conjunto de ações diretas sobre
uma despesa a partir da listagem.

## Contexto
Hoje a listagem (FEAT-03) tem uma ação de editar por item (FEAT-05),
mas nenhuma forma de remover uma despesa cadastrada por engano ou
duplicada — a única opção seria excluir diretamente pela API. A
exclusão é permanente (hard delete, sem lixeira/soft-delete, mesma
limitação já registrada no backend, FEAT-07) — por isso exige
confirmação explícita do usuário antes de qualquer chamada à API.

## Requisitos de negócio
- Cada despesa na listagem (`ExpensesListPage`, FEAT-03) ganha uma ação
  de excluir, ao lado da ação de editar (FEAT-05)
- Acionar a exclusão **nunca chama a API diretamente** — sempre abre um
  popup de confirmação identificando a despesa (ao menos a descrição),
  com uma opção de confirmar e uma de cancelar
- Cancelar fecha o popup sem qualquer chamada à API e sem alterar a
  despesa
- Confirmar chama `DELETE /expenses/{id}`; com sucesso, a despesa some
  da listagem imediatamente, sem exigir recarregar a página
- Exclusão é permanente — não há como desfazer, nem lixeira (mesma
  limitação do backend, FEAT-07); o texto do popup de confirmação deixa
  isso claro para o usuário
- Se a despesa já não existir mais ou não pertencer ao usuário
  autenticado (404) — ex.: excluída em outra aba entre a listagem
  carregar e a confirmação — a aplicação exibe uma mensagem clara; como
  o item já não deveria mais existir para o usuário, ele também é
  removido da listagem local
- Erros inesperados da API (5xx) ao excluir são tratados e exibidos de
  forma amigável, sem remover o item da listagem (a exclusão não
  aconteceu)
- Se a chamada retornar 401 (sessão expirada durante o uso), o usuário
  é informado e reconduzido à tela de login — mesmo comportamento já
  estabelecido nas features anteriores
- A ação de excluir só está disponível dentro da listagem, que já é
  protegida por sessão válida (`ProtectedRoute`, FEAT-01) — nenhuma
  regra nova de acesso é introduzida por esta feature

## User stories

### Abrir a confirmação de exclusão
Given um usuário autenticado vendo a listagem de despesas com pelo
menos uma despesa
When ele aciona a ação de excluir em uma despesa
Then a aplicação abre um popup de confirmação identificando a despesa,
sem chamar a API

### Cancelar a exclusão
Given um usuário autenticado com o popup de confirmação de exclusão
aberto
When ele aciona cancelar (ou fecha o popup sem confirmar)
Then o popup fecha, nenhuma chamada é feita à API e a despesa continua
na listagem, inalterada

### Confirmar a exclusão com sucesso
Given um usuário autenticado com o popup de confirmação de exclusão
aberto para uma despesa própria
When ele confirma a exclusão
Then a aplicação chama `DELETE /expenses/{id}`, o popup fecha e a
despesa some da listagem imediatamente

### Excluir despesa que já não existe mais
Given um usuário autenticado
When ele confirma a exclusão de uma despesa que já foi removida (ou
nunca pertenceu a ele) entre o carregamento da listagem e a confirmação
Then a aplicação exibe uma mensagem clara e remove o item da listagem
local, sem quebrar a tela

### Erro inesperado ao excluir
Given um usuário autenticado com o popup de confirmação de exclusão
aberto
When ele confirma a exclusão e a API retorna um erro inesperado (5xx)
Then a aplicação exibe uma mensagem de erro genérica, e a despesa
permanece na listagem

### Sessão expirada durante a exclusão
Given um usuário com sessão expirada
When ele confirma a exclusão de uma despesa e a API retorna 401
Then a aplicação informa que a sessão expirou e redireciona para
`/login`

## Contratos da API observáveis
Esta feature consome o contrato já definido e implementado no backend
(`backend/specs/FEAT-07-exclusao-despesa/spec.md` /
`backend/docs/openapi.json`), reproduzido aqui apenas como referência
de integração.

### DELETE /expenses/{id}
Header: `Authorization: Bearer <token>`

Response 204: sem corpo.

Response 401 / 404 / 500: mesmo formato de `ProblemDetails` já usado
nas demais features (`title`, `status`, `detail`).

## Critérios de aceite
- [x] Cada despesa na listagem tem uma ação de excluir, ao lado da ação
      de editar
- [x] Acionar excluir sempre abre um popup de confirmação antes de
      qualquer chamada à API
- [x] Popup de confirmação identifica a despesa e deixa claro que a
      exclusão é permanente
- [x] Cancelar fecha o popup sem chamar a API e sem alterar a despesa
- [x] Confirmar chama `DELETE /expenses/{id}` e remove a despesa da
      listagem imediatamente após sucesso
- [x] Excluir uma despesa já inexistente (404) exibe mensagem clara e
      remove o item da listagem local
- [x] Erro inesperado (5xx) ao excluir exibe mensagem genérica e mantém
      a despesa na listagem
- [x] Erro 401 ao excluir exibe aviso de sessão expirada e redireciona
      para `/login`

## Status

Implementado. `httpClient.delete`, `expensesApi.deleteExpense`,
`useDeleteExpense`, `useExpensesQuery.removeItem`,
`ExpenseDeleteDialog` (shadcn `alert-dialog`) implementados conforme
`plan.md`. Cada item de `ExpenseList` ganhou um botão de excluir (ícone
`Trash2`) ao lado do de editar, que abre o dialog de confirmação;
`ExpensesListPage` conecta `query.removeItem` como `onDeleted`. Nenhum
erro tipado novo (reaproveita `NotFoundError`/`SessionExpiredError`/
`UnknownExpenseError` da FEAT-05).

Suíte completa (`npm test`) passa: 116/116 testes. `tsc -b`,
`vite build` e `oxlint` sem erros novos (mesmos dois warnings
pré-existentes de antes desta feature).

Validação manual: fluxo completo (excluir, cancelar, confirmar, despesa
já removida) validado pelo usuário com o backend real — feature
confirmada funcionando ponta a ponta.

Durante a validação, o usuário reportou um warning no console do Base
UI ("A component that acts as a button expected a native `<button>`")
nos três lugares onde `Button` era composto com `render={<Link .../>}`
para ações de navegação (ícone de editar em `ExpenseList`, "Voltar à
listagem" em `ExpenseNotFound`, "Cancelar" em `EditExpenseForm`, esta
última já existente desde a FEAT-05). Corrigido trocando essas três
composições por `<Link>` puro estilizado com `buttonVariants()` (já
exportado por `components/ui/button.tsx`) em vez de passar pelo
primitivo `Button` do Base UI — mantém a aparência idêntica, mas
preserva a semântica de link (`role="link"`) em vez de forçar
`role="button"` numa `<a>` de navegação. Suíte completa revalidada
(116/116) após a correção.

## Fora do escopo
- Exclusão em lote (múltiplas despesas de uma vez) — mesma limitação
  do backend (FEAT-07)
- Soft-delete / lixeira / desfazer exclusão — mesma limitação do
  backend (FEAT-07)
- Exclusão a partir de outra tela que não a listagem (ex.: a partir da
  tela de edição, FEAT-05)
