import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { transactionsApi } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'
import type { TransactionFormOutput } from '../schemas/transactionSchema'

interface UseRegisterTransactionResult {
  registerTransaction: (data: TransactionFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useRegisterTransaction(tipo: 'despesa' | 'receita'): UseRegisterTransactionResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function registerTransaction(data: TransactionFormOutput): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await transactionsApi.registerTransaction(token ?? '', {
        description: data.description,
        amountInCents: data.amount,
        categoryId: data.categoryId,
        tipo,
        date: data.date,
      })
      setSuccess(true)
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        useAuthStore.getState().clearSession()
      }
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { registerTransaction, isLoading, error, success }
}
