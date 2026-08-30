import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { expensesApi } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'
import type { ExpenseFormOutput } from '../schemas/transactionSchema'

interface UseRegisterExpenseResult {
  registerExpense: (data: ExpenseFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useRegisterExpense(): UseRegisterExpenseResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function registerExpense(data: ExpenseFormOutput): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await expensesApi.registerExpense(token ?? '', {
        description: data.description,
        amountInCents: data.amount,
        categoryId: data.categoryId,
        expenseDate: data.expenseDate,
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

  return { registerExpense, isLoading, error, success }
}
