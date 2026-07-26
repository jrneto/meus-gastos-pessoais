import { useState } from 'react'
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
      setSession(result.accessToken, result.userId, result.expiresIn)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { login, isLoading, error }
}