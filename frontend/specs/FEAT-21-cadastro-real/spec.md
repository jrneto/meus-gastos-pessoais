# FEAT-21: Cadastro real (substituir o modo "Criar conta" fake)

## Objetivo

Substituir o modo "Criar conta" da tela de Login — hoje puramente visual
(`SignupForm`, FEAT-14), que só valida nome/email/senha no client e
navega para uma página fake (`/cadastro-em-breve`) sem chamar nenhuma
API — por um cadastro real, integrado a `POST /auth/register`
(`backend/specs/FEAT-26-perfil-usuario-cadastro`), incluindo os campos
que o backend já exige: nome, telefone e CPF, além de email e senha.

## Contexto

O backend já expõe `POST /auth/register` (FEAT-26) exigindo `email`,
`password`, `name`, `phoneNumber` e `cpf`. O design (`frontend/
design-system/README.md`, "Cadastro de conta") mostra o modo "Criar
conta" pedindo **nome completo, CPF, telefone, e-mail e senha**, com
máscara progressiva em CPF (`000.000.000-00`) e telefone
(`(11) 98765-4321`), os dois lado a lado em grid de 2 colunas no web
(`03-criar-conta-preenchida.png`), e um estado de processamento
(`04-login-processando.png`) — mesma linguagem visual já usada em
outros formulários do Modernist (botão ocupado, spinner, rótulo em
gerúndio).

**Decisão fechada com o usuário durante este `/specify`:** o Cognito
**não confirma o usuário automaticamente** no `SignUp` — hoje (e
propositalmente, por ora) só existe confirmação manual, feita pelo
administrador diretamente no console AWS do Cognito. Isso já é o
comportamento real do backend desde a FEAT-01: se um usuário não
confirmado tenta logar, `POST /auth/login` responde 401 com
`type: "user-not-confirmed"` (`AuthErrors.UserNotConfirmed`) — sem
mudança nenhuma necessária no backend para esta feature. Consequências
diretas para o frontend:

1. **Nenhum login automático após o cadastro.** Diferente do que uma
   leitura rápida do design sugere (cadastro → processando → dashboard
   já autenticado), o fluxo real é cadastro → confirmação visual de que
   a conta foi criada **e está pendente de aprovação** → volta para o
   modo "Entrar". Tentar logar automaticamente com as credenciais recém
   -criadas sempre falharia com `user-not-confirmed` hoje.
2. **O formulário de login precisa diferenciar `user-not-confirmed` de
   `invalid-credentials`.** Hoje `authApi.login` trata qualquer 401 como
   `InvalidCredentialsError` ("Email ou senha inválidos"), o que
   induziria um usuário recém-cadastrado (mas ainda não aprovado) a
   pensar que digitou a senha errada. Passa a inspecionar o campo
   `type` do corpo do erro (RFC 9457, já retornado pela API) para
   escolher a mensagem certa.

