import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { transactionsApi, type TransactionDetail } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'

interface UseTransactionResult {
  data: TransactionDetail | null
  isLoading: boolean
  error: Error | null
}

export function useTransaction(id: string): UseTransactionResult {
  const [data, setData] = useState<TransactionDetail | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    // Sem id, não há o que buscar — usado pelo popup unificado de
    // formulário (FEAT-18), que chama este hook incondicionalmente
    // mesmo no modo cadastro, onde não existe transação a carregar.
    if (!id) {
      setIsLoading(false)
      return
    }

    let cancelled = false
    setIsLoading(true)
    setError(null)
    transactionsApi
      .getTransactionById(token ?? '', id)
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
