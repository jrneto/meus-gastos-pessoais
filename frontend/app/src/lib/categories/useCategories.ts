import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { categoriesReadApi } from './categoriesReadApi'
import { SessionExpiredError } from './categoryErrors'
import type { CategoryItem } from './types'

interface UseCategoriesResult {
  items: CategoryItem[]
  isLoading: boolean
  error: Error | null
}

export function useCategories(): UseCategoriesResult {
  const [items, setItems] = useState<CategoryItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    categoriesReadApi
      .getCategories(token ?? '')
      .then((result) => {
        if (!cancelled) setItems(result.items)
      })
      .catch((err) => {
        if (cancelled) return
        if (err instanceof SessionExpiredError) {
          useAuthStore.getState().clearSession()
        }
        setError(err as Error)
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [token])

  return { items, isLoading, error }
}
