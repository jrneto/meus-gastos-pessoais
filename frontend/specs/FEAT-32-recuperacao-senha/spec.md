# FEAT-32: Recuperação de senha (fluxo completo)

## Objetivo

Permitir que um usuário que esqueceu a senha a redefina sozinho, direto
pela tela de login, sem depender de suporte manual: um fluxo de 3 passos
— informar o e-mail, confirmar o código de 6 dígitos recebido, e definir
uma senha nova — reaproveitando o padrão de tela de código já
estabelecido pela FEAT-31 (confirmação de cadastro).

## Contexto

Item do backlog (`frontend/docs/backlog.md`, seção "Autenticação: área
não logada", combinada em 2026-09-01), parte da mesma leva de telas do
protótipo atualizado do design system (`24-recuperar-senha.png`,
`25-otp-recuperacao.png`, `26-nova-senha.png`,
`27-login-senha-redefinida.png`, web e mobile). Depende do backend
FEAT-36 (`POST /auth/forgot-password`, `POST /auth/reset-password`, já
implementado e concluído) e do padrão de tela de OTP estabelecido pela
FEAT-31 (contador de cooldown, 6 campos de dígito com avanço automático
de foco, texto de erro inline único).

O link "Esqueci minha senha" aparece no protótipo (`27-login-senha-
redefinida.png`) mas **ainda não existe no código** — `LoginForm.tsx`
hoje não tem esse link; ele precisa ser criado como parte desta feature
(a redação original do backlog, "a partir do link já existente na tela
de login", se referia só ao protótipo).

**Decisões fechadas com o usuário durante este `/specify`:**

1. **Esta feature também corrige a validação de senha do cadastro.** O
   backlog assumia que a política completa do Cognito (mínimo 8
   caracteres, com maiúscula, minúscula, número e símbolo) "já é
   aplicada no cadastro" — conferido durante o `/specify`:
   `registerSchema.ts` hoje só valida mínimo de 8 caracteres, a política
   completa nunca foi implementada no frontend. Decisão: implementar a
   validação completa uma vez (compartilhada) e aplicá-la tanto ao campo
   "Nova senha" desta feature quanto ao campo "Senha" do cadastro
   (`registerSchema`), fechando a divergência em vez de deixá-la como
   débito técnico à parte.
2. **"← Voltar" no Passo 2/3 (Verifique o código) volta ao Passo 1/3**
   (permite trocar o e-mail informado), diferente do padrão da FEAT-31
   (lá, "← Voltar" na tela de confirmação volta direto ao login). A
   FEAT-31 tomou aquela decisão porque voltar ao formulário de cadastro
   geraria um 409 (`email-already-exists`) ao tentar recadastrar o mesmo
   email — esse risco não existe aqui, então o comportamento padrão do
   protótipo (voltar um passo) é mantido.
3. **Erro de código inválido/expirado só aparece no Passo 3/3.** O
   backend não tem endpoint para validar o código isoladamente —
   `POST /auth/reset-password` recebe `email` + `code` + `newPassword`
   numa única chamada (ver `backend/specs/FEAT-36-recuperacao-senha/
   spec.md`, ponto de confirmação sobre ordem de validação: o código é
   validado antes da senha). Por isso, o Passo 2/3 ("Confirmar código")
   é **só uma validação client-side** (6 dígitos preenchidos) que avança
   pro Passo 3/3 — a API só é chamada quando a senha nova é submetida.
   Se a resposta indicar código inválido/expirado, a tela mostra o erro
   inline no próprio Passo 3/3, com um link "Voltar e conferir o
   código" que leva ao Passo 2/3 (a senha digitada é perdida ao voltar).
4. **Contador de 60s é só cooldown de reenvio, nunca expiração real do
   código** — mesma decisão já tomada na FEAT-31 (decisão 2) e no
   backlog do backend (2026-09-01). O protótipo usa a copy "Ele vale por
   1 minuto." / "até o código expirar", que não reflete o TTL real do
   Cognito (bem maior que 60s); a copy da implementação é ajustada do
   mesmo jeito que a FEAT-31 já ajustou a sua.
5. **Botão de mostrar/ocultar senha não faz parte desta feature.** O
   protótipo do Passo 3/3 (`26-nova-senha.png`) mostra um toggle
   "OCULTAR" no campo de senha — pertence à FEAT-33 (backlog,
   "Senha visível"), ainda não implementada. Os campos de senha desta
   feature nascem `type="password"`, sem toggle; a FEAT-33 adiciona o
   toggle depois, em qualquer campo de senha do app (login, cadastro,
   nova senha) de uma vez, sem exigir mudança nesta feature.

## Requisitos de negócio

- Tela de login (modo "Entrar") ganha um link "Esqueci minha senha" que
  abre o fluxo de recuperação, começando pelo Passo 1/3. Fluxo é
  inteiramente client-state (mesmo padrão da tela de confirmação da
  FEAT-31) — sem rota própria; um F5 durante o fluxo volta ao login.
- **Passo 1/3 — E-mail** (`24-recuperar-senha.png`): campo de e-mail
  obrigatório; "Enviar código" chama `POST /auth/forgot-password` com o
  e-mail informado. Sucesso (`200`, sempre — mesmo para e-mail
  inexistente ou não confirmado, anti-enumeração do backend) avança
  para o Passo 2/3 sem revelar se o e-mail existe de fato. "← Voltar ao
  login" retorna ao login em modo "Entrar", sem chamar API.
- **Passo 2/3 — Verifique o código** (`25-otp-recuperacao.png`): mostra
  o e-mail informado no Passo 1/3; 6 campos de dígito com avanço
  automático de foco ao digitar e volta ao campo anterior com Backspace
  em campo vazio (mesmo comportamento da FEAT-31), `inputmode`
  numérico. Contador regressivo de 60s (formato `M:SS`), rotulado como
  cooldown de reenvio, nunca como prazo de validade do código (decisão
  4). "Confirmar código" com os 6 dígitos preenchidos **apenas avança**
  para o Passo 3/3, sem chamar API (decisão 3); com menos de 6 dígitos,
  o submit é bloqueado no client com a mensagem "Digite os 6 dígitos do
  código.". Contador chegando a zero desabilita os 6 campos e troca o
  botão por "Reenviar e-mail", que chama `POST /auth/forgot-password`
  novamente (mesmo endpoint do Passo 1/3 — reset não tem endpoint de
  reenvio dedicado), limpa os campos, reabilita-os e reinicia o contador
  em 60s. "← Voltar" retorna ao Passo 1/3, permitindo trocar o e-mail,
  sem chamar API (decisão 2).
- **Passo 3/3 — Nova senha** (`26-nova-senha.png`): campos "Nova senha"
  e "Confirmar nova senha", ambos obrigatórios, validados pela política
  completa do Cognito (mínimo 8 caracteres, com maiúscula, minúscula,
  número e símbolo — decisão 1, mesma regra agora também aplicada ao
  campo de senha do cadastro). "Confirmar nova senha" precisa ser
  idêntico a "Nova senha" — divergência mostra erro inline "As senhas
  não coincidem.", verificado no client antes de chamar a API. "Salvar
  nova senha" chama `POST /auth/reset-password` com o `email` (Passo
  1/3), o `code` (dígitos do Passo 2/3) e o `newPassword`. Sucesso
  (`200`) volta ao login em modo "Entrar" com o e-mail preenchido e um
  aviso visível "Senha redefinida. Entre com a nova senha." (mesmo
  padrão do aviso "Email confirmado..." da FEAT-31,
  `27-login-senha-redefinida.png`).
- Erro `400` de `POST /auth/reset-password` do tipo código
  (`invalid-reset-code` ou `expired-reset-code` — mesmo texto genérico
  único para os dois, sem diferenciar, mesma decisão 3 da FEAT-31) exibe
  erro inline no Passo 3/3 ("Código inválido ou expirado.") com um link
  "Voltar e conferir o código" que leva ao Passo 2/3 (decisão 3); a
  senha digitada é perdida ao voltar.
- Erro `400` de `POST /auth/reset-password` do tipo `bad-request` (senha
  fora da política) exibe o erro inline no próprio Passo 3/3, sem sair
  dele e sem perder o código já confirmado no Passo 2/3.
- Erro de rede em `POST /auth/forgot-password` ou
  `POST /auth/reset-password` mostra a mesma mensagem de erro de rede já
  usada no restante do auth, sem perder o estado da tela atual (passo,
  dígitos, contador).
- Cadastro (modo "Criar conta" do login, `registerSchema`) passa a usar
  a mesma validação completa de senha desta feature (decisão 1) — deixa
  de aceitar qualquer senha com 8+ caracteres sem os outros requisitos.

## User Stories

**US1 — Link de recuperação leva ao Passo 1/3**
- Given a tela de login, modo "Entrar"
- When o usuário clica em "Esqueci minha senha"
- Then o Passo 1/3 (E-mail) é exibido

**US2 — Pedido de código sempre avança, mesmo para e-mail que não existe**
- Given o Passo 1/3, com qualquer e-mail preenchido (exista ou não no
  Cognito)
- When o usuário clica em "Enviar código"
- Then `POST /auth/forgot-password` é chamado, a API retorna 200, e o
  Passo 2/3 é exibido, sem nenhuma indicação de o e-mail existir ou não

**US3 — Passo 2/3 avança sem chamar API**
- Given o Passo 2/3, com os 6 dígitos do código preenchidos
- When o usuário clica em "Confirmar código"
- Then a tela avança para o Passo 3/3 localmente, sem nenhuma chamada de
  API

**US4 — Submit bloqueado com código incompleto**
- Given o Passo 2/3, com menos de 6 dígitos preenchidos
- When o usuário tenta confirmar
- Then o submit é bloqueado no client, com a mensagem "Digite os 6
  dígitos do código.", sem avançar de passo

**US5 — Contador chega a zero e reenvio**
- Given o Passo 2/3, sem confirmar o código a tempo
- When o contador chega a 0:00
- Then os 6 campos ficam desabilitados e o botão "Reenviar e-mail" é
  exibido; ao clicar nele, `POST /auth/forgot-password` é chamado de
  novo, os campos são limpos e reabilitados, e o contador reinicia em
  60s

**US6 — Voltar do Passo 2/3 ao Passo 1/3**
- Given o Passo 2/3
- When o usuário clica em "← Voltar"
- Then a tela volta ao Passo 1/3, com o e-mail digitado ainda visível,
  sem chamar nenhuma API

**US7 — Redefinição com código e senha corretos**
- Given o Passo 3/3, com o código confirmado no Passo 2/3 e uma senha
  nova dentro da política
- When o usuário preenche "Nova senha" e "Confirmar nova senha" iguais e
  clica em "Salvar nova senha"
- Then `POST /auth/reset-password` é chamado com email/code/newPassword,
  a API retorna 200, e a tela volta ao login em modo "Entrar" com o
  e-mail preenchido e o aviso "Senha redefinida. Entre com a nova
  senha."

**US8 — Senhas não coincidem**
- Given o Passo 3/3
- When "Nova senha" e "Confirmar nova senha" têm valores diferentes e o
  usuário tenta salvar
- Then o submit é bloqueado no client com "As senhas não coincidem.",
  sem chamar a API

**US9 — Código inválido ou expirado, descoberto no Passo 3/3**
- Given o Passo 3/3, com um código que na verdade está incorreto ou
  expirado
- When o usuário preenche uma senha válida e confirma
- Then a API retorna 400 (`invalid-reset-code` ou `expired-reset-code`),
  um erro inline "Código inválido ou expirado." é exibido no Passo 3/3
  com um link "Voltar e conferir o código", que leva ao Passo 2/3 (a
  senha digitada é perdida)

**US10 — Senha fora da política do Cognito**
- Given o Passo 3/3, com o código correto
- When o usuário informa uma senha que não atende a política completa
  (ex.: sem símbolo) e confirma
- Then a API retorna 400 (`bad-request`), um erro inline é exibido no
  próprio Passo 3/3, sem avançar nem voltar de passo

**US11 — Erro de rede**
- Given qualquer passo do fluxo
- When `POST /auth/forgot-password` ou `POST /auth/reset-password` falha
  por erro de rede
- Then a mesma mensagem de erro de rede já usada no restante do auth é
  exibida, sem perder o estado da tela (passo atual, dígitos, contador)

**US12 — Cadastro passa a exigir a política completa de senha**
- Given a tela de login, modo "Criar conta"
- When o usuário informa uma senha com 8+ caracteres mas sem maiúscula,
  minúscula, número ou símbolo
- Then o submit é bloqueado no client com o erro correspondente, sem
  chamar `POST /auth/register`

## Contratos consumidos (já implementados no backend, sem mudança)

Contrato completo e decisões de anti-enumeração em
`backend/specs/FEAT-36-recuperacao-senha/spec.md`. Resumo do que o
frontend envia/recebe:

### POST /auth/forgot-password

Request:
```json
{
  "email": "neto@email.com"
}
```

Response 200: sem corpo (sempre — inclusive email inexistente ou não
confirmado, sem revelar diferença).

### POST /auth/reset-password

Request:
```json
{
  "email": "neto@email.com",
  "code": "123456",
  "newPassword": "NovaSenha@2026"
}
```

Response 200: sem corpo.

Response 400 (código incorreto):
```json
{
  "type": "https://gastosapp.dev/errors/invalid-reset-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de recuperação inválido."
}
```

Response 400 (código expirado, ou email inexistente — mesmo `type` nos
dois casos, anti-enumeração do backend):
```json
{
  "type": "https://gastosapp.dev/errors/expired-reset-code",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Código de recuperação expirado."
}
```

Response 400 (senha fora da política):
```json
{
  "type": "https://gastosapp.dev/errors/bad-request",
  "title": "Parâmetros inválidos",
  "status": 400,
  "detail": "Senha deve ter no mínimo 8 caracteres, com letra maiúscula, minúscula, número e símbolo."
}
```

## Critérios de aceite

- [x] Link "Esqueci minha senha" aparece no login (modo "Entrar") e
      abre o Passo 1/3
- [x] Passo 1/3: "Enviar código" chama `POST /auth/forgot-password` e
      avança para o Passo 2/3 com 200, mesmo para e-mail inexistente,
      sem revelar a diferença (US2)
- [x] "← Voltar ao login" no Passo 1/3 volta ao login sem chamar API
- [x] Passo 2/3: 6 campos de dígito com avanço automático de foco e
      Backspace entre campos, mesmo padrão da FEAT-31
- [x] Passo 2/3: "Confirmar código" com os 6 dígitos preenchidos avança
      pro Passo 3/3 sem chamar API (US3)
- [x] Passo 2/3: submit com menos de 6 dígitos é bloqueado no client
      (US4)
- [x] Passo 2/3: contador de 60s rotulado como cooldown de reenvio (não
      como expiração do código)
- [x] Passo 2/3: contador chegando a zero desabilita os campos e troca o
      botão por "Reenviar e-mail", que chama `POST /auth/forgot-
      password` de novo, limpa/reabilita os campos e reinicia o
      contador (US5)
- [x] "← Voltar" no Passo 2/3 volta ao Passo 1/3, preservando o e-mail
      digitado, sem chamar API (US6)
- [x] Passo 3/3: "Nova senha" e "Confirmar nova senha" validados pela
      política completa do Cognito (mín. 8, maiúscula, minúscula,
      número, símbolo)
- [x] Passo 3/3: senhas diferentes bloqueiam o submit no client (US8)
- [x] Passo 3/3: "Salvar nova senha" chama `POST /auth/reset-password`
      com email/code/newPassword; sucesso (200) volta ao login com
      e-mail preenchido e aviso "Senha redefinida..." (US7)
- [x] Passo 3/3: erro de código (`invalid-reset-code`/`expired-reset-
      code`) mostra erro inline único com link de volta ao Passo 2/3,
      perdendo a senha digitada (US9)
- [x] Passo 3/3: erro de senha fora da política (`bad-request`) mostra
      erro inline sem sair do passo (US10)
- [x] Erro de rede em qualquer chamada preserva o estado da tela atual
      (US11)
- [x] `registerSchema` (cadastro) passa a validar a mesma política
      completa de senha, compartilhada com o Passo 3/3 (US12)
- [x] Campos de senha desta feature nascem sem toggle de
      mostrar/ocultar (fora do escopo, decisão 5)
- [x] Layout segue o Modernist, consistente com
      `24-recuperar-senha.png`/`25-otp-recuperacao.png`/
      `26-nova-senha.png`/`27-login-senha-redefinida.png`, responsivo
      (web e mobile, mesmo componente) — validado ao vivo no Chrome
      contra os 4 screenshots e o backend local real
- [x] Cobertura de teste (Vitest + RTL + MSW) para os cenários acima:
      sucesso ponta a ponta, e-mail inexistente (sem diferença
      observável), código incompleto, contador zerado + reenvio, voltar
      entre passos, senhas não coincidem, código inválido/expirado,
      senha fora da política, erro de rede, política de senha no
      cadastro
- [x] 100% dos testes passando (unitário + componente) — 622/622

## Status

Implementação concluída (28/28 tasks de `tasks.md`). Suíte completa:
622 testes (100% passando), `tsc -b`/`oxlint`/`npm run build` limpos.
Validado ao vivo no Chrome contra o backend local real (LocalStack +
cognito-local): fluxo completo email → código → reenvio pós-cooldown →
nova senha → login com a senha redefinida, ponta a ponta.

**Achado durante a revisão visual:** faltava o kicker "PASSO N DE 3 ·
RECUPERAÇÃO" acima do título de cada passo (presente nos 4 screenshots
de referência, web e mobile) — corrigido reaproveitando a classe
`.card-kicker` já existente no CSS Modernist.

