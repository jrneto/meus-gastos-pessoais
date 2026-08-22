# FEAT-19: Migração para o design system Modernist — CRUD de Categorias

## Objetivo

Continuar a migração visual iniciada nas FEAT-14 a FEAT-18, recriando a
tela de **Categorias** (listagem, cadastro, edição e exclusão) com a
linguagem visual do design system **Modernist**, seguindo o padrão de
formulário **inline** mostrado no design de referência
(`frontend/design-system/jrnexpenses-web.dc.html`, bloco `isCat`) — em
vez do padrão de popup adotado para despesas (FEAT-17/18).

## Contexto

Hoje `CategoriesPage` lista categorias (`CategoryList`) com um link
"Nova categoria" que navega para `/categories/new`
(`NewCategoryPage` → `NewCategoryForm`); cada item tem um ícone de
editar que navega para `/categories/:id/edit`
(`EditCategoryPage` → `EditCategoryForm`); e um ícone de excluir que
abre `CategoryDeleteDialog` (hoje `AlertDialog` do shadcn/ui). Os dois
formulários (criar/editar) reaproveitam `CategoryFormFields` (Nome,
Cor, Ícone via `IconPicker`) e a mesma validação
(`categorySchema`).

O design de referência mostra "Categorias e orçamentos": um botão
"+ Nova categoria" que expande um formulário **inline** (Nome +
Orçamento mensal) diretamente na tela, sem navegar/abrir popup; cada
categoria é uma linha com um "quadrado" da inicial do nome, barra de
progresso de gasto vs. orçamento, e um link "Editar orçamento" que
também expande inline, editando só o valor do orçamento.

Esta feature adapta esse padrão ao que o app realmente tem hoje
(nome, cor, ícone — **sem orçamento**, conceito inexistente no
backend):
1. "+ Nova categoria" expande um formulário inline na própria tela
   (Nome, Cor, Ícone), sem navegar para `/categories/new`
2. Cada categoria na lista ganha um botão "Editar" que expande a
   **mesma** linha num formulário inline pré-preenchido (mesmos
   campos do cadastro), sem navegar para `/categories/:id/edit` —
   mesma decisão de unificar cadastro/edição já tomada para despesas
   (FEAT-17/18), só que como formulário inline em vez de popup
3. Exclusão continua com confirmação, agora recriada como popup
   Modernist (`.dialog-backdrop`/`.dialog`), mesmo padrão já usado em
   `ExpenseDeleteDialog`
4. As rotas `/categories/new` e `/categories/:id/edit` são removidas
5. Sem orçamento mensal, sem barra de progresso de gasto — a lista
   mostra ícone, cor e nome de cada categoria

Nenhuma regra de negócio ou contrato de API muda: mesmos endpoints
(`POST /categories`, `PUT /categories/{id}`, `DELETE /categories/{id}`,
`GET /categories`), mesma validação (`categorySchema`), mesmos erros
tratados hoje (`NameConflictError`, `NotFoundError`, etc.).

## Requisitos de negócio

- Nenhuma regra de validação de categoria muda: nome (1–50
  caracteres, sem duplicata — `NameConflictError`), cor (hex válido),
  ícone (obrigatório, dentre o catálogo de `CATEGORY_ICONS`)
- `CategoriesPage`, `CategoryList`, o formulário de categoria e
  `IconPicker` usam tokens/classes do Modernist, sem nenhuma classe
  Tailwind/shadcn remanescente
- Botão "+ Nova categoria" expande/recolhe um formulário inline na
  tela (Nome, Cor, Ícone, botões Cancelar/Salvar); enquanto aberto,
  submeter com sucesso recolhe o formulário e a lista já mostra a
  categoria criada; cancelar recolhe sem chamar a API
- Cada linha da lista tem um botão "Editar" que expande a própria
  linha num formulário inline pré-preenchido com os dados atuais
  (mesmos campos do cadastro); salvar com sucesso recolhe a linha e
  atualiza os dados exibidos; cancelar recolhe sem chamar a API
- Somente um formulário (cadastro OU edição de uma linha) fica aberto
  por vez — abrir outro fecha o anterior, sem perder dados não salvos
  de forma confusa (simplesmente descarta o que não foi salvo)
- Se a categoria sendo editada for excluída por outra sessão
  (`404`/`NotFoundError` ao salvar), a linha de edição fecha
  silenciosamente e a lista se atualiza, sem exibir erro — mesmo
  tratamento silencioso já usado para despesas
- Exclusão de categoria continua com o mesmo popup de confirmação,
  recriado no Modernist (`.dialog-backdrop`/`.dialog`), preservando
  texto, estado de carregamento e tratamento de erro atuais
- As rotas `/categories/new` e `/categories/:id/edit` são removidas;
  acessar essas URLs diretamente deixa de ter destino próprio (mesma
  decisão já tomada para as rotas de despesa nas FEAT-17/18)
