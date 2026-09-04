import { useState } from 'react'
import { authApi, type ResetPasswordPayload } from '../api/authApi'

interface UseResetPasswordResult {
  resetPassword: (payload: ResetPasswordPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

// Mesmo formato de `useConfirmAccount`: não toca a `authStore` — redefinir
// a senha não autentica, o usuário ainda precisa logar depois.
export function useResetPassword(): UseResetPasswordResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)

  async function resetPassword(payload: ResetPasswordPayload): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await authApi.resetPassword(payload)
      setSuccess(true)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { resetPassword, isLoading, error, success }
}
