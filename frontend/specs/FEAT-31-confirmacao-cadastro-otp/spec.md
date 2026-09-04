# FEAT-31: Confirmação de cadastro via código (OTP)

## Objetivo

Substituir a tela de "conta criada, aguarde aprovação do administrador"
(FEAT-21) por um fluxo real de confirmação: depois de `POST
/auth/register` ter sucesso, o usuário é levado direto para uma tela de
código de 6 dígitos (OTP) que o Cognito já envia por email no `SignUp`.
Código correto confirma a conta via `POST /auth/confirm`
(`backend/specs/FEAT-35-confirmacao-cadastro-otp`, já implementado) e
devolve o usuário ao login, pronto pra entrar de verdade — sem depender
mais de aprovação manual no console AWS.

## Contexto

O backend já expõe `POST /auth/confirm` e `POST /auth/resend-confirmation`
(FEAT-35, concluída) — ver spec para o contrato completo e as decisões
de anti-enumeração. O design (`frontend/design-system/README.md`,
"Autenticação (atualizado)", e os protótipos `web/jrnexpenses-web.dc.html`
/ `mobile/jrnexpenses.dc.html`) já mostra a tela: 6 campos de dígito com
avanço automático de foco, contador regressivo de 60s, campo de erro
inline, e um estado de "contador zerado" que desabilita os campos e
troca o botão por "Reenviar e-mail" (`21-otp-cadastro.png`,
`22-otp-expirado.png`). Ao confirmar com sucesso, volta ao login com um
aviso "E-mail confirmado" (`23-login-email-confirmado.png`). Como o app
é uma SPA responsiva única (não há projeto mobile nativo separado — ver
`MobileBottomNav`), o protótipo mobile do design system é só a
referência de layout em telas estreitas; a implementação é um único
fluxo React que se adapta por breakpoint, igual ao resto do app.

Esta feature **substitui** o comportamento fechado na FEAT-21: hoje,
`POST /auth/register` bem-sucedido mostra "Conta criada! Aguarde a
aprovação do administrador..." e volta pro modo "Entrar" sem nenhuma
forma de o próprio usuário confirmar a conta. Isso deixa de existir —
a confirmação passa a ser via OTP, sem intervenção manual.

**Decisões fechadas com o usuário durante este `/specify`:**

1. **Login com conta não confirmada ganha um caminho de volta para a
   tela de código.** Hoje `POST /auth/login` com 401
   (`user-not-confirmed`) mostra "Sua conta ainda não foi aprovada.
   Aguarde a confirmação do administrador e tente novamente." — texto
   de uma época sem OTP. Passa a mostrar uma mensagem compatível com o
   fluxo real ("Confirme seu cadastro pelo código enviado por e-mail
   antes de entrar.") com um botão/link **"Confirmar cadastro"** que
   leva à mesma tela de OTP, com o email do formulário de login já
   preenchido, disparando `POST /auth/resend-confirmation` na entrada
   (o código original pode ter sido perdido/expirado havia muito tempo)
   e iniciando o cooldown de 60s já em andamento. Cobre o caso de
   alguém fechar a aba na tela de confirmação e só tentar logar depois.
2. **O contador de 60s é só cooldown de reenvio, nunca expiração real
   do código** — mesma decisão já tomada no backlog do backend
   (`backend/docs/backlog.md`, 2026-09-01) e aplicada à copy dos
   emails nas specs de FEAT-35/36. O protótipo original usa a copy
   "Ele vale por 1 minuto." / "até o código expirar" / "O código
   expirou.", que não reflete o TTL real do Cognito (bem maior que
   60s). A copy da implementação é ajustada para não prometer uma
   expiração que o backend não aplica — ver "Requisitos de negócio".
3. **Um único texto de erro inline para qualquer 400 de `POST
   /auth/confirm`** (código incorreto ou código expirado/email
   inexistente — dois `type` diferentes por design do backend, ver
   FEAT-35 "Decisão 1"), sem tentar diferenciá-los na UI. Diferenciar
   arriscaria criar um canal indireto de enumeração que o backend
   deliberadamente evitou ao devolver o mesmo `type` genérico
   (`expired-confirmation-code`) tanto pra código expirado quanto pra
   email inexistente.
4. **"← Voltar" na tela de código retorna ao modo "Entrar" do login**
   (não ao modo "Criar conta", como no protótipo original). Como a
   conta já foi criada no Cognito nesse ponto, reabrir o formulário de
   cadastro levaria a um 409 (`email-already-exists`) se o usuário
   tentasse recadastrar o mesmo email — sem utilidade real.

## Requisitos de negócio

- Cadastro bem-sucedido (`201` de `POST /auth/register`) navega direto
  para a tela de confirmação de código, sem nenhuma tela intermediária
  — a mensagem "Conta criada! Aguarde a aprovação do administrador..."
  deixa de existir
