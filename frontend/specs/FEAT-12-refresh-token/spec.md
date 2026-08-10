# FEAT-12: Consumo do refresh token no frontend

## Objetivo
Fazer o frontend consumir o refresh token implementado no backend
(`backend/specs/FEAT-15-refresh-token/spec.md`), para que o usuário
permaneça logado ao atualizar a página (F5) ou reabrir a aba, e para que
uma expiração do `accessToken` em memória durante o uso do app seja
renovada de forma transparente, sem forçar novo login.

Hoje (`FEAT-01-setup-login`) o `accessToken` vive só em memória
(Zustand, sem `persist`) e a sessão é considerada válida enquanto
`Date.now() < expiresAt`. Sem refresh, qualquer F5 derruba a sessão e
qualquer expiração do `accessToken` (1h) redireciona para `/login`,
mesmo que o usuário ainda tenha uma sessão válida no backend (cookie de
refresh token, validade 5 dias). Esta feature elimina essas duas
quebras de sessão desnecessárias.

O backend já expõe os contratos necessários (`POST /auth/refresh`,
`POST /auth/logout`), implementados e testados na FEAT-15 do backend.
Esta feature é exclusivamente a integração do lado do frontend.

## Requisitos de negócio
- No boot da aplicação (carregamento inicial / F5), antes de decidir se
  o usuário está autenticado, o app tenta silenciosamente `POST
  /auth/refresh`. Se tiver sucesso, a sessão é restaurada (novo
  `accessToken` em memória) sem exibir a tela de login. Se falhar (401),
  o app trata como "sem sessão" e segue o comportamento atual (acesso a
  rota protegida redireciona para `/login`).
- Enquanto o app está em uso, se uma chamada autenticada a API retornar
  401 por `accessToken` expirado, o app tenta renovar a sessão via
  `POST /auth/refresh` uma única vez e, se bem-sucedido, repete
  automaticamente a chamada original com o novo `accessToken` — o
  usuário não percebe a renovação nem perde a ação que estava
  executando.
- Se a chamada a `POST /auth/refresh` (seja no boot, seja durante o
  uso) retornar 401 (refresh token ausente, expirado ou inválido), o
  app limpa a sessão local e redireciona para `/login`, do mesmo jeito
  que já acontece hoje para expiração local do `accessToken`.
- Múltiplas chamadas 401 simultâneas (ex.: várias requisições em
  paralelo cujo `accessToken` expirou ao mesmo tempo) disparam **no
  máximo uma** chamada a `/auth/refresh` — as demais aguardam o
  resultado dessa única chamada em vez de disparar refreshes
  concorrentes.
- Falha de rede (timeout, 5xx, sem conexão) ao chamar `/auth/refresh`
  não é tratada como sessão inválida — não limpa a sessão nem
  redireciona para `/login`; é tratada como falha da operação que
  disparou a tentativa (mesmo tratamento que qualquer outro erro de
  rede já tem hoje).
- Toda chamada HTTP do frontend para a API passa a enviar credenciais
  (cookie) — necessário para que o cookie httpOnly `refreshToken`
  (setado pelo backend com `Path=/auth`) seja enviado automaticamente
  pelo navegador nas chamadas a `/auth/refresh` e `/auth/logout`.
- A ação de logout, além de limpar o estado local (como hoje), passa a
  chamar `POST /auth/logout` para encerrar a sessão no backend
  (limpando o cookie de refresh token no servidor) antes de redirecionar
  para `/login`. Falha nessa chamada (ex.: rede indisponível) não deve
  impedir o logout local — o usuário sai da sessão localmente de
  qualquer forma.

## User stories

### US1 — Sessão restaurada ao recarregar a página
**Given** um usuário que fez login e possui um cookie de refresh token
válido no navegador
**When** ele atualiza a página (F5) ou reabre a aba, mesmo com o
`accessToken` em memória perdido
**Then** o app chama `POST /auth/refresh` silenciosamente no boot,
obtém um novo `accessToken` e mantém o usuário na rota protegida, sem
exibir a tela de login

### US2 — Recarregar a página sem sessão válida
**Given** um usuário que nunca logou, ou cujo refresh token já expirou
(mais de 5 dias) ou foi limpo (logout)
**When** ele atualiza a página ou acessa a rota protegida diretamente
**Then** o app tenta `POST /auth/refresh`, recebe 401, e redireciona
para `/login` (mesmo comportamento hoje já visível para "sem token")

