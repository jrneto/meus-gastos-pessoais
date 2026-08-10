# Plano técnico — FEAT-12: Consumo do refresh token no frontend

Referência: [`spec.md`](./spec.md). Backend (`FEAT-15-refresh-token`,
já mergeado em `develop`) expõe `POST /auth/refresh` e
`POST /auth/logout`, com o refresh token transportado em cookie
httpOnly (`Path=/auth`). Este plano cobre exclusivamente o consumo
disso pelo frontend.

## Investigação do código atual (relevante para o plano)
- `lib/httpClient.ts`: wrapper fino sobre `fetch`, sem `credentials`,
  sem noção de auth. `Content-Type` é o único header default.
- `features/auth/api/authApi.ts`: `login()` e `me()`; padrão
  `safeFetch` (converte exceção de rede em `NetworkError`) +
  `assertOk` (mapeia 401 → erro tipado).
- `features/auth/store/authStore.ts`: Zustand, `token`/`userId`/
  `expiresAt`, `setSession`/`clearSession`. Sem `persist`.
- `features/expenses/api/*.ts` e seus hooks (`useExpensesQuery`,
  `useExpense`, `useRegisterExpense`, `useUpdateExpense`,
  `useDeleteExpense`): cada hook lê `token` da `authStore` e passa
  manualmente para a função de API, que seta
  `headers: { Authorization: Bearer <token> }`. Em 401, cada hook hoje
  chama `useAuthStore.getState().clearSession()` diretamente.
- `app/App.tsx` / `app/router.tsx`: `App` só renderiza o
  `RouterProvider`; `ProtectedRoute` decide `isAuthenticated` via
  `useAuthSession` (deriva de `token`/`expiresAt` da store).
- `routes/SettingsPage.tsx`: botão "Sair" chama `clearSession()` direto
  da store e navega para `/login`.
- **Regra de dependência do frontend** (`frontend/CLAUDE.md`):
  `features/*` depende de `lib/`, nunca o contrário; uma feature nunca
  importa de dentro de outra. Isso impede `lib/httpClient.ts` de
  importar `authStore` (que é de `features/auth`) diretamente.
- **Bloqueio externo identificado**: `backend/src/GastosApp.Api/Program.cs`
  configura CORS sem `.AllowCredentials()`. Sem isso, o navegador rejeita
  respostas de chamadas com `credentials: 'include'` (necessário para o
  cookie httpOnly ser enviado a `/auth/refresh`/`/auth/logout`). O
  usuário confirmou que vai tratar isso separadamente, como mudança
  Modo Leve no contexto backend — só documentado aqui como dependência.

## Decisão de arquitetura central
Para respeitar a regra de dependência (`lib/` não pode importar
`features/*`) e ainda centralizar a lógica de renovação em
`httpClient`, `lib/httpClient.ts` ganha um mecanismo de plugin
(inversão de controle), registrado uma única vez no bootstrap (`app/`,
camada que já importa de várias features, ex. `router.tsx`):

```ts
// lib/httpClient.ts
interface AuthPlugin {
  getAccessToken: () => string | null
  refreshAccessToken: () => Promise<string | null> // null = sessão inválida (401 no refresh)
  onSessionExpired: () => void
}
export function registerAuthPlugin(plugin: AuthPlugin): void
```

`app/authBootstrap.ts` (novo arquivo) faz a única chamada a
`registerAuthPlugin`, conectando `authStore` e `authApi.refresh()` —
sem que `lib/` conheça `features/auth`.

Com isso, **nenhuma mudança é necessária em `features/expenses`**: os
hooks continuam lendo `token` da store e passando para as funções de
API como hoje (redundante, mas inofensivo) — `httpClient` passa a
sobrescrever o header `Authorization` com `getAccessToken()` sempre que
o plugin estiver registrado, então o valor manual vira só um detalhe
legado sem efeito. Isso é o que permite o retry funcionar de forma
transparente sem tocar no código de expenses.

## Camadas afetadas

### `lib/httpClient.ts`
- Toda requisição passa a incluir `credentials: 'include'`.
- Novo `registerAuthPlugin(plugin)` guarda o plugin em variável de
  módulo.
