import { useEffect, useState } from 'react'
import { ensureSessionId } from '@/lib/sessionId'
import { authApi } from '../api/authApi'
import { useAuthStore } from '../store/authStore'

interface UseSessionBootstrapResult {
  isBootstrapping: boolean
}

/**
 * Tenta restaurar a sessão silenciosamente no boot da aplicação (ex.:
 * F5), via `POST /auth/refresh` (cookie httpOnly). Qualquer falha (401
 * ou erro de rede) é ignorada — a sessão permanece vazia e
 * `ProtectedRoute` redireciona para `/login` como já acontece hoje.
 */
export function useSessionBootstrap(): UseSessionBootstrapResult {
  const [isBootstrapping, setIsBootstrapping] = useState(true)
  const setSession = useAuthStore((state) => state.setSession)

  useEffect(() => {
    let active = true

    async function bootstrap() {
      try {
        const result = await authApi.refresh()
        if (active) {
          // Reaproveita o session-id já guardado nesta aba (F5) ou gera
          // um novo se não houver (aba/navegador reaberto depois de
          // fechado) — nunca um login explícito, então nunca força um
          // valor novo quando já existe um. Ver lib/sessionId.ts.
          ensureSessionId()
          setSession(result.accessToken, result.userId, result.expiresIn)
        }
      } catch {
        // Sem sessão restaurável — segue sem token, tratado pelo
        // ProtectedRoute.
      } finally {
        if (active) {
          setIsBootstrapping(false)
        }
      }
    }

    void bootstrap()

    return () => {
      active = false
    }
  }, [setSession])

  return { isBootstrapping }
}
