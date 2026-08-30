import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { transactionsApi, type TransactionQueryItem, type GetTransactionsParams } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'
import type { TransactionFilterOutput } from '../schemas/transactionFilterSchema'

interface UseTransactionsQueryResult {
  items: TransactionQueryItem[]
  isLoading: boolean
  isLoadingMore: boolean
  error: Error | null
  hasMore: boolean
  applyFilters: (filters: TransactionFilterOutput) => void
  loadMore: () => void
  removeItem: (id: string) => void
  refetch: () => void
}

export function useTransactionsQuery(): UseTransactionsQueryResult {
  const [items, setItems] = useState<TransactionQueryItem[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [filters, setFilters] = useState<GetTransactionsParams>({})
  const [isLoading, setIsLoading] = useState(false)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  async function fetchPage(
    nextFilters: GetTransactionsParams,
    cursorParam: string | null,
    append: boolean,
  ): Promise<void> {
    if (append) {
      setIsLoadingMore(true)
    } else {
      setIsLoading(true)
    }
    setError(null)
    try {
      const result = await transactionsApi.getTransactions(token ?? '', {
        ...nextFilters,
        cursor: cursorParam ?? undefined,
      })
      setItems((prev) => (append ? [...prev, ...result.items] : result.items))
      setCursor(result.nextCursor)
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        useAuthStore.getState().clearSession()
      }
      setError(err as Error)
    } finally {
      if (append) {
        setIsLoadingMore(false)
      } else {
        setIsLoading(false)
      }
    }
  }

  useEffect(() => {
    fetchPage({}, null, false)
  }, [])

  function applyFilters(newFilters: TransactionFilterOutput): void {
    setFilters(newFilters)
    setCursor(null)
    fetchPage(newFilters, null, false)
  }

  function loadMore(): void {
    if (!cursor) {
      return
    }
    fetchPage(filters, cursor, true)
  }

  function removeItem(id: string): void {
    setItems((prev) => prev.filter((item) => item.id !== id))
  }

  // Reexecuta a busca com os filtros já aplicados (primeira página) —
  // usado para atualizar a listagem depois de criar uma transação
  // (FEAT-17), sem alterar os filtros em uso.
  function refetch(): void {
    fetchPage(filters, null, false)
  }

  return {
    items,
    isLoading,
    isLoadingMore,
    error,
    hasMore: cursor !== null,
    applyFilters,
    loadMore,
    removeItem,
    refetch,
  }
}
