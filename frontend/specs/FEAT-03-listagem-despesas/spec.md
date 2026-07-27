# FEAT-03: Listagem de despesas com filtros

## Objetivo
Entregar a segunda tela funcional pós-login: uma listagem das despesas já
cadastradas (FEAT-02), com filtros por mês, categoria, intervalo de datas
e faixa de valor, consumindo o contrato já existente e implementado no
backend (`GET /expenses`, documentado em
`backend/specs/FEAT-06-consulta-despesas/spec.md`). Esta feature é
somente leitura: não altera nem cria despesas.

## Contexto
A FEAT-02 entregou o cadastro de despesas, mas hoje não existe nenhuma
forma de visualizá-las no frontend — o usuário cadastra "às cegas". O
backend já expõe `GET /expenses` com todos os filtros e paginação por
cursor necessários (FEAT-06, implementado). Esta feature cobre
exclusivamente a construção da tela que consome esse endpoint.

A tela de listagem passa a ser acessível a partir da tela de cadastro
(FEAT-02) e vice-versa, por meio de navegação simples entre as duas
telas — ambas protegidas pela mesma `ProtectedRoute` já existente
(FEAT-01).

## Requisitos de negócio
- A listagem exibe, por despesa: descrição, valor (formatado como
  moeda, a partir de `amountInCents`), categoria e data (`expenseDate`)
- Filtros disponíveis na tela, todos opcionais e combináveis entre si,
  espelhando o contrato do backend:
  - Mês (`yearMonth`, seleção de mês/ano)
  - Categoria (mesmo enum fechado usado no cadastro, FEAT-02)
  - Intervalo de datas (data inicial/final)
  - Faixa de valor (mínimo/máximo)
- Sem nenhum filtro aplicado, a tela carrega todas as despesas do
  usuário autenticado, paginadas
- Resultados sempre ordenados da despesa mais recente para a mais
  antiga (ordem já garantida pelo backend — o frontend não reordena)
