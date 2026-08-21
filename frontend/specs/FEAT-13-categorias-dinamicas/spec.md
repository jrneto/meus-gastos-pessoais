# FEAT-13: Categorias dinâmicas

## Objetivo

Permitir que o usuário gerencie suas próprias categorias de despesa
(criar, editar, excluir, listar) através de uma tela própria, e migrar
todo o fluxo de despesas (cadastro, edição, listagem, filtro) do enum
fechado hardcoded hoje no frontend (`category`) para as categorias
dinâmicas do usuário (`categoryId`), alinhando com o contrato já
implementado no backend
(`backend/specs/FEAT-16-crud-categorias/spec.md` e
`backend/specs/FEAT-17-despesas-categoria-dinamica/spec.md`).

## Contexto

O backend já removeu o enum fechado `ExpenseCategory` e passou a exigir
`categoryId` (referência a uma categoria própria do usuário, criada via
`POST /categories`) em `POST`/`GET`/`PUT /expenses` — mudança já
mergeada em `develop` (FEAT-16/FEAT-17). O frontend ainda usa um enum
fixo local (`EXPENSE_CATEGORIES`, em
`features/expenses/constants/expenseCategories.ts`) e envia/recebe o
campo antigo `category`: está desalinhado do contrato real da API, e o
cadastro/edição/listagem de despesas hoje não funciona contra o backend
atual.

Esta feature cobre as duas pontas dessa lacuna: (1) uma tela nova de
gestão de categorias e (2) a migração do fluxo de despesas para usar
essas categorias.

O campo `icone` é validado pelo backend apenas quanto a
presença/tamanho (até 50 caracteres, sem catálogo fechado — ver FEAT-16).
O frontend oferece uma lista curada de ícones (`lucide-react`, já
dependência do projeto) como picker visual: o usuário escolhe visualmente,
e o nome do ícone escolhido (ex.: `"utensils"`) é o valor de texto
enviado como `icone`. Essa curadoria é uma conveniência de UI, não uma
regra de negócio do backend.

## Requisitos de negócio

- Categoria pertence sempre ao usuário autenticado (garantido pelo
  backend via JWT); o frontend nunca precisa/tem como informar o dono
- `nome`: obrigatório, texto não vazio, até 50 caracteres. Validação
  client-side (Zod) espelha essa regra; a unicidade por slug (ver
  FEAT-16) é responsabilidade do backend — o frontend apenas exibe o
  erro 422 retornado
- `cor`: obrigatória, selecionada via color picker, formato `#RRGGBB`
- `icone`: obrigatório, selecionado através de uma grade de ícones
  curados (lucide-react); o nome do ícone escolhido é o valor enviado
- Lista de categorias vazia quando o usuário nunca criou nenhuma — sem
  criação automática/seed (mesma regra do backend)
- Criar/editar categoria com `nome` já usado (422 `name-conflict`):
  erro inline no campo `nome`, formulário não é limpo
- Criar/editar categoria com dado inválido (400 `validation-error`):
  erro inline no(s) campo(s) correspondente(s)
- Excluir categoria com despesas associadas (422 `category-in-use`):
  mensagem explicando que a categoria não pode ser excluída enquanto
  houver despesas vinculadas; categoria permanece na lista
- Excluir/editar categoria inexistente ou de outro usuário (404): trata
  como erro genérico (não deve ocorrer via UI normal, já que a lista só
  mostra categorias do próprio usuário)
- Formulário de despesa (cadastro e edição) passa a:
  - Buscar `GET /categories` para popular o `Select` de categoria, no
    lugar do enum `EXPENSE_CATEGORIES` local
  - Enviar `categoryId` (não mais `category`) em `POST`/`PUT /expenses`
  - Se o usuário não tiver nenhuma categoria cadastrada, o formulário
    orienta a criar uma categoria primeiro (com atalho para a tela de
    categorias) em vez de exibir um `Select` vazio
- Filtro de despesas por categoria passa a filtrar por `categoryId` (em
  vez de `category`), continuando opcional ("Todas" como valor vazio)
- Listagem/detalhe de despesa resolve `categoryId` para nome/cor/ícone
  usando os dados já buscados de `GET /categories` — a resposta de
  despesa não traz esses dados embutidos (decisão do backend, ver
  FEAT-17); se o `categoryId` de uma despesa não corresponder a nenhuma
  categoria carregada (categoria foi excluída, cenário raro pois
  exclusão é bloqueada com despesas associadas — mas poderia ocorrer
  por dado legado), exibir um rótulo genérico de categoria não
  encontrada em vez de quebrar a tela