- Sem campo de orçamento mensal, sem barra de progresso de gasto —
  fora do escopo (conceito inexistente no backend hoje)
- Sem mudança de contrato com o backend, sem novo endpoint, sem novo
  recurso AWS

## User stories

### Ver a lista de categorias

- **Given** um usuário autenticado navega para "Categorias"
- **When** a página carrega
- **Then** vê a lista de categorias (ícone, cor, nome) com a
  linguagem visual do Modernist, e o botão "+ Nova categoria"

### Criar uma categoria

- **Given** o usuário clica em "+ Nova categoria"
- **When** o formulário inline expande, ele preenche nome, cor e
  ícone válidos e salva
- **Then** a categoria é criada via `POST /categories`, o formulário
  recolhe, e a nova categoria aparece na lista

### Nome duplicado ao criar

- **Given** o formulário inline de nova categoria aberto
- **When** o usuário salva com um nome já usado por outra categoria
- **Then** vê o erro inline no campo Nome (mesma mensagem de hoje),
  sem recolher o formulário

### Editar uma categoria

- **Given** o usuário clica em "Editar" numa linha da lista
- **When** a linha expande com os dados atuais preenchidos, ele altera
  algo e salva
- **Then** a categoria é atualizada via `PUT /categories/{id}`, a
  linha recolhe, e a lista reflete os dados atualizados

### Categoria excluída durante a edição

- **Given** o usuário está editando uma categoria que, nesse meio
  tempo, foi excluída por outra sessão
- **When** ele tenta salvar (`404`)
- **Then** a linha de edição fecha silenciosamente e a lista se
  atualiza, sem mostrar erro

### Cancelar cadastro ou edição

- **Given** o formulário inline de cadastro ou de edição aberto
- **When** o usuário clica em "Cancelar"
- **Then** o formulário recolhe sem chamar a API, sem alterar dados

### Excluir uma categoria

- **Given** o usuário clica em excluir numa linha da lista
- **When** confirma no popup Modernist de exclusão
- **Then** a categoria é removida via `DELETE /categories/{id}` e some
  da lista, com o mesmo tratamento de erro (inclusive categoria já
  excluída) de hoje

### Rotas antigas não existem mais

- **Given** qualquer navegação
- **When** o usuário (ou um link salvo) tenta acessar `/categories/new`
  ou `/categories/:id/edit`
- **Then** não encontra mais uma página própria — cadastro e edição só
  são acessíveis pelos formulários inline dentro de `/categories`

- O link "Criar categoria" mostrado em `ExpenseForm` quando o usuário
  não tem nenhuma categoria cadastrada (fora do escopo visual desta
  feature) aponta hoje para `/categories/new` — passa a apontar para
  `/categories`, já que a rota antiga deixa de existir; ajuste
  mecânico mínimo, sem migrar `ExpenseForm` nem mudar seu
  comportamento além do destino do link

## Fora do escopo

- Orçamento mensal por categoria e barra de progresso de gasto —
  conceito inexistente no backend hoje
- Qualquer alteração em `backend/`
- Provisionamento ou alteração de infraestrutura AWS
- Mudança na curadoria de ícones (`CATEGORY_ICONS`) ou nas regras de
  validação (`categorySchema`)

## Critérios de aceite

- [x] `CategoriesPage`, `CategoryList`, formulário de categoria e
      `IconPicker` recriados com tokens/classes do Modernist, sem
      classe Tailwind/shadcn remanescente
- [x] "+ Nova categoria" expande um formulário inline (Nome, Cor,
      Ícone); salvar cria a categoria e recolhe; cancelar recolhe sem
      chamar a API
- [x] Cada linha tem um botão "Editar" que expande a própria linha num
      formulário inline pré-preenchido; salvar atualiza e recolhe;
      cancelar recolhe sem chamar a API
- [x] Apenas um formulário (cadastro ou de uma linha) fica aberto por
      vez
- [x] Nome duplicado ao criar/editar exibe erro inline no campo Nome,
      sem fechar o formulário
- [x] 404 ao salvar uma edição (categoria já excluída) fecha a linha
      silenciosamente e atualiza a lista, sem exibir erro
- [x] Exclusão migrada para popup Modernist
      (`.dialog-backdrop`/`.dialog`), preservando todo o comportamento
      atual
- [x] Rotas `/categories/new` e `/categories/:id/edit`,
      `NewCategoryPage`, `EditCategoryPage`, `NewCategoryForm`,
      `EditCategoryForm`, `CategoryFormFields` e `CategoryNotFound`
      removidos
- [x] Nenhuma outra tela do app muda visualmente por causa desta
      feature
- [x] 100% dos testes (unitários/componente) de `features/categories/`
      e `routes/CategoriesPage` passando após a migração (242/242,
      `tsc -b`, `oxlint` e `npm run build` limpos)
