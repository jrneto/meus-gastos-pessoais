/**
 * `session-id` de observabilidade (FEAT-38 do backend, header
 * `session-id`) — identifica uma sessão de uso, não a requisição
 * isolada (`trace-id`, gerado por chamada em `httpClient.ts`) nem o
 * token de acesso (Cognito). Conceito de aplicação, independente de
 * refresh de token: só um login novo gera um valor novo.
 *
 * Guardado em `sessionStorage` (não `localStorage`, e não a memória do
 * Zustand como o access token — ver constitution.md): precisa
 * sobreviver a um F5 no meio do uso (`useSessionBootstrap` não deve
 * gerar um valor novo nesse caso), mas não é dado sensível, então não
 * se aplica a mesma restrição do token. `sessionStorage` é isolado por
 * aba — fechar a aba/navegador e reabrir (ou abrir uma segunda aba
 * digitando a URL) começa com um valor novo, mesmo com login
 * automático via cookie de refresh: cada aba representa uma jornada de
 * uso independente para fins de correlação de log.
 */

const STORAGE_KEY = 'gastosapp.sessionId'

function generate(): string {
  return crypto.randomUUID()
}

/**
 * Lê o `session-id` atual, sem gerar um novo. `null` quando ainda não
 * há sessão (ex.: antes do login/bootstrap terminar).
 */
export function getSessionId(): string | null {
  try {
    return sessionStorage.getItem(STORAGE_KEY)
  } catch {
    // Storage indisponível (ex.: alguns modos de navegação privada) —
    // observabilidade é best-effort, nunca pode quebrar a aplicação.
    return null
  }
}

/**
 * Gera e guarda um `session-id` novo, substituindo o que já existir.
 * Chamar só a partir de um login explícito bem-sucedido (`useLogin`).
 */
export function startNewSession(): string {
  const id = generate()
  try {
    sessionStorage.setItem(STORAGE_KEY, id)
  } catch {
    // Idem getSessionId — best-effort.
  }
  return id
}

/**
 * Reaproveita o `session-id` já guardado nesta aba (ex.: F5 no meio do
 * uso) ou gera um novo se não houver nenhum (primeira visita, ou aba/
 * navegador reaberto depois de fechado). Chamar a partir do bootstrap
 * silencioso de sessão (`useSessionBootstrap`).
 */
export function ensureSessionId(): string {
  return getSessionId() ?? startNewSession()
}

/** Limpa o `session-id` da aba atual. Chamar a partir do logout. */
export function clearSessionId(): void {
  try {
    sessionStorage.removeItem(STORAGE_KEY)
  } catch {
    // Idem getSessionId — best-effort.
  }
}
