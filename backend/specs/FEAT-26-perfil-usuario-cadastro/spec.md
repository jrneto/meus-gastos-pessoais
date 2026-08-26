# FEAT-26: Perfil do usuário no cadastro (nome, telefone, CPF)

## Objetivo

Ampliar `POST /auth/register` para capturar, além de email e senha,
**nome**, **telefone** e **CPF** do usuário — dados hoje inexistentes
no cadastro. `GET /auth/me` passa a expor esses mesmos campos.

## Contexto

Inserida fora da ordem original do `backend/docs/roadmap.md` — decisão
do usuário de priorizar esta feature antes da antiga FEAT-26 ("E-mail
de boas-vindas", renumerada para FEAT-27; "Seed de categorias padrão"
renumerada para FEAT-28).

**Decisões fechadas com o usuário durante o `/specify`:**

1. **Armazenamento**: nome, telefone e CPF ficam num **novo item por
   usuário no DynamoDB** (perfil), não em atributos do Cognito. Motivo:
   `name` e `phone_number` são atributos padrão do Cognito (poderiam ser
   usados sem mudar o User Pool), mas **CPF não tem atributo padrão** —
   exigiria um atributo customizado (`custom:cpf`), e atributos
   customizados só podem ser definidos **na criação** do User Pool.
   Adicionar depois recriaria o pool de produção do zero
   (`deletion_protection = "ACTIVE"`), com perda de todos os usuários
   reais. Pra manter uma única fonte de verdade pro perfil (em vez de
   dividir entre Cognito e DynamoDB) e não tocar em infraestrutura
   nenhuma, os três campos ficam no DynamoDB. O Cognito continua
   responsável só por identidade/autenticação (email + senha).
2. **Obrigatoriedade**: os três campos são **obrigatórios já no
   registro** — `POST /auth/register` passa a exigir email, senha,
   nome, telefone e CPF juntos. É uma mudança de contrato (breaking
   change) do endpoint já publicado.
3. **Unicidade do CPF**: CPF é **único entre usuários** (mesma regra já
   aplicada ao email) e validado pelo algoritmo oficial de dígito
   verificador — cadastro com CPF inválido ou já usado por outro
   usuário é rejeitado.
4. **Formato do telefone**: só dígitos, DDD + número (10 ou 11 dígitos,
   sem `+55`, sem máscara) — mesmo padrão adotado para o CPF.

## Requisitos de negócio

- `POST /auth/register` exige `email`, `password`, `name`,
  `phoneNumber` e `cpf` — ausência de qualquer um retorna 400
- `name`: obrigatório; após `Trim()`, precisa ter entre 2 e 150
  caracteres; sem outra restrição de formato (aceita nomes compostos,
  acentos, hífen)
- `phoneNumber`: obrigatório; deve conter **somente dígitos** e ter 10
  ou 11 caracteres (DDD + número, com ou sem o 9º dígito do celular);
  qualquer caractere não numérico (`.`, `-`, `(`, `)`, espaço, `+55`)
  torna o valor inválido → 400
- `cpf`: obrigatório; deve conter **somente dígitos**, ter exatamente
  11 caracteres, e ser um CPF matematicamente válido (algoritmo oficial
  de dígito verificador); sequências com todos os dígitos iguais
  (`00000000000`, `11111111111`, ..., `99999999999`) são sempre
  inválidas mesmo quando o cálculo do dígito verificador "fecha" —
  regra padrão de validação de CPF no Brasil
- CPF já usado por outro usuário retorna 409 (`cpf-already-exists`) —
  checagem independente da checagem de email duplicado (já existente)
- Nome, telefone e CPF são armazenados vinculados ao `userId` do
  usuário recém-criado no Cognito — nunca antes da confirmação do
  `SignUp`
- Se a gravação do perfil (nome/telefone/cpf) falhar **depois** do
  Cognito já ter criado o usuário, o cadastro é desfeito por completo
  (o usuário é removido do Cognito) e a API retorna 500 — nunca fica
  uma conta "pela metade" (criada no Cognito, sem perfil, bloqueando um
  novo cadastro com o mesmo email)
- `GET /auth/me` passa a retornar `name`, `phoneNumber` e `cpf`
  gravados no registro, além dos campos já existentes (`userId`,
  `email`)
- Validação de `email`/`password` já existente (FEAT-01) não muda
- Sem migração de dados: usuários cadastrados antes desta feature não
  têm perfil — fora do escopo tratar esse caso (decisão já registrada
  em `backend/docs/roadmap.md`: tabela pode ser recriada do zero, sem
  compatibilidade retroativa)

## User Stories

**US1 — Cadastro completo com sucesso**
- Given um email ainda não cadastrado
- When o cliente chama `POST /auth/register` com `email`, `password`,
  `name`, `phoneNumber` e `cpf` válidos
- Then a API retorna 201 com `userId`, `email`, `name`, `phoneNumber` e
  `cpf` no corpo

**US2 — Campo obrigatório ausente**
- Given uma requisição de registro
- When falta `name`, `phoneNumber` ou `cpf` (ausente, vazio ou só
  espaços)
- Then a API retorna 400, e nenhum usuário é criado no Cognito

**US3 — Telefone em formato inválido**
- Given uma requisição de registro
- When `phoneNumber` vem com pontuação (ex.: `"(11) 99999-8888"`), com
  DDI (ex.: `"+5511999998888"`), ou com menos de 10 ou mais de 11
  dígitos
- Then a API retorna 400, e nenhum usuário é criado

