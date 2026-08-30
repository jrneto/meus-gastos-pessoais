import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { expensesApi } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'

interface UseDeleteExpenseResult {
  deleteExpense: (id: string) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useDeleteExpense(): UseDeleteExpenseResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function deleteExpense(id: string): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await expensesApi.deleteExpense(token ?? '', id)
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

  return { deleteExpense, isLoading, error, success }
}
