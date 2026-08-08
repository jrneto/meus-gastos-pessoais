# FEAT-01: Setup inicial do frontend + tela de login

## Objetivo
Dar o pontapé inicial do frontend (React), estabelecendo a base de
arquitetura, tooling e testes que as próximas features vão seguir, e
entregar a primeira tela funcional: login integrado à API já existente
(`POST /auth/login`, `GET /auth/me`), autenticando contra o Cognito via
backend. Não é só uma tela — é a fundação (estrutura, padrões, ambiente
de execução local/produção) sobre a qual o restante do app será
construído.

## Requisitos de negócio
- Login exige `email` e `senha`.
- Validação client-side antes de chamar a API: email em formato válido;
  senha com no mínimo 8 caracteres (espelha a regra já aplicada pelo
  backend em `backend/specs/FEAT-01-auth/spec.md`).
- O token retornado no login (IdToken do Cognito) deve ser persistido no
  client para autenticar chamadas subsequentes (ex.: `GET /auth/me`) e o
  acesso à rota protegida.
- A sessão expira conforme o `expiresIn` retornado pelo login; ao
  expirar, o usuário é redirecionado para `/login`.
- Uma rota protegida (placeholder pós-login) só é acessível com token
  válido; sem token (ou token expirado), o acesso redireciona para
  `/login`.
- Deve existir uma ação de logout que limpa o token armazenado e
  redireciona para `/login`.
- Erros de credenciais inválidas (401 da API) exibem mensagem amigável
  ao usuário, sem expor detalhes técnicos da resposta da API.
- Erros de validação (campos vazios, email inválido, senha curta) são
  exibidos inline, por campo, sem chamar a API.
- A aplicação deve poder ser executada localmente apontando tanto para
  a API rodando em ambiente local quanto para a API de produção (AWS),
  trocando apenas configuração de ambiente — sem alterar código.
- Stack de UI solicitada para este projeto: React + TypeScript,
  shadcn/ui, Tailwind CSS, Tremor (gráficos, usado em features
  futuras), React Hook Form e Zod (validação de formulário).

## User stories

### Login com sucesso
Given um usuário com credenciais válidas na página de login
When ele informa email e senha corretos e envia o formulário
Then a aplicação chama `POST /auth/login`, armazena o token retornado
e redireciona para a rota protegida (placeholder pós-login)

### Login com credenciais inválidas
Given um usuário na página de login
When ele informa email/senha que a API rejeita (401)
Then a aplicação exibe uma mensagem amigável de "credenciais
inválidas" e permanece na página de login, sem armazenar token

### Validação de campos antes de chamar a API
Given um usuário na página de login
When ele tenta enviar o formulário com email vazio, email em formato
inválido, senha vazia ou senha com menos de 8 caracteres
Then a aplicação exibe o erro correspondente inline, por campo, e não
chama a API

### Acesso à rota protegida sem sessão
Given um usuário sem token válido armazenado
When ele tenta acessar a rota protegida (placeholder pós-login)
diretamente pela URL
Then a aplicação redireciona para `/login`

### Acesso à rota protegida com sessão válida
Given um usuário com token válido armazenado
When ele acessa a rota protegida
Then a aplicação permite o acesso e exibe o conteúdo placeholder

### Expiração de sessão
Given um usuário com token armazenado cujo tempo de expiração
(`expiresIn`) já passou
When ele acessa (ou está navegando em) a rota protegida
Then a aplicação trata a sessão como inválida, limpa o token e
redireciona para `/login`

### Logout
Given um usuário autenticado na rota protegida
When ele aciona a ação de logout
Then a aplicação limpa o token armazenado e redireciona para `/login`

### Execução local apontando para API local ou de produção
Given um desenvolvedor rodando a aplicação localmente
When ele configura o ambiente para apontar para a API local ou para a
API de produção (AWS)
Then a aplicação faz as chamadas de login/autenticação para a URL
configurada, sem necessidade de alterar código

## Contratos da API observáveis
Este FEAT consome contratos já definidos e implementados no backend
(`backend/specs/FEAT-01-auth/spec.md`), reproduzidos aqui apenas como
referência de integração — o backend é a fonte da verdade:

### POST /auth/login
Request:
```json
{
  "email": "neto@email.com",
  "password": "Senha123"
}
```

Response 200:
```json
{
  "accessToken": "eyJ...",
  "expiresIn": 3600,
  "userId": "uuid-do-cognito"
}
```

Response 401 (credenciais inválidas):
```json
{
  "type": "https://gastosapp.dev/errors/invalid-credentials",
  "title": "Email ou senha inválidos",
  "status": 401
}
```

### GET /auth/me
Header: `Authorization: Bearer <token>`

Response 200:
```json
{
  "userId": "uuid-do-cognito",
  "email": "neto@email.com",
  "name": "Neto"
}
```

Response 401 (token ausente, inválido ou expirado):
```json
{
  "type": "https://gastosapp.dev/errors/unauthorized",
  "title": "Não autorizado",
  "status": 401
}
```

## Dependências / bloqueios
- **CORS no backend**: resolvido em paralelo a este FEAT (mudança Modo
  Leve no contexto backend — política de CORS adicionada em
  `GastosApp.Api/Program.cs`, origens liberadas via configuração
  `Cors:AllowedOrigins`). Pré-requisito para que o navegador consiga
  chamar a API a partir do frontend.
- **URL da API de produção**: ainda não há domínio/URL fixa documentada
  para o frontend (Cognito `callback_urls` está com placeholder, ver
  `backend/infra/terraform/cognito.tf`). Até lá, a configuração de
  ambiente de produção do frontend deve ser preparada para receber essa
  URL quando disponível, sem bloquear o desenvolvimento local.

## Critérios de aceite
- [x] Projeto React (Vite + TypeScript) criado em `frontend/app/`, executável
      localmente
- [x] Tela de login com campos de email e senha, validados
      client-side (formato de email, senha mínima de 8 caracteres)
      antes de chamar a API
- [x] Login com sucesso chama `POST /auth/login`, armazena o token e
      redireciona para a rota protegida
- [x] Login com credenciais inválidas exibe mensagem amigável (401)
- [x] Rota protegida (placeholder) inacessível sem token válido
      (redireciona para `/login`)
- [x] Rota protegida acessível com token válido
- [x] Sessão expirada (`expiresIn` vencido) redireciona para `/login`
- [x] Ação de logout limpa o token e redireciona para `/login`
- [x] Aplicação configurável, via variável de ambiente, para apontar
      para API local ou API de produção AWS, sem alteração de código
- [x] Testes unitários cobrindo: validação do formulário de login,
      fluxo de sucesso, fluxo de erro (401) e proteção de rota
- [x] Backend libera CORS para a origem do frontend em desenvolvimento
      (`http://localhost:5173`), conforme mudança Modo Leve associada

## Fora do escopo deste FEAT
- Tela de cadastro/registro de usuário
- Recuperação de senha
- MFA
- Dashboard funcional (a rota pós-login é apenas um placeholder)
- Deploy do frontend (CloudFront/S3 via Terraform) — infraestrutura
  futura, tratada em `frontend/infra/`
- Testes end-to-end (apenas testes unitários neste FEAT)
- Refresh token automático (backend não oferece refresh token no MVP)
- Dark mode / temas
- Internacionalização
