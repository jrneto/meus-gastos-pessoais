import { useState } from 'react'
import { authApi, type ForgotPasswordPayload } from '../api/authApi'

interface UseForgotPasswordResult {
  forgotPassword: (payload: ForgotPasswordPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
}

// Mesmo formato de `useResendConfirmation`, sem tocar a `authStore` — o
// backend sempre responde 200 (FEAT-36), então `error` só é populado
// por falha técnica (rede ou erro inesperado).
export function useForgotPassword(): UseForgotPasswordResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  async function forgotPassword(payload: ForgotPasswordPayload): Promise<void> {
    setIsLoading(true)
    setError(null)
    try {
      await authApi.forgotPassword(payload)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { forgotPassword, isLoading, error }
}
