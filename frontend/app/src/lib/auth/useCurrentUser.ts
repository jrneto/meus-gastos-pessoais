import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { CurrentUser } from './currentUserApi'
import { currentUserApi } from './currentUserApi'
import { SessionExpiredError } from './currentUserErrors'

interface UseCurrentUserResult {
  data: CurrentUser | null
  isLoading: boolean
  error: Error | null
}

export function useCurrentUser(): UseCurrentUserResult {
  const [data, setData] = useState<CurrentUser | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    currentUserApi
      .getCurrentUser(token ?? '')
      .then((result) => {
        if (!cancelled) setData(result)
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

  return { data, isLoading, error }
}
