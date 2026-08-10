import { authApi } from '@/features/auth/api/authApi'
import { RefreshFailedError } from '@/features/auth/errors/authErrors'
import { useAuthStore } from '@/features/auth/store/authStore'
import { registerAuthPlugin } from '@/lib/httpClient'

// Único ponto de ligação entre `lib/httpClient` (que não pode importar
// nada de `features/*`) e a auth real da aplicação. Chamado uma vez,
// na inicialização (ver `app/App.tsx`).
export function setupAuthBootstrap(): void {
  registerAuthPlugin({
    getAccessToken: () => useAuthStore.getState().token,
    refreshAccessToken: async () => {
      try {
        const result = await authApi.refresh()
        useAuthStore.getState().setSession(result.accessToken, result.userId, result.expiresIn)
        return result.accessToken
      } catch (err) {
        if (err instanceof RefreshFailedError) {
          return null
        }
        throw err
      }
    },
    onSessionExpired: () => useAuthStore.getState().clearSession(),
  })
}