**US4 — CPF matematicamente inválido**
- Given uma requisição de registro
- When `cpf` tem 11 dígitos mas o dígito verificador não confere (ex.:
  `"12345678900"` alterado incorretamente), ou é uma sequência de
  dígitos repetidos (ex.: `"11111111111"`)
- Then a API retorna 400, e nenhum usuário é criado

**US5 — CPF já cadastrado por outro usuário**
- Given um usuário já registrado com CPF `"12345678909"`
- When um novo cadastro é enviado com o mesmo `cpf` (email diferente)
- Then a API retorna 409 (`cpf-already-exists`), e nenhum novo usuário
  é criado

**US6 — Email já cadastrado continua funcionando**
- Given um usuário já registrado com email `"neto@email.com"`
- When um novo cadastro é enviado com o mesmo email (CPF diferente)
- Then a API retorna 409 (`email-already-exists`), como já acontecia
  antes desta feature

**US7 — Consulta ao perfil após o registro**
- Given um usuário registrado com `name`, `phoneNumber` e `cpf`
- When ele chama `GET /auth/me` autenticado
- Then a resposta 200 traz `name`, `phoneNumber` e `cpf` idênticos aos
  enviados no registro, além de `userId` e `email`

**US8 — Falha ao gravar o perfil não deixa conta órfã**
- Given uma requisição de registro válida, cujo `SignUp` no Cognito é
  concluído com sucesso mas a gravação do perfil no DynamoDB falha
  (ex.: erro transiente)
- When a API trata essa falha
- Then a API retorna 500, o usuário criado no Cognito é removido, e uma
  nova tentativa de registro com o mesmo email é aceita normalmente

## Contratos da API

### POST /auth/register

Request:
```json
{
  "email": "neto@email.com",
  "password": "Senha123",
  "name": "Fulano da Silva",
  "phoneNumber": "11999998888",
  "cpf": "12345678909"
}
```

Response 201 (Location: /auth/me):
```json
{
  "userId": "uuid-gerado-pelo-cognito",
  "email": "neto@email.com",
  "name": "Fulano da Silva",
  "phoneNumber": "11999998888",
  "cpf": "12345678909"
}
```

Response 400 (parâmetro ausente ou inválido — email/senha/nome/
telefone/cpf):
```json
{
  "type": "https://gastosapp.dev/errors/bad-request",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Telefone deve conter 10 ou 11 dígitos numéricos."
}
```

Response 409 (email já cadastrado — comportamento já existente):
```json
{
  "type": "https://gastosapp.dev/errors/email-already-exists",
  "title": "Conflito",
  "status": 409,
  "detail": "Email já cadastrado"
}
```

Response 409 (CPF já cadastrado):
```json
{
  "type": "https://gastosapp.dev/errors/cpf-already-exists",
  "title": "Conflito",
  "status": 409,
  "detail": "CPF já cadastrado"
}
```

### GET /auth/me

Header: `Authorization: Bearer <token>`

Response 200:
```json
{
  "userId": "uuid-do-cognito",
  "email": "neto@email.com",
  "name": "Fulano da Silva",
  "phoneNumber": "11999998888",
  "cpf": "12345678909"
}
```

Sem mudança nos demais comportamentos de `GET /auth/me` (401 sem token,
já documentado na FEAT-01).

### Erros comuns

Formato padrão de erro do projeto
(`GastosApp.Api/Common/ResultHttpExtensions.cs`): `title` fixo e
genérico por tipo de erro (RFC 9457), mensagem específica sempre em
`detail`. Fonte de verdade exata: `backend/docs/openapi.json`.

## Critérios de aceite

- [ ] `POST /auth/register` com `name`, `phoneNumber` e `cpf` válidos
      retorna 201 com os 5 campos no corpo (`userId`, `email`, `name`,
      `phoneNumber`, `cpf`)
- [ ] Ausência de `name`, `phoneNumber` ou `cpf` retorna 400
- [ ] `phoneNumber` fora do formato (não numérico, ou diferente de 10/11
      dígitos) retorna 400
- [ ] `cpf` com dígito verificador inválido, com menos/mais de 11
      dígitos, ou com todos os dígitos iguais retorna 400
- [ ] `cpf` já usado por outro usuário retorna 409
      (`cpf-already-exists`)
- [ ] Comportamento existente de `email` duplicado (409) continua
      funcionando sem alteração
- [ ] `GET /auth/me` retorna `name`, `phoneNumber` e `cpf` gravados no
      registro
- [ ] Falha ao gravar o perfil após o `SignUp` no Cognito reverte o
      cadastro (usuário removido do Cognito) e retorna 500, permitindo
      nova tentativa com o mesmo email
- [ ] Nenhuma mudança no Cognito User Pool (schema/atributos) — todo o
      perfil vive no DynamoDB
- [ ] Todo novo endpoint/campo coberto por teste de componente
- [ ] `backend/docs/openapi.json` regenerado refletindo os novos campos
      de request/response de `POST /auth/register` e `GET /auth/me`

## Fora do escopo

- Edição posterior do perfil (ex.: `PATCH /users/me`) — usuário não
  pode alterar nome/telefone/cpf após o registro nesta feature
- Verificação de telefone via SMS/código
- Unicidade de telefone (só email e CPF são únicos)
- Máscara/formatação de exibição (ex.: `123.456.789-09`) — a API
  trafega só dígitos; formatação visual é responsabilidade do frontend
- Qualquer atributo do Cognito User Pool (`name`, `phone_number`,
  `custom:*`) — o Cognito continua só com `email`
- Migração de dados de usuários cadastrados antes desta feature
- Alteração de CPF de um cadastro existente
