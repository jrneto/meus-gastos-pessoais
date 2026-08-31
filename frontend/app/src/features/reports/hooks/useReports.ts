import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { ReportPeriod, ReportsResponse } from '../api/reportsApi'
import { reportsApi } from '../api/reportsApi'
import { SessionExpiredError } from '../errors/reportsErrors'

interface UseReportsResult {
  data: ReportsResponse | null
  isLoading: boolean
  error: Error | null
}

// Busca o relatório do `period`/`date` informados — chamado pela
// ReportsPage (FEAT-27) com a data corrente e o período selecionado no
// `PeriodToggle`. Sem `refetch`: esta tela não tem nenhuma ação de
// escrita (itens da lista não são clicáveis, sem botão de nova
// transação no design), então não há gatilho que precise refazer a
// busca fora de `period`/`date` mudarem.
export function useReports(period: ReportPeriod, date: string): UseReportsResult {
  const [data, setData] = useState<ReportsResponse | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    reportsApi
      .getReports(token ?? '', period, date)
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
  }, [period, date, token])

  return { data, isLoading, error }
}
