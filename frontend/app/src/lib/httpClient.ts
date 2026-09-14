import { getAppVersion } from './appVersion'
import { getSessionId } from './sessionId'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL

// Paths que nunca passam pelo interceptor de auth (nem Authorization
// automático, nem refresh-on-401): /auth/login não tem token ainda, e
// /auth/refresh é a própria chamada de renovação — deixá-la disparar
// um refresh recursivamente causaria loop infinito.
const AUTH_INTERCEPTOR_EXCLUDED_PATHS = ['/auth/login', '/auth/refresh']

// Headers de observabilidade (backend/specs/FEAT-38), todos opcionais
// do ponto de vista da API — client-platform é fixo (única origem web
// hoje), sem lista fechada de valores exigida pelo backend.
const CLIENT_PLATFORM = 'web'

export interface AuthPlugin {
  getAccessToken: () => string | null
  // Retorna o novo accessToken em caso de sucesso, ou `null` quando o
  // refresh foi recusado (sessão inválida — ex.: 401). Lança exceção
  // para falha de rede, propositalmente distinta de `null`.
  refreshAccessToken: () => Promise<string | null>
  onSessionExpired: () => void
}

let authPlugin: AuthPlugin | null = null

/**
 * Liga o httpClient a uma implementação de auth (token atual, renovação,
 * limpeza de sessão), sem que este módulo (`lib/`) precise importar
 * nada de `features/auth` — ver `app/authBootstrap.ts`.
 */
export function registerAuthPlugin(plugin: AuthPlugin): void {
  authPlugin = plugin
}

// Deduplica chamadas de refresh concorrentes: a primeira 401 dispara o
// refresh, as demais aguardam a mesma promise em vez de disparar
// refreshes paralelos.
let refreshPromise: Promise<string | null> | null = null

function ensureRefreshed(): Promise<string | null> {
  if (!refreshPromise) {
    refreshPromise = (authPlugin as AuthPlugin)
      .refreshAccessToken()
      .finally(() => {
        refreshPromise = null
      })
  }
  return refreshPromise
}

// Disjuntor contra loop de refresh: cada hook de leitura (`useMembers`,
// `useCategories`, ...) refaz sua chamada quando `token` muda no
// authStore — inclusive quando quem mudou foi um refresh disparado por
// OUTRO hook. Se o recurso pedido continuar devolvendo 401 mesmo com o
// token novo (ex.: bug de contrato, não sessão expirada), cada um desses
// hooks dispara seu próprio ciclo de refresh, que muda `token` de novo,
// que dispara todos de novo — loop sem fim, visto na prática ao testar a
// FEAT-41 (front ainda não ajustado ao novo contrato). Sem relação com
// `refreshPromise` acima, que só deduplica refreshes *simultâneos* — o
// problema aqui é uma sequência de ciclos completos (refresh bem
// sucedido, retry ainda 401), um atrás do outro.
let consecutiveRefreshFailures = 0
const MAX_CONSECUTIVE_REFRESH_FAILURES = 2

/**
 * Rearma o disjuntor após uma sessão nova de fato (login explícito) —
 * sem isso, uma sessão anterior que disparou o loop deixaria o
 * disjuntor acionado pelo resto da aba, bloqueando até um refresh
 * legítimo da sessão nova. Ver `features/auth/hooks/useLogin.ts`.
 */
export function resetAuthCircuitBreaker(): void {
  consecutiveRefreshFailures = 0
}

function isAuthInterceptorExcluded(path: string): boolean {
  return AUTH_INTERCEPTOR_EXCLUDED_PATHS.some((excluded) => path.startsWith(excluded))
}

function buildHeaders(init: RequestInit | undefined): HeadersInit {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    // trace-id identifica esta requisição isolada — um valor novo por
    // chamada (diferente de session-id, que persiste durante o uso).
    'trace-id': crypto.randomUUID(),
    'client-platform': CLIENT_PLATFORM,
    'client-version': getAppVersion(),
    ...(init?.headers as Record<string, string> | undefined),
  }

  // Ausente antes do login/bootstrap terminar — omitido nesse caso
  // (a API não exige o header, ver FEAT-38), nunca enviado vazio.
  const sessionId = getSessionId()
  if (sessionId) {
    headers['session-id'] = sessionId
  }

  const token = authPlugin?.getAccessToken()
  if (token) {
    headers.Authorization = `Bearer ${token}`
  }

  return headers
}

async function rawRequest(path: string, init: RequestInit | undefined): Promise<Response> {
  return fetch(`${API_BASE_URL}${path}`, {
    ...init,
    credentials: 'include',
    headers: buildHeaders(init),
  })
}

async function request(path: string, init?: RequestInit): Promise<Response> {
  const response = await rawRequest(path, init)

  if (response.status !== 401 || !authPlugin || isAuthInterceptorExcluded(path)) {
    return response
  }

  if (consecutiveRefreshFailures >= MAX_CONSECUTIVE_REFRESH_FAILURES) {
    // Já tentamos renovar e repetir repetidas vezes seguidas sem que o
    // recurso voltasse a funcionar — trata como sessão inválida em vez
    // de insistir indefinidamente. Não dispara mais nenhum refresh:
    // é isso que quebra o loop (sem ele, `onSessionExpired` zera o
    // token, mas o próximo 401 tentaria renovar de novo do mesmo jeito).
    authPlugin.onSessionExpired()
    return response
  }

  const newToken = await ensureRefreshed()

  if (newToken === null) {
    consecutiveRefreshFailures = 0
    authPlugin.onSessionExpired()
    return response
  }

  const retried = await rawRequest(path, init)
  consecutiveRefreshFailures = retried.status === 401 ? consecutiveRefreshFailures + 1 : 0
  return retried
}

export const httpClient = {
  get: (path: string, init?: RequestInit) => request(path, { ...init, method: 'GET' }),
  post: (path: string, body?: unknown, init?: RequestInit) =>
    request(path, {
      ...init,
      method: 'POST',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  put: (path: string, body?: unknown, init?: RequestInit) =>
    request(path, {
      ...init,
      method: 'PUT',
      body: body !== undefined ? JSON.stringify(body) : undefined,
    }),
  delete: (path: string, init?: RequestInit) => request(path, { ...init, method: 'DELETE' }),
}