- Tela de confirmação mostra o email para o qual o código foi enviado
  (o mesmo do cadastro, ou o preenchido no login ao vir pelo CTA da
  decisão 1) e 6 campos de dígito, com:
  - avanço automático de foco ao digitar, volta ao campo anterior com
    Backspace em campo vazio
  - `inputmode` numérico, um dígito por campo
  - submit bloqueado no client se os 6 dígitos não estiverem
    preenchidos, com mensagem "Digite os 6 dígitos do código.", sem
    chamar a API
- Contador regressivo de 60s (formato `M:SS`), rotulado como cooldown
  de reenvio (ex.: "aguarde Xs para poder reenviar"), nunca como prazo
  de validade do código em si (decisão 2)
- Ao chegar a zero: os 6 campos ficam desabilitados e o botão de
  confirmar é substituído por "Reenviar e-mail", que chama `POST
  /auth/resend-confirmation`, limpa os campos, reabilita-os e reinicia
  o contador em 60s
- Código correto (`200` de `POST /auth/confirm`) leva de volta ao login
  em modo "Entrar", com o email já preenchido e um aviso visível "Email
  confirmado. Sua conta está ativa — entre com seus dados." (mesma
  copy do design, `23-login-email-confirmado.png`)
- Qualquer `400` de `POST /auth/confirm` (código incorreto, código
  expirado ou email inexistente — ver decisão 3) mostra um erro inline
  único, ex. "Código inválido ou expirado. Confira o email ou solicite
  um novo código.", **sem resetar o contador nem limpar os dígitos
  já preenchidos**
- Erro de rede em `POST /auth/confirm` ou `POST /auth/resend-confirmation`
  mostra a mesma mensagem de erro de rede já usada no restante do auth
- "← Voltar" na tela de confirmação retorna ao login em modo "Entrar"
  (decisão 4), sem chamar nenhuma API
- Login com `401` (`user-not-confirmed`) mostra "Confirme seu cadastro
  pelo código enviado por e-mail antes de entrar." com um botão
  "Confirmar cadastro" que abre a tela de confirmação com o email do
  login pré-preenchido, disparando `POST /auth/resend-confirmation`
  automaticamente ao abrir (decisão 1)
- Login com `401` (`invalid-credentials`) continua com a mensagem já
  existente ("Email ou senha inválidos."), sem nenhuma mudança

## User Stories

**US1 — Cadastro leva direto à confirmação**
- Given o formulário de cadastro preenchido corretamente
- When o usuário submete e a API retorna 201
- Then a tela de confirmação de código é exibida imediatamente, com o
  email recém-cadastrado visível e o contador de 60s já em andamento

**US2 — Confirmação com código correto**
- Given a tela de confirmação, com o código de 6 dígitos correto em
  mãos
- When o usuário digita o código e confirma
- Then a API retorna 200, e a tela volta ao login em modo "Entrar" com
  o email preenchido e o aviso "Email confirmado..."; o usuário
  consegue logar normalmente em seguida

**US3 — Código incorreto**
- Given a tela de confirmação
- When o usuário digita um código que não confere e confirma
- Then a API retorna 400, um erro inline é exibido, os dígitos digitados
  permanecem e o contador continua de onde estava (não reseta)

**US4 — Contador chega a zero**
- Given a tela de confirmação, sem confirmar o código a tempo
- When o contador chega a 0:00
- Then os 6 campos ficam desabilitados e o botão "Reenviar e-mail" é
  exibido no lugar do botão de confirmar

**US5 — Reenvio de código**
- Given a tela de confirmação com o contador zerado (ou o usuário
  chegou até ela pelo CTA do login, decisão 1)
- When o reenvio é disparado (clique em "Reenviar e-mail" ou entrada
  automática pelo CTA do login)
- Then `POST /auth/resend-confirmation` é chamado, os campos são
  limpos e reabilitados, e o contador reinicia em 60s

**US6 — Submit bloqueado com código incompleto**
- Given a tela de confirmação com menos de 6 dígitos preenchidos
- When o usuário tenta confirmar
- Then o submit é bloqueado no client (sem chamar a API), com a
  mensagem "Digite os 6 dígitos do código."

**US7 — Login com conta não confirmada**
- Given uma conta cadastrada mas ainda não confirmada
- When o usuário tenta logar com email e senha corretos
- Then a API retorna 401 (`user-not-confirmed`) e o login exibe
  "Confirme seu cadastro pelo código enviado por e-mail antes de
  entrar." com o botão "Confirmar cadastro"

**US8 — CTA do login leva à confirmação com reenvio automático**
- Given a mensagem de conta não confirmada no login (US7)
- When o usuário clica em "Confirmar cadastro"
- Then a tela de confirmação abre com o email do login pré-preenchido,
  `POST /auth/resend-confirmation` é chamado automaticamente e o
  contador de 60s começa a contar

**US9 — Voltar da tela de confirmação**
- Given a tela de confirmação, por qualquer caminho de entrada (US1 ou
  US8)
- When o usuário clica em "← Voltar"
- Then a tela volta ao login em modo "Entrar", sem chamar nenhuma API

