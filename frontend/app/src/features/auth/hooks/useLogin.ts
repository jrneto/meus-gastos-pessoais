import { useState } from 'react'
import { resetAuthCircuitBreaker } from '@/lib/httpClient'
import { startNewSession } from '@/lib/sessionId'
import { authApi } from '../api/authApi'
import type { LoginCredentials } from '../schemas/loginSchema'
import { useAuthStore } from '../store/authStore'

interface UseLoginResult {
  login: (credentials: LoginCredentials) => Promise<void>
  isLoading: boolean
  error: Error | null
}

export function useLogin(): UseLoginResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const setSession = useAuthStore((state) => state.setSession)

  async function login(credentials: LoginCredentials): Promise<void> {
    setIsLoading(true)
    setError(null)
    try {
      const result = await authApi.login(credentials)
      // Login explícito sempre gera um session-id novo, mesmo que já
      // exista um da aba (ex.: logout seguido de login de novo) — ver
      // lib/sessionId.ts.
      startNewSession()
      // Idem para o disjuntor de refresh do httpClient — uma sessão
      // anterior que tenha disparado o loop de 401 não deve deixá-lo
      // acionado pra sessão nova (ver lib/httpClient.ts).
      resetAuthCircuitBreaker()
      setSession(result.accessToken, result.userId, result.expiresIn)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { login, isLoading, error }
}