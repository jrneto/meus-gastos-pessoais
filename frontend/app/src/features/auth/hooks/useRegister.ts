import { useState } from 'react'
import { authApi, type RegisterPayload } from '../api/authApi'

interface UseRegisterResult {
  register: (payload: RegisterPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

// Diferente de `useLogin`, não popula a `authStore`: o cadastro nunca
// autentica automaticamente (a conta fica pendente de aprovação manual
// no Cognito — ver spec.md, "Decisão fechada"). `success` sinaliza pro
// formulário exibir a confirmação e voltar ao modo "Entrar".
export function useRegister(): UseRegisterResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)

  async function register(payload: RegisterPayload): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await authApi.register(payload)
      setSuccess(true)
    } catch (err) {
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { register, isLoading, error, success }
}
