import { authApi } from '../api/authApi'
import { useAuthStore } from '../store/authStore'

interface UseLogoutResult {
  logout: () => Promise<void>
}

/**
 * Encerra a sessão no backend (`POST /auth/logout`, limpa o cookie de
 * refresh token) e localmente (`clearSession`). Falha na chamada ao
 * backend é ignorada — o usuário sai da sessão localmente de qualquer
 * forma.
 */
export function useLogout(): UseLogoutResult {
  const clearSession = useAuthStore((state) => state.clearSession)

  async function logout(): Promise<void> {
    try {
      await authApi.logout()
    } catch {
      // Falha ao encerrar a sessão no backend não impede o logout local.
    } finally {
      clearSession()
    }
  }

  return { logout }
}
