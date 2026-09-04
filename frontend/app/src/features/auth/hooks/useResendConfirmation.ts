import { useState } from 'react'
import { authApi, type ResendConfirmationPayload } from '../api/authApi'

interface UseResendConfirmationResult {
  resend: (payload: ResendConfirmationPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
}

// Mesmo formato de `useLogin`, sem tocar a `authStore` — o backend
// sempre responde 200 (FEAT-35), então `error` só é populado por falha
// técnica (rede ou erro inesperado).
export function useResendConfirmation(): UseResendConfirmationResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)

  async function resend(payload: ResendConfirmationPayload): Promise<void> {
    setIsLoading(true)
    setError(null)
    try {
      await authApi.resendConfirmation(payload)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { resend, isLoading, error }
}
