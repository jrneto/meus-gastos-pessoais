import { useState } from 'react'
import { authApi, type ConfirmPayload } from '../api/authApi'

interface UseConfirmAccountResult {
  confirm: (payload: ConfirmPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

// Mesmo formato de `useRegister`: não toca a `authStore` (confirmar a
// conta não autentica, o usuário ainda precisa logar depois).
export function useConfirmAccount(): UseConfirmAccountResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)

  async function confirm(payload: ConfirmPayload): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await authApi.confirm(payload)
      setSuccess(true)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { confirm, isLoading, error, success }
}