**US10 — Login com senha errada continua distinto**
- Given uma conta já confirmada
- When o usuário tenta logar com a senha errada
- Then a API retorna 401 (`invalid-credentials`) e o login continua
  exibindo "Email ou senha inválidos." — sem nenhuma mudança

**US11 — Erro de rede**
- Given a tela de confirmação
- When `POST /auth/confirm` ou `POST /auth/resend-confirmation` falha
  por erro de rede
- Then a mesma mensagem de erro de rede já usada no restante do auth é
  exibida, sem perder o estado da tela (dígitos, contador)

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo e decisões de anti-enumeração em
`backend/specs/FEAT-35-confirmacao-cadastro-otp/spec.md`. Resumo do que
o frontend envia/recebe:

### POST /auth/confirm

Request:
```json
{
  "email": "neto@email.com",
  "code": "123456"
}
```

Response 200: sem corpo.

Response 400 (código incorreto — usuário existente, confirmado ou
não):
```json
{
  "type": "https://gastosapp.dev/errors/invalid-confirmation-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de confirmação inválido."
}
```

Response 400 (código expirado, ou email inexistente — mesmo `type` nos
dois casos, decisão de anti-enumeração do backend):
```json
{
  "type": "https://gastosapp.dev/errors/expired-confirmation-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de confirmação expirado."
}
```

### POST /auth/resend-confirmation

Request:
```json
{
  "email": "neto@email.com"
}
```

Response 200: sem corpo (sempre — inclusive email inexistente ou já
confirmado, sem revelar diferença).

### POST /auth/login (mudança de tratamento, não de contrato)

O contrato já existe (FEAT-01/FEAT-21); o que muda é só a mensagem e a
ação disponível para o frontend ao ler `401` com
`type: "user-not-confirmed"`:
```json
{
  "type": "https://gastosapp.dev/errors/user-not-confirmed",
  "title": "Não autorizado",
  "status": 401,
  "detail": "Usuário não confirmado. Por favor, confirme seu email antes de fazer login."
}
```

## Critérios de aceite

- [x] Cadastro com sucesso (201) navega direto para a tela de
      confirmação, sem passar pela antiga tela "aguarde aprovação"
- [x] Tela de confirmação tem 6 campos de dígito com avanço automático
      de foco e suporte a Backspace entre campos
- [x] Submit com menos de 6 dígitos é bloqueado no client, sem chamar a
      API
- [x] Código correto chama `POST /auth/confirm`, recebe 200 e volta ao
      login em modo "Entrar" com o email preenchido e aviso "Email
      confirmado..."
- [x] Código incorreto ou expirado (qualquer 400 de `POST
      /auth/confirm`) mostra erro inline único, sem resetar o contador
      nem limpar os dígitos
- [x] Contador regressivo de 60s, rotulado como cooldown de reenvio
      (não como expiração do código)
- [x] Contador chegando a zero desabilita os 6 campos e troca o botão
      de confirmar por "Reenviar e-mail"
- [x] "Reenviar e-mail" chama `POST /auth/resend-confirmation`, limpa e
      reabilita os campos, e reinicia o contador em 60s
- [x] "← Voltar" na tela de confirmação volta ao login em modo "Entrar"
      sem chamar nenhuma API
- [x] Login com 401 `user-not-confirmed` exibe a nova mensagem
      ("Confirme seu cadastro...") com o botão "Confirmar cadastro"
- [x] Botão "Confirmar cadastro" do login abre a tela de confirmação
      com o email pré-preenchido e dispara `POST
      /auth/resend-confirmation` automaticamente, com o contador já em
      andamento
- [x] Login com 401 `invalid-credentials` continua com a mensagem já
      existente, sem regressão
- [x] Erro de rede em `POST /auth/confirm` ou `POST
      /auth/resend-confirmation` mostra a mesma mensagem de rede já
      usada no restante do auth
- [x] Layout segue o Modernist, consistente com
      `21-otp-cadastro.png`/`22-otp-expirado.png`/
      `23-login-email-confirmado.png`, responsivo (web e mobile, mesmo
      componente) — validado ao vivo no Chrome contra os 3 screenshots
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima:
      sucesso, código incorreto, contador zerado + reenvio, submit
      bloqueado por código incompleto, CTA do login, erro de rede

## Fora do escopo

- Recuperação de senha ("Esqueci minha senha") e sua própria tela de
  OTP (`25-otp-recuperacao.png`) — depende do backend FEAT-36, ainda
  sem spec própria no frontend
- Qualquer mudança de contrato ou comportamento em `POST
  /auth/confirm`, `POST /auth/resend-confirmation`, `POST
  /auth/register` ou `POST /auth/login` — os quatro endpoints já
  implementam tudo que esta feature precisa
- Persistir o estado da tela de confirmação entre reloads do navegador
  (F5 na tela de confirmação volta para o login; o usuário chega de
  novo à confirmação relogando ou recadastrando)
- Rate limiting/bloqueio de tentativas no frontend além do já existente
  no backend (Cognito) — nenhuma trava adicional de tentativas por IP
  ou por sessão
- Alterar o template HTML do email de confirmação — já resolvido no
  backend (FEAT-34)
