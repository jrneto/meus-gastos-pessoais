import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { summaryApi, type SummaryResponse } from '../api/summaryApi'
import { SessionExpiredError } from '../errors/summaryErrors'

interface UseSummaryResult {
  data: SummaryResponse | null
  isLoading: boolean
  error: Error | null
  refetch: () => void
}

// Busca o resumo do `month` informado — chamado com o mês corrente
// pelo DashboardPage (FEAT-26), sem navegação pra outros meses nesta
// feature. `refetch()` refaz a busca do mesmo mês, usado pelo
// `onSaved` do TransactionFormDialog reaproveitado na tela.
export function useSummary(month: string): UseSummaryResult {
  const [data, setData] = useState<SummaryResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const [refetchToken, setRefetchToken] = useState(0)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    summaryApi
      .getSummary(token ?? '', month)
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
  }, [month, token, refetchToken])

  function refetch(): void {
    setRefetchToken((value) => value + 1)
  }

  return { data, isLoading, error, refetch }
}