- Erros 401 (sessão expirada) em qualquer operação de categoria ou
  despesa seguem o padrão já estabelecido (`SessionExpiredError`,
  redireciona para `/login`)

## User Stories

**US1 — Consultar sem categorias cadastradas**
- Given um usuário autenticado que nunca criou nenhuma categoria
- When ele acessa a tela de categorias
- Then vê uma lista vazia, com uma chamada para criar a primeira
  categoria

**US2 — Consultar categorias já cadastradas**
- Given um usuário autenticado com categorias já criadas
- When ele acessa a tela de categorias
- Then vê a lista com nome, cor e ícone de cada categoria

**US3 — Criar categoria com sucesso**
- Given um usuário autenticado na tela de categorias
- When ele preenche nome, escolhe uma cor e um ícone válidos e envia o
  formulário
- Then a categoria é criada, aparece na lista, e uma confirmação visual
  é exibida

**US4 — Impedir nome de categoria duplicado**
- Given um usuário autenticado com uma categoria "Lazer"
- When ele tenta criar (ou editar outra categoria para) o nome "Lazer"
- Then a API retorna 422 e a tela exibe erro inline no campo nome, sem
  criar/alterar a categoria

**US5 — Validar dados obrigatórios**
- Given um usuário autenticado no formulário de categoria
- When ele tenta enviar sem nome, sem cor válida ou sem ícone
  selecionado
- Then a aplicação exibe erro inline por campo e não chama a API

**US6 — Editar categoria existente**
- Given um usuário autenticado com uma categoria sua
- When ele altera nome, cor e/ou ícone com dados válidos e salva
- Then a categoria é atualizada na lista e uma confirmação é exibida

**US7 — Excluir categoria sem despesas associadas**
- Given um usuário autenticado com uma categoria sem despesas
  vinculadas
- When ele confirma a exclusão dessa categoria
- Then a categoria é removida da lista

**US8 — Impedir exclusão de categoria com despesas associadas**
- Given um usuário autenticado com uma categoria vinculada a pelo menos
  uma despesa
- When ele tenta excluir essa categoria
- Then a API retorna 422 e a tela exibe mensagem explicando que existem
  despesas associadas; a categoria permanece na lista

**US9 — Cadastrar despesa escolhendo categoria própria**
- Given um usuário autenticado com ao menos uma categoria cadastrada
- When ele preenche o formulário de despesa selecionando uma dessas
  categorias e envia
- Then a aplicação chama `POST /expenses` com o `categoryId`
  correspondente, exibe confirmação e limpa o formulário

**US10 — Cadastrar despesa sem nenhuma categoria cadastrada**
- Given um usuário autenticado sem nenhuma categoria própria
- When ele acessa o formulário de cadastro de despesa
- Then a aplicação orienta a criar uma categoria antes de continuar, em
  vez de exibir um seletor de categoria vazio

**US11 — Editar despesa trocando de categoria**
- Given um usuário autenticado com uma despesa e duas categorias
  próprias
- When ele edita a despesa trocando para a outra categoria e salva
- Then a aplicação chama `PUT /expenses/{id}` com o novo `categoryId`,
  e a listagem/detalhe passam a refletir a nova categoria

**US12 — Filtrar despesas por categoria**
- Given um usuário autenticado com despesas em categorias diferentes
- When ele seleciona uma categoria no filtro da listagem de despesas
- Then a aplicação chama `GET /expenses?categoryId=...` e exibe somente
  as despesas daquela categoria

**US13 — Sessão expirada em qualquer operação**
- Given um usuário com sessão expirada
- When ele tenta consultar, criar, editar ou excluir categorias, ou
  cadastrar/editar/listar/filtrar despesas
- Then a API retorna 401, a aplicação informa que a sessão expirou e
  redireciona para `/login`

## Contratos da API observáveis

Contrato de wire completo (endpoints, schemas, status codes) já
implementado e documentado em `backend/docs/openapi.json` — fonte
primária para as chamadas HTTP. Regras de negócio/validação:
`backend/specs/FEAT-16-crud-categorias/spec.md` (categorias) e
`backend/specs/FEAT-17-despesas-categoria-dinamica/spec.md` (despesas
com `categoryId`). Reproduzido abaixo apenas como referência de
integração:

### GET /categories

Response 200:
```json
{
  "items": [
    { "id": "...", "nome": "Alimentacao", "cor": "#F97316", "icone": "utensils", "createdAt": "2025-06-15T12:34:56Z" }
  ]
}
```

### POST /categories

Request:
```json
{ "nome": "Viagem", "cor": "#0EA5E9", "icone": "plane" }
```
Response 201: mesmo formato do item acima. 400 (`validation-error`), 422 (`name-conflict`).

### PUT /categories/{id}

Request: mesmo formato do `POST`. Response 200: item atualizado.
400 (`validation-error`), 404 (`not-found`), 422 (`name-conflict`).

### DELETE /categories/{id}

Response 204. 404 (`not-found`), 422 (`category-in-use`).

### POST /expenses

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "categoryId": "7f3e9a10-4b2c-4d1a-9e8f-2c1b3a4d5e6f",
  "expenseDate": "2025-06-15"
}
```
Response 201: mesmo payload + `id`/`createdAt`. 400 (`validation-error`,
inclui `categoryId` inexistente ou de outro usuário), 401.

### GET /expenses

Filtro `categoryId` (antes `category`) substitui o antigo filtro por
enum; demais filtros inalterados (ver
`backend/specs/FEAT-06-consulta-despesas/spec.md`). Itens da lista/
detalhe trazem `categoryId` no lugar de `category`.

### PUT /expenses/{id}

Mesmo formato do `POST`. 400 (`validation-error`), 404 (`not-found`).

### Erros comuns

Formato padrão RFC 9457 já usado em toda a API (`ResultHttpExtensions.BuildProblem`
no backend) — `title` fixo por tipo de erro, mensagem específica em
`detail`. Ver exemplos completos em
`backend/specs/FEAT-16-crud-categorias/spec.md` (seção "Erros comuns a
todas as rotas").

## Critérios de aceite

- [x] Nova tela `/categories` (com item de navegação no `AppShell`)
      lista as categorias do usuário, vazia quando ele não tem nenhuma
- [x] Formulário de criar categoria com nome, color picker e ícone
      picker (lista curada `lucide-react`), com validação client-side
      (Zod) espelhando as regras do backend
- [x] Criar categoria com sucesso atualiza a lista e exibe confirmação
- [x] Criar/editar categoria com nome duplicado exibe erro inline (422)
- [x] Criar/editar categoria com dado inválido exibe erro inline (400)
- [x] Editar categoria existente atualiza nome/cor/ícone na lista
- [x] Excluir categoria sem despesas associadas remove da lista (204)
- [x] Excluir categoria com despesas associadas exibe mensagem de
      bloqueio e mantém a categoria (422)
- [x] Formulário de cadastro/edição de despesa usa `Select` populado
      via `GET /categories`, enviando `categoryId` (não mais `category`)
      em `POST`/`PUT /expenses`
- [x] Usuário sem nenhuma categoria cadastrada é orientado a criar uma
      antes de conseguir cadastrar despesa
- [x] Filtro de despesas por categoria usa `categoryId` e retorna
      somente despesas daquela categoria
- [x] Listagem/detalhe de despesa exibe nome/cor/ícone da categoria
      resolvendo `categoryId` via `GET /categories`, sem quebrar quando
      não encontrar correspondência
- [x] Nenhuma referência residual ao enum antigo `EXPENSE_CATEGORIES`/
      campo `category` permanece no código de despesas
- [x] Erros 401 em qualquer fluxo acima redirecionam para `/login`
- [x] 100% dos testes (unitários/componente) passando

## Fora do escopo

- Criação automática de categorias padrão (seed) para todo usuário novo
  — decisão adiada desde a FEAT-16 do backend, continua fora de escopo
- Catálogo fechado de ícones no backend — a curadoria de ícones é só
  uma conveniência de UI do frontend
- Reordenação/exibição customizada de categorias
- Migração/backfill de despesas antigas gravadas com valores do enum
  removido — dado legado será tratado manualmente, fora desta feature
- Provisionamento de qualquer infraestrutura AWS nova (esta feature não
  introduz recurso AWS novo, só consome endpoints já existentes)
