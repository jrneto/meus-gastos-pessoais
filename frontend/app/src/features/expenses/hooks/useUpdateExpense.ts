import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { expensesApi } from '../api/expensesApi'
import { SessionExpiredError } from '../errors/expenseErrors'
import type { ExpenseFormOutput } from '../schemas/expenseSchema'

interface UseUpdateExpenseResult {
  updateExpense: (data: ExpenseFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useUpdateExpense(id: string): UseUpdateExpenseResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function updateExpense(data: ExpenseFormOutput): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await expensesApi.updateExpense(token ?? '', id, {
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

  return { updateExpense, isLoading, error, success }
}