- Para paths fora de `/auth/login` e `/auth/refresh` (exclusão só
  desses dois — `/auth/logout` e um futuro `/auth/me` continuam
  passando pelo interceptor normalmente):
  - Se o plugin estiver registrado, `Authorization: Bearer <token>` é
    sempre injetado a partir de `plugin.getAccessToken()` (sobrescreve
    qualquer header `Authorization` que o chamador tenha passado).
  - Se a resposta vier 401: dispara `ensureRefreshed()`, uma promise
    de módulo (`let refreshPromise: Promise<string | null> | null`)
    para deduplicar chamadas concorrentes — a primeira 401 dispara
    `plugin.refreshAccessToken()`, as demais aguardam a mesma promise.
    - Refresh retorna token novo → repete a requisição original **uma
      vez** com o novo `Authorization`, e retorna essa resposta ao
      chamador (o `assertOk` de cada feature já trata o resultado,
      incluindo o caso raro de um segundo 401).
    - Refresh retorna `null` (sessão inválida) → chama
      `plugin.onSessionExpired()` e retorna a resposta 401 original
      sem retry (mapeamento de erro de cada feature já existe e não
      muda).
    - `refreshAccessToken()` lança exceção (falha de rede) → a exceção
      é repropagada (sem chamar `onSessionExpired`), caindo no
      `safeFetch` já existente de cada feature → vira `NetworkError`
      como qualquer outra falha de rede hoje.
- Novo arquivo de teste `lib/httpClient.test.ts` cobrindo: injeção de
  `Authorization`, retry transparente em 401 com refresh bem-sucedido,
  sessão limpa em refresh com 401, erro de rede não limpa sessão,
  deduplicação de chamadas concorrentes.

### `features/auth/api/authApi.ts`
- `refresh(): Promise<LoginResponse>` — `POST /auth/refresh`, sem
  body; reaproveita o mesmo formato de resposta de `login`
  (`accessToken`/`expiresIn`/`userId`). 401 → `RefreshFailedError`
  (novo). Outras falhas seguem o padrão atual (`NetworkError`/
  `UnknownAuthError`).
- `logout(): Promise<void>` — `POST /auth/logout`, sem body. Sucesso
  (200) não retorna corpo. Falha (rede ou status) é decisão do
  chamador (`useLogout`) ignorar, conforme requisito de negócio.

### `features/auth/errors/authErrors.ts`
- Novo `RefreshFailedError` (401 em `/auth/refresh` — refresh token
  ausente/expirado/inválido; frontend trata os dois `type` do backend
  da mesma forma, como já previsto na spec).

### `features/auth/hooks/useSessionBootstrap.ts` (novo)
- Roda uma vez (efeito sem dependências) ao montar: chama
  `authApi.refresh()`; sucesso → `authStore.setSession(...)`; qualquer
  falha (401 ou rede) → não faz nada (sessão permanece vazia,
  `ProtectedRoute` redireciona para `/login` como já acontece hoje).
- Expõe `{ isBootstrapping: boolean }`.
- Teste cobrindo: sucesso popula a store; 401 mantém store vazia; erro
  de rede mantém store vazia (nenhum dos dois trava `isBootstrapping`
  em `true`).

### `features/auth/hooks/useLogout.ts` (novo)
- Chama `authApi.logout()` (erro ignorado via `try/catch` vazio,
  conforme requisito), depois `authStore.getState().clearSession()`.
- Retorna uma função `logout()` para o componente chamar e navegar.

### `app/authBootstrap.ts` (novo)
- Única chamada a `registerAuthPlugin`, ligando `httpClient` a
  `authStore`/`authApi.refresh()`:
  ```ts
  registerAuthPlugin({
    getAccessToken: () => useAuthStore.getState().token,
    refreshAccessToken: async () => {
      try {
        const result = await authApi.refresh()
        useAuthStore.getState().setSession(result.accessToken, result.userId, result.expiresIn)
        return result.accessToken
      } catch (err) {
        if (err instanceof RefreshFailedError) return null
        throw err
      }
    },
    onSessionExpired: () => useAuthStore.getState().clearSession(),
  })
  ```
