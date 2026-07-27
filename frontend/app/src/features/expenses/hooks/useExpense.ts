import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { expensesApi, type ExpenseDetail } from '../api/expensesApi'
import { SessionExpiredError } from '../errors/expenseErrors'

interface UseExpenseResult {
  data: ExpenseDetail | null
  isLoading: boolean
  error: Error | null
}

export function useExpense(id: string): UseExpenseResult {
  const [data, setData] = useState<ExpenseDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    expensesApi
      .getExpenseById(token ?? '', id)
      .then((result) => {
        if (!cancelled) {
          setData(result)
        }
      })
      .catch((err) => {
        if (cancelled) {
          return
        }
        if (err instanceof SessionExpiredError) {
          useAuthStore.getState().clearSession()
        }
        setError(err as Error)
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false)
        }
      })
    return () => {
      cancelled = true
    }
  }, [id, token])

  return { data, isLoading, error }
}
