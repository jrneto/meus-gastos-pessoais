const API_BASE_URL = import.meta.env.VITE_API_BASE_URL

// Paths que nunca passam pelo interceptor de auth (nem Authorization
// automático, nem refresh-on-401): /auth/login não tem token ainda, e
// /auth/refresh é a própria chamada de renovação — deixá-la disparar
// um refresh recursivamente causaria loop infinito.
const AUTH_INTERCEPTOR_EXCLUDED_PATHS = ['/auth/login', '/auth/refresh']

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

function isAuthInterceptorExcluded(path: string): boolean {
  return AUTH_INTERCEPTOR_EXCLUDED_PATHS.some((excluded) => path.startsWith(excluded))
}

function buildHeaders(init: RequestInit | undefined): HeadersInit {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(init?.headers as Record<string, string> | undefined),
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

  const newToken = await ensureRefreshed()

  if (newToken === null) {
    authPlugin.onSessionExpired()
    return response
  }

  return rawRequest(path, init)
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
