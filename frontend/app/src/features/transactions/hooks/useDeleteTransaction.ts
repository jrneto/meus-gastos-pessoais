import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { transactionsApi } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'

interface UseDeleteTransactionResult {
  deleteTransaction: (id: string) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useDeleteTransaction(): UseDeleteTransactionResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function deleteTransaction(id: string): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await transactionsApi.deleteTransaction(token ?? '', id)
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

  return { deleteTransaction, isLoading, error, success }
}