- Paginação client-side reflete a paginação por cursor do backend:
  o usuário aciona carregar mais resultados (ex.: botão "Carregar
  mais"), que busca a próxima página usando o `nextCursor` da resposta
  anterior; não há input de número de página
- Ajustar qualquer filtro reinicia a listagem do zero (nova busca a
  partir da primeira página, descartando cursor anterior)
- Se a combinação de filtros não retornar nenhuma despesa, a tela exibe
  um estado vazio claro (não uma lista em branco sem explicação)
- Erros retornados pela API (400, 401) são tratados e exibidos de forma
  amigável, sem expor detalhes técnicos da resposta
- Se a chamada retornar 401 (sessão expirada durante o uso), o usuário
  é informado e reconduzido à tela de login — mesmo comportamento já
  estabelecido em `FEAT-01-setup-login` e `FEAT-02-cadastro-despesa`
- A tela só é acessível com sessão válida (`ProtectedRoute` da FEAT-01)
  — sem sessão, redireciona para `/login`
- Existe navegação simples entre a tela de cadastro (FEAT-02) e a tela
  de listagem (esta feature), nos dois sentidos

## User stories

### Listar despesas sem filtros
Given um usuário autenticado com despesas cadastradas
When ele acessa a tela de listagem sem aplicar nenhum filtro
Then a aplicação chama `GET /expenses` sem parâmetros de filtro e exibe
as despesas retornadas, ordenadas da mais recente para a mais antiga

### Filtrar por mês
Given um usuário autenticado na tela de listagem
When ele seleciona um mês e aplica o filtro
Then a aplicação chama `GET /expenses?yearMonth=YYYY-MM` e exibe somente
as despesas daquele mês

### Filtrar por categoria
Given um usuário autenticado na tela de listagem
When ele seleciona uma categoria e aplica o filtro
Then a aplicação chama `GET /expenses?category=X` e exibe somente as
despesas daquela categoria

### Filtrar por intervalo de datas
Given um usuário autenticado na tela de listagem
When ele informa uma data inicial e final e aplica o filtro
Then a aplicação chama `GET /expenses?dateFrom=...&dateTo=...` e exibe
somente despesas com `expenseDate` dentro do intervalo

### Filtrar por faixa de valor
Given um usuário autenticado na tela de listagem
When ele informa um valor mínimo e/ou máximo e aplica o filtro
Then a aplicação chama `GET /expenses` com `minAmountInCents`/
`maxAmountInCents` correspondentes e exibe somente despesas dentro da
faixa

### Combinar múltiplos filtros
Given um usuário autenticado na tela de listagem
When ele aplica mês, categoria, intervalo de datas e faixa de valor ao
mesmo tempo
Then a aplicação chama `GET /expenses` com todos os parâmetros
combinados e exibe apenas as despesas que satisfazem todos os filtros

### Trocar de filtro reinicia a listagem
Given um usuário autenticado com uma listagem já carregada (com ou sem
paginação adicional já buscada)
When ele altera qualquer filtro e aplica novamente
Then a aplicação descarta os resultados/cursor anteriores e busca a
primeira página novamente com os novos filtros

### Carregar mais resultados
Given um usuário autenticado com mais despesas do que o tamanho de
página retornado
When ele aciona a ação de carregar mais
Then a aplicação usa o `nextCursor` da resposta anterior para buscar e
anexar a próxima página, sem repetir nem pular despesas

### Nenhum resultado encontrado
Given um usuário autenticado
When os filtros aplicados não correspondem a nenhuma despesa
Then a aplicação exibe um estado vazio claro, sem erro

### Erro inesperado da API (400)
Given um usuário autenticado na tela de listagem
When a API retorna 400 (ex.: combinação de filtros inconsistente que
escapou da validação client-side)
Then a aplicação exibe uma mensagem de erro genérica, sem quebrar a tela

### Sessão expirada durante o uso
Given um usuário com sessão expirada usando a tela de listagem
When a API retorna 401 ao buscar despesas
Then a aplicação informa que a sessão expirou e redireciona para
`/login`

### Acesso à tela sem sessão válida
Given um usuário sem sessão válida
When ele tenta acessar a tela de listagem diretamente pela URL
Then a aplicação redireciona para `/login` (comportamento herdado da
rota protegida, `FEAT-01-setup-login`)

### Navegar entre cadastro e listagem
Given um usuário autenticado em uma das duas telas (cadastro ou
listagem)
When ele aciona a navegação para a outra tela
Then a aplicação exibe a tela correspondente, sem exigir novo login

## Contratos da API observáveis
Este FEAT consome o contrato já definido e implementado no backend
(`backend/specs/FEAT-06-consulta-despesas/spec.md` /
`backend/docs/openapi.json`), reproduzido aqui apenas como referência de
integração:

### GET /expenses
Header: `Authorization: Bearer <token>`

Query params (todos opcionais, combináveis): `yearMonth` (`YYYY-MM`),
`category` (enum fechado, mesmo do cadastro), `dateFrom`/`dateTo`
(`YYYY-MM-DD`), `minAmountInCents`/`maxAmountInCents`, `cursor`,
`limit`.

Response 200:
```json
{
  "items": [
    {
      "id": "...",
      "description": "Almoço no restaurante",
      "amountInCents": 4590,
      "category": "Alimentacao",
      "expenseDate": "2025-06-15",
      "createdAt": "2025-06-15T12:34:56Z"
    }
  ],
  "nextCursor": "opaque-token-or-null"
}
```

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "Um ou mais filtros são inválidos."
}
```

Response 401 (unauthorized):
```json
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Unauthorized",
  "status": 401
}
```

Valores aceitos para `category` (mesmo enum fechado do backend e do
cadastro, FEAT-02): `Alimentacao`, `Transporte`, `Moradia`, `Saude`,
`Educacao`, `Lazer`, `ComprasEServicos`, `Outros`.

## Critérios de aceite
- [x] Tela de listagem acessível via rota protegida, exibindo despesas
      do usuário autenticado
- [x] Sem filtros, lista todas as despesas do usuário, paginadas,
      ordenadas da mais recente para a mais antiga
- [x] Filtro por mês (`yearMonth`) funcional, isolado e combinado com
      outros filtros
- [x] Filtro por categoria funcional, isolado e combinado com outros
      filtros
- [x] Filtro por intervalo de datas funcional, isolado e combinado com
      outros filtros
- [x] Filtro por faixa de valor funcional, isolado e combinado com
      outros filtros
- [x] Alterar qualquer filtro reinicia a listagem a partir da primeira
      página
- [x] Ação de carregar mais busca a próxima página via `nextCursor`,
      sem repetir nem pular despesas
- [x] Estado vazio claro quando nenhuma despesa corresponde aos filtros
- [x] Erro 400 da API exibe mensagem genérica sem quebrar a tela
- [x] Erro 401 exibe aviso de sessão expirada e redireciona para
      `/login`
- [x] Acesso à tela sem sessão válida redireciona para `/login`
- [x] Navegação simples entre a tela de cadastro (FEAT-02) e a tela de
      listagem, nos dois sentidos
- [x] Testes cobrindo: aplicação de cada filtro isoladamente e
      combinados, paginação via "carregar mais", estado vazio, erro 400
      e erro 401

## Status

Implementado. `expensesApi.getExpenses`, `useExpensesQuery`,
`expenseFilterSchema`, `ExpenseFilters`, `ExpenseList`,
`formatCentsToCurrency`, `InvalidFilterError`/`UnknownExpenseQueryError`
(reaproveitando `SessionExpiredError`/`NetworkError` já existentes),
`routes/ExpensesListPage.tsx` e a rota `/expenses` implementados
conforme `plan.md`. Suíte completa (`npm test`) passa: 67/67 testes.
`tsc -b` e `vite build` sem erros; `oxlint` sem erros (dois warnings
pré-existentes/aceitos, sem relação com bugs).

Validação manual: dev server e build de produção confirmados sem erro
de compilação; todos os módulos novos servidos corretamente pelo Vite.
O fluxo end-to-end real (login → navegar para "Ver despesas" → aplicar
filtros → ver despesas reais) não pôde ser validado no ambiente de
desenvolvimento (sem backend/credenciais AWS locais disponíveis), mas
foi validado manualmente pelo usuário com o backend real — feature
confirmada funcionando ponta a ponta.

## Fora do escopo deste FEAT
- Criação, edição ou exclusão de despesas (cobertas por outras
  features)
- Ordenação por campo diferente de data (mesma limitação do backend,
  FEAT-06)
- Busca textual livre na descrição
- Filtro por tipo `despesa`/`receita` (backend só tem `despesa` hoje)
- Exportação dos resultados (CSV, PDF etc.)
- Gráficos/dashboard agregados (dependem de endpoint analítico ainda
  não existente no backend)
- Edição de despesa diretamente a partir da listagem (ex.: clique para
  editar) — feature futura separada
