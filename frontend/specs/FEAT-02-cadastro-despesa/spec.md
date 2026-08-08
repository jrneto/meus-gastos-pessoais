# FEAT-02: Cadastro de despesas

## Objetivo
Entregar a primeira tela funcional pós-login: um formulário para o
usuário registrar uma despesa (gasto pessoal), consumindo o contrato
já existente no backend (`POST /expenses`, documentado em
`backend/specs/FEAT-04-registro-despesa/spec.md`). Esta tela **substitui**
o placeholder pós-login criado na FEAT-01 (`HomePage`) — passa a ser a
página real renderizada pela rota protegida (`/`) depois do login.
É o primeiro caso de uso de negócio do produto (controle de gastos
pessoal), e a base sobre a qual a futura listagem/dashboard de despesas
será construída.

## Requisitos de negócio
- Formulário exige: descrição, valor, categoria e data da despesa —
  mesmos campos e regras já validadas pelo backend:
  - Descrição: obrigatória, texto não vazio, até 200 caracteres
  - Valor: obrigatório, número positivo (maior que zero). O usuário
    digita em formato monetário legível (ex.: `45,90`); a conversão
    para centavos (formato que a API espera) acontece no client antes
    do envio
  - Categoria: obrigatória, selecionada dentre um enum fechado (mesmo
    conjunto do backend): Alimentação, Transporte, Moradia, Saúde,
    Educação, Lazer, Compras e Serviços, Outros
  - Data da despesa: obrigatória, pode ser retroativa ou futura (não
    precisa ser a data de hoje)
- Validação client-side espelha as regras acima (Zod), evitando round-trip
  desnecessário à API para erros óbvios
- Erros de validação são exibidos inline, por campo, sem chamar a API
- Após cadastro com sucesso: o formulário é limpo e uma confirmação
  visual é exibida; o usuário permanece na mesma tela, pronto para
  cadastrar a próxima despesa (sem redirecionar)
- Erros retornados pela API (400, 401) são tratados e exibidos de forma
  amigável, sem expor detalhes técnicos da resposta
- Se a chamada retornar 401 (sessão expirada durante o uso), o usuário
  é informado e reconduzido à tela de login — consistente com o
  comportamento já estabelecido em `FEAT-01-setup-login`
- A tela só é acessível com sessão válida (já garantido pela
  `ProtectedRoute` da FEAT-01) — sem sessão, redireciona para `/login`

## User stories

### Cadastro de despesa com sucesso
Given um usuário autenticado na tela de cadastro de despesas
When ele preenche descrição, valor, categoria e data válidos e envia o
formulário
Then a aplicação chama `POST /expenses`, exibe uma confirmação de
sucesso e limpa o formulário para um novo cadastro

### Validação de campos obrigatórios
Given um usuário na tela de cadastro de despesas
When ele tenta enviar o formulário com descrição vazia, valor ausente/
menor ou igual a zero, categoria não selecionada ou data ausente
Then a aplicação exibe o erro correspondente inline, por campo, e não
chama a API

### Erro inesperado da API (400)
Given um usuário autenticado que já passou pela validação client-side
When a API ainda assim retorna 400 (divergência de regra client/API)
Then a aplicação exibe uma mensagem de erro genérica, sem perder os
dados já preenchidos no formulário

### Sessão expirada durante o cadastro
Given um usuário com sessão expirada tentando cadastrar uma despesa
When a API retorna 401 ao enviar o formulário
Then a aplicação informa que a sessão expirou e redireciona para
`/login`

### Acesso à tela sem sessão válida
Given um usuário sem sessão válida
When ele tenta acessar a tela de cadastro de despesas diretamente pela
URL
Then a aplicação redireciona para `/login` (comportamento herdado da
rota protegida, `FEAT-01-setup-login`)

## Contratos da API observáveis
Este FEAT consome o contrato já definido e implementado no backend
(`backend/specs/FEAT-04-registro-despesa/spec.md` /
`backend/docs/openapi.json`), reproduzido aqui apenas como referência
de integração:

### POST /expenses
Header: `Authorization: Bearer <token>`

Request:
```json
{
  "description": "Almoço no restaurante",
  "amountInCents": 4590,
  "category": "Alimentacao",
  "expenseDate": "2025-06-15"
}
```

Response 201 (Location: `/expenses/{id}`):
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

Response 400 (validation-error):
```json
{
  "type": "https://gastosapp.dev/errors/validation-error",
  "title": "Validation Error",
  "status": 400,
  "detail": "Um ou mais campos são inválidos."
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

Valores aceitos para `category` (enum fechado do backend): `Alimentacao`,
`Transporte`, `Moradia`, `Saude`, `Educacao`, `Lazer`,
`ComprasEServicos`, `Outros`.

## Critérios de aceite
- [x] Rota protegida (`/`) passa a renderizar a tela de cadastro de
      despesas em vez do placeholder da FEAT-01
- [x] Formulário com campos descrição, valor, categoria (select com o
      enum fechado) e data
- [x] Validação client-side de todos os campos obrigatórios antes de
      chamar a API, com erro inline por campo
- [x] Valor digitado em formato monetário (ex.: `45,90`) é convertido
      corretamente para centavos antes do envio
- [x] Cadastro com sucesso chama `POST /expenses`, exibe confirmação e
      limpa o formulário, sem sair da tela
- [x] Erro 400 da API exibe mensagem genérica sem perder os dados
      preenchidos
- [x] Erro 401 exibe aviso de sessão expirada e redireciona para
      `/login`
- [x] Acesso à tela sem sessão válida redireciona para `/login`
- [x] Testes unitários cobrindo: validação do formulário, conversão de
      valor para centavos, fluxo de sucesso, erro 400 e erro 401

## Fora do escopo deste FEAT
- Listagem/consulta de despesas (`GET /expenses`) — feature futura
  separada, que também poderá reaproveitar esta tela como parte de um
  dashboard
- Edição ou exclusão de despesa
- Anexar comprovante
- Gráficos/dashboard (Tremor entra só quando essa feature existir)
- Cadastro dinâmico de categorias — usa o enum fechado do backend
- Paginação/filtros