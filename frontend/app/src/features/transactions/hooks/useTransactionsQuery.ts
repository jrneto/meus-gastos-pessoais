import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { expensesApi, type ExpenseQueryItem, type GetExpensesParams } from '../api/transactionsApi'
import { SessionExpiredError } from '../errors/transactionErrors'
import type { ExpenseFilterOutput } from '../schemas/transactionFilterSchema'

interface UseExpensesQueryResult {
  items: ExpenseQueryItem[]
  isLoading: boolean
  isLoadingMore: boolean
  error: Error | null
  hasMore: boolean
  applyFilters: (filters: ExpenseFilterOutput) => void
  loadMore: () => void
  removeItem: (id: string) => void
  refetch: () => void
}

export function useExpensesQuery(): UseExpensesQueryResult {
  const [items, setItems] = useState<ExpenseQueryItem[]>([])
  const [cursor, setCursor] = useState<string | null>(null)
  const [filters, setFilters] = useState<GetExpensesParams>({})
  const [isLoading, setIsLoading] = useState(false)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  async function fetchPage(
    nextFilters: GetExpensesParams,
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
      const result = await expensesApi.getExpenses(token ?? '', {
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

  function applyFilters(newFilters: ExpenseFilterOutput): void {
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
  // usado para atualizar a listagem depois de criar uma despesa
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