### US3 — Renovação transparente durante o uso
**Given** um usuário navegando no app com o `accessToken` já expirado
em memória, mas com refresh token ainda válido
**When** o app faz uma chamada autenticada à API e recebe 401
**Then** o app renova a sessão via `POST /auth/refresh` e repete
automaticamente a chamada original, sem exibir erro nem redirecionar
para `/login`

### US4 — Refresh falha durante o uso
**Given** um usuário navegando no app cujo refresh token expirou ou foi
invalidado enquanto ele usava a aplicação
**When** uma chamada autenticada retorna 401 e a tentativa de renovação
(`POST /auth/refresh`) também retorna 401
**Then** o app limpa a sessão local e redireciona para `/login`

### US5 — Múltiplas chamadas expiram ao mesmo tempo
**Given** um usuário com o `accessToken` expirado em memória
**When** o app dispara várias chamadas autenticadas em paralelo e todas
retornam 401
**Then** apenas uma chamada a `POST /auth/refresh` é feita; as demais
chamadas originais aguardam esse resultado e são repetidas com o mesmo
novo `accessToken`

### US6 — Logout encerra a sessão renovável
**Given** um usuário autenticado na rota protegida
**When** ele aciona a ação de logout
**Then** o app chama `POST /auth/logout`, limpa o `accessToken` local e
redireciona para `/login`; uma tentativa posterior de `POST
/auth/refresh` (ex.: F5) passa a retornar 401

### US7 — Falha de rede ao tentar renovar
**Given** um usuário com o `accessToken` expirado em memória e sem
conexão de rede (ou API indisponível)
**When** o app tenta renovar a sessão via `POST /auth/refresh`
**Then** o app trata como erro de rede da operação em andamento (não
limpa a sessão nem redireciona para `/login`)

## Contratos da API observáveis
Este FEAT consome contratos já definidos e implementados no backend
(`backend/specs/FEAT-15-refresh-token/spec.md`), reproduzidos aqui
apenas como referência de integração — o backend é a fonte da verdade
(`backend/docs/openapi.json`):

### POST /auth/refresh
Sem request body. Requer o cookie `refreshToken` (enviado
automaticamente pelo navegador — exige que as chamadas do frontend
sejam feitas com credenciais incluídas).

Response 200:
```json
{
  "accessToken": "eyJ...",
  "expiresIn": 3600,
  "userId": "uuid-do-cognito"
}
```

Response 401 (cookie ausente, expirado ou inválido):
```json
{
  "type": "https://gastosapp.dev/errors/refresh-token-missing",
  "title": "Refresh token ausente.",
  "status": 401
}
```
(ou `invalid-refresh-token`, mesmo status 401 — o frontend trata os
dois casos da mesma forma: sessão inválida)

### POST /auth/logout
Sem request body. Cookie `refreshToken` opcional (idempotente).

Response 200: sem corpo.

### POST /auth/login (contrato já consumido desde FEAT-01, sem mudança
de uso no frontend — o cookie de refresh token passa a ser setado pelo
backend automaticamente na resposta, sem exigir nenhuma leitura ou
tratamento explícito por parte do frontend)

## Critérios de aceite
- [x] Boot da aplicação tenta `POST /auth/refresh` silenciosamente
      antes de decidir se a sessão está ativa
- [x] Sessão restaurada com sucesso no boot mantém o usuário na rota
      protegida sem exibir a tela de login
- [x] Boot sem sessão válida (refresh retorna 401) redireciona para
      `/login`, sem alterar o comportamento já existente
- [x] Chamada autenticada que recebe 401 por expiração dispara renovação
      automática e repete a chamada original de forma transparente
- [x] Renovação que falha (401) durante o uso limpa a sessão e
      redireciona para `/login`
- [x] Múltiplas chamadas 401 concorrentes resultam em uma única chamada
      a `/auth/refresh`
- [x] Erro de rede ao tentar renovar não limpa a sessão nem redireciona
      para `/login`
- [x] Logout chama `POST /auth/logout` além de limpar o estado local
- [x] Todas as chamadas HTTP do frontend para a API enviam credenciais
      (cookie)
- [x] Testes unitários/componente cobrendo: restauração de sessão no
      boot, renovação transparente em 401, falha de renovação, chamadas
      concorrentes e logout
- [x] 100% dos testes passando (novos e já existentes)

## Fora do escopo deste FEAT
- Qualquer mudança no backend (contrato já implementado e estável na
  FEAT-15 do backend)
- Rotação de refresh token
- Aviso visual de "sessão prestes a expirar" antes da renovação
  automática
- Sincronização de sessão entre múltiplas abas abertas
- "Lembrar de mim" / sessões de duração configurável pelo usuário
- MFA, recuperação de senha (já fora do escopo desde `FEAT-01`)