- Importado uma vez em `app/App.tsx` (efeito colateral no import, ou
  chamado explicitamente no topo do módulo de `App.tsx`).

### `app/App.tsx`
- Usa `useSessionBootstrap()`; enquanto `isBootstrapping` é `true`,
  renderiza `null` (tela em branco breve — sem spinner dedicado, fora
  do escopo da spec); depois renderiza `RouterProvider` normalmente.

### `routes/SettingsPage.tsx`
- Troca `useAuthStore((state) => state.clearSession)` por
  `useLogout()`; `handleLogout` passa a ser `async`, chama `logout()`
  do hook e então navega — mantém o comportamento visível idêntico ao
  atual (só adiciona a chamada a `/auth/logout` por baixo).

### `features/expenses/*`
- **Nenhuma mudança de código.** O retry transparente funciona porque
  `httpClient` sobrescreve o `Authorization` internamente (ver decisão
  de arquitetura acima). Os `clearSession()` manuais em cada hook de
  expenses continuam funcionando como fallback (caso a `httpClient` já
  tenha limpado a sessão via `onSessionExpired`, chamar `clearSession()`
  de novo é idempotente).

## Contratos técnicos (DTOs)
Sem mudança de contrato de wire — reaproveita
`backend/docs/openapi.json` (`/auth/refresh`, `/auth/logout`) já
documentado na spec. No frontend:
- `authApi.refresh()` retorna o mesmo shape de `authApi.login()`
  (`{ accessToken, expiresIn, userId }`).
- `authApi.logout()` retorna `void`.

## Recursos AWS
Nenhum recurso novo ou alterado — feature é só consumo, do lado do
frontend, de um contrato já implementado no backend.

## Mapeamento de erros
| Cenário | Origem | Tratamento |
|---|---|---|
| 401 em chamada autenticada + refresh OK | `httpClient` | Retry transparente, sem erro visível |
| 401 em chamada autenticada + refresh 401 | `httpClient` → `onSessionExpired` | Resposta 401 original repassada; erro tipado já existente de cada feature (`SessionExpiredError`, etc.) |
| 401 em `/auth/refresh` (boot) | `useSessionBootstrap` | Sessão permanece vazia; `ProtectedRoute` redireciona |
| Falha de rede em `/auth/refresh` (durante uso) | `httpClient` repropaga exceção | `safeFetch` de cada feature converte em `NetworkError`, sessão intacta |
| Falha de rede em `/auth/refresh` (boot) | `useSessionBootstrap` | Sessão permanece vazia (mesmo efeito que 401, decisão simplificadora aceita) |
| Falha em `/auth/logout` | `useLogout` | Ignorada; logout local ocorre de qualquer forma |

## Dependência externa (fora deste plano)
Backend precisa adicionar `.AllowCredentials()` (e provavelmente trocar
`AllowAnyHeader()/AllowAnyMethod()` por listas explícitas, exigência do
CORS spec quando `AllowCredentials` está ativo) na policy `"Frontend"`
em `backend/src/GastosApp.Api/Program.cs`. **Usuário confirmou que vai
tratar isso separadamente**, como mudança Modo Leve no contexto
backend — sem isso, os testes end-to-end reais (navegador) desta
feature não funcionam, mesmo com os testes unitários/componente do
frontend passando (MSW não reforça CORS).

## Verificação
- `cd frontend/app && npm test` — 100% dos testes passando (novos +
  existentes), incluindo os novos `httpClient.test.ts`,
  `useSessionBootstrap.test.ts`, `useLogout.test.ts`, e ajustes em
  `SettingsPage.test.tsx`.
- Verificação manual (após o backend aplicar `AllowCredentials`):
  `npm run dev`, login, F5 na rota protegida → permanece logado;
  expirar o `accessToken` manualmente (ex. via devtools, alterando
  `expiresAt` na store) e navegar → renovação silenciosa; logout →
  cookie limpo (checar em devtools) e refresh subsequente retorna 401.
