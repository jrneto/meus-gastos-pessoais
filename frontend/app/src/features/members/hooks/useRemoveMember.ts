import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { membersApi } from '../api/membersApi'
import { SessionExpiredError } from '../errors/memberErrors'

interface UseRemoveMemberResult {
  removeMember: (id: string) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useRemoveMember(): UseRemoveMemberResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function removeMember(id: string): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await membersApi.removeMember(token ?? '', id)
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

  return { removeMember, isLoading, error, success }
}