Esta feature cobre **cadastro real** e o ajuste mínimo de login
necessário para não confundir o usuário recém-cadastrado. Ela **não**
implementa nenhum fluxo de confirmação/aprovação (nem tela de "aguarde
aprovação" com polling, nem reenvio de confirmação) — isso é feito hoje
manualmente pelo administrador no console AWS, fora do frontend.

## Requisitos de negócio

- Formulário de cadastro pede, nesta ordem: nome, CPF, telefone, email,
  senha — CPF e telefone lado a lado em grid de 2 colunas no web
  (`03-criar-conta-preenchida.png`)
- Validação client-side (Zod), espelhando as regras já aplicadas pelo
  backend (`RegisterUserCommandValidator`), para dar feedback antes do
  submit — a API continua sendo a fonte de verdade final:
  - `name`: obrigatório, após trim entre 2 e 150 caracteres
  - `phoneNumber`: campo aceita digitação com máscara
    `(11) 98765-4321`, mas só os dígitos são validados e enviados à
    API — precisa ter 10 ou 11 dígitos
  - `cpf`: campo aceita digitação com máscara `000.000.000-00`, mas só
    os dígitos são validados e enviados à API — precisa ter 11
    dígitos, dígito verificador válido (mesmo algoritmo do backend) e
    não pode ser uma sequência de dígitos repetidos
    (`00000000000`...`99999999999`)
  - `email`/`password`: mesmas regras já usadas no login (formato de
    email; senha mínima de 8 caracteres)
- Máscaras são progressivas (aplicadas durante a digitação) e usam
  `inputmode="numeric"`, sem permitir mais dígitos que o limite de cada
  campo — mesmo comportamento descrito no design
- Ao submeter com sucesso (`201`), o formulário **não loga o usuário
  automaticamente**: exibe uma confirmação de que a conta foi criada e
  está pendente de aprovação, e retorna ao modo "Entrar" da mesma tela
  (sem navegar para nenhuma rota separada)
- Erros da API mapeados para mensagens específicas:
  - `400` (`validation-error`) → mensagem genérica de dados inválidos
    (o client já bloqueia a maioria dos casos antes do submit; este é
    o fallback para o que escapar da validação local)
  - `409` (`email-already-exists`) → "Este email já está cadastrado."
  - `409` (`cpf-already-exists`) → "Este CPF já está cadastrado."
  - erro de rede → mesma `NetworkError` já usada em login
  - qualquer outro erro (incluindo `500`) → mensagem genérica de erro
    inesperado
- `LoginModeForm` passa a diferenciar, a partir do `type` do corpo do
  erro 401:
  - `user-not-confirmed` → "Sua conta ainda não foi aprovada. Aguarde a
    confirmação do administrador e tente novamente."
  - qualquer outro 401 (`invalid-credentials`) → mensagem já existente
    ("Email ou senha inválidos.")
- Página fake `/cadastro-em-breve` (`SignupComingSoonPage`) e sua rota
  são removidas — não há mais destino "em breve" para o cadastro

## User Stories

**US1 — Cadastro com sucesso**
- Given o modo "Criar conta" da tela de login
- When o usuário preenche nome, CPF, telefone, email e senha válidos e
  submete
- Then a API retorna 201, o formulário exibe confirmação de que a conta
  foi criada e está pendente de aprovação, e a tela volta para o modo
  "Entrar" (sem token de sessão armazenado, sem navegação para o
  dashboard)

**US2 — Campo obrigatório ausente ou inválido (validação client-side)**
- Given o formulário de cadastro
- When o usuário tenta submeter com nome vazio, telefone com menos de
  10 dígitos, ou CPF com dígito verificador inválido
- Then o submit é bloqueado no client, sem chamar a API, exibindo a
  mensagem de erro do campo correspondente

**US3 — Email já cadastrado**
- Given um email já registrado no backend
- When o usuário tenta se cadastrar novamente com esse email (CPF
  diferente)
- Then a API retorna 409 (`email-already-exists`) e o formulário exibe
  "Este email já está cadastrado.", sem sair da tela de cadastro

**US4 — CPF já cadastrado**
- Given um CPF já registrado no backend
- When o usuário tenta se cadastrar com esse CPF (email diferente)
- Then a API retorna 409 (`cpf-already-exists`) e o formulário exibe
  "Este CPF já está cadastrado.", sem sair da tela de cadastro

**US5 — Tentativa de login antes da aprovação**
- Given uma conta recém-cadastrada, ainda não confirmada pelo
  administrador no console do Cognito
- When o usuário tenta logar com o email e senha que acabou de
  cadastrar
- Then a API retorna 401 (`user-not-confirmed`) e o formulário de login
  exibe "Sua conta ainda não foi aprovada. Aguarde a confirmação do
  administrador e tente novamente." — nunca a mensagem de credenciais
  inválidas

**US6 — Login com senha errada continua distinto**
- Given uma conta já aprovada
- When o usuário tenta logar com a senha errada
- Then a API retorna 401 (`invalid-credentials`) e o formulário de
  login continua exibindo "Email ou senha inválidos." (comportamento já
  existente, sem regressão)

**US7 — Erro de rede no cadastro**
- Given o formulário de cadastro preenchido corretamente
- When a chamada a `POST /auth/register` falha por erro de rede
- Then o formulário exibe a mesma mensagem de erro de rede já usada no
  login, sem perder os dados preenchidos

## Contratos consumidos (já implementados no backend, sem mudança)

### POST /auth/register

Ver contrato completo em
`backend/specs/FEAT-26-perfil-usuario-cadastro/spec.md`. Resumo do que
o frontend envia/recebe:

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

Response 201 — sem token de sessão (só dados do usuário criado):
```json
{
  "userId": "uuid-gerado-pelo-cognito",
  "email": "neto@email.com",
  "name": "Fulano da Silva",
  "phoneNumber": "11999998888",
  "cpf": "12345678909"
}
```

Erros: `400` (`validation-error`), `409` (`email-already-exists`),
`409` (`cpf-already-exists`), `500` (falha ao gravar perfil — já
tratada/revertida pelo backend, permitindo nova tentativa).

### POST /auth/login (mudança de tratamento, não de contrato)

O contrato já existe (FEAT-01); o que muda é só a leitura do erro pelo
frontend. Response 401 hoje já distingue, via `type`:
```json
{
  "type": "https://gastosapp.dev/errors/user-not-confirmed",
  "title": "Não autorizado",
  "status": 401,
  "detail": "Usuário não confirmado. Por favor, confirme seu email antes de fazer login."
}
```
versus
```json
{
  "type": "https://gastosapp.dev/errors/invalid-credentials",
  "title": "Não autorizado",
  "status": 401,
  "detail": "Email ou senha inválidos"
}
```

## Critérios de aceite

- [x] Cadastro com nome, CPF, telefone, email e senha válidos chama
      `POST /auth/register`, recebe 201 e exibe confirmação de conta
      criada/pendente de aprovação, voltando ao modo "Entrar"
- [x] Nenhum token é armazenado nem navegação para rota autenticada
      acontece após o cadastro
- [x] Validação client-side (Zod) bloqueia submit com nome, telefone ou
      CPF inválidos, com mensagens por campo
- [x] CPF e telefone exibem máscara progressiva durante a digitação
      (`000.000.000-00` e `(11) 98765-4321`), enviando à API somente os
      dígitos
- [x] 409 `email-already-exists` exibe "Este email já está cadastrado."
- [x] 409 `cpf-already-exists` exibe "Este CPF já está cadastrado."
- [x] 400 (`validation-error`) exibe mensagem genérica de dados
      inválidos
- [x] Erro de rede no cadastro exibe a mesma `NetworkError` já usada no
      login, sem perder os dados preenchidos
- [x] Login com 401 `user-not-confirmed` exibe mensagem específica de
      conta pendente de aprovação, distinta de credenciais inválidas
- [x] Login com 401 `invalid-credentials` continua exibindo a mensagem
      já existente, sem regressão
- [x] `SignupComingSoonPage` e a rota `/cadastro-em-breve` são removidas
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima
      (sucesso, cada erro 4xx, erro de rede, distinção dos dois 401 no
      login)

## Fora do escopo

- Qualquer fluxo de confirmação/aprovação de conta pelo frontend (tela
  de "aguardando aprovação" com polling, reenvio de código, etc.) — a
  aprovação continua manual, feita pelo administrador no console AWS do
  Cognito
- Login automático após o cadastro (decisão fechada acima — hoje
  sempre falharia com `user-not-confirmed`)
- Edição de perfil (nome/telefone/CPF) após o cadastro — fora do escopo
  também no backend (FEAT-26, "Fora do escopo")
- Recuperação de senha / "esqueci minha senha"
- Qualquer mudança no backend — `POST /auth/register` e `POST
  /auth/login` já implementam tudo que esta feature precisa