**Débito técnico descoberto e corrigido durante a implementação (fora
do escopo original desta feature, autorizado pelo usuário):** ao
escrever os testes desta feature, identificada uma fragilidade
pré-existente na suíte completa do frontend — testes que afirmam sobre
um estado de loading transitório contra um mock do MSW sem delay (ou
com delay fixo insuficiente) podem flacar sob carga (React 18 pode
agrupar o `setState` de início/fim do loading no mesmo lote quando a
resposta resolve rápido/cedo demais). Investigado a pedido do usuário
rodando a suíte completa repetidamente; corrigidos 3 arquivos de outras
features (`InviteMemberDialog.test.tsx`, `SettingsPage.test.tsx`,
`CategoriesPage.test.tsx`) com a técnica de Promise controlada
manualmente pelo teste. Validado com múltiplas rodadas completas
consecutivas 100% verdes depois do fix. Detalhes completos e o que
ainda pode não ter sido descoberto em `frontend/docs/backlog.md`
("Débitos técnicos e melhorias futuras").

## Fora do escopo

- Qualquer mudança de contrato ou comportamento em
  `POST /auth/forgot-password` ou `POST /auth/reset-password` — já
  implementados e concluídos na FEAT-36 do backend
- Endpoint de verificação isolada de código — não existe hoje e não será
  criado; o Passo 2/3 avança só localmente (decisão 3)
- Botão de mostrar/ocultar senha em qualquer campo — fica pra FEAT-33
  (decisão 5)
- Qualquer mudança de contrato ou comportamento em `POST /auth/login`,
  `POST /auth/register` (endpoint em si), `POST /auth/confirm` ou
  `POST /auth/resend-confirmation`
- Rate limiting/bloqueio de tentativas no frontend além do já existente
  no backend (Cognito) — nenhuma trava adicional por IP ou sessão
- Persistir o estado do fluxo entre reloads do navegador (F5 volta ao
  login)
- Alterar os templates HTML dos e-mails de recuperação/senha alterada —
  já resolvido no backend (FEAT-33/34/36 do backend)
