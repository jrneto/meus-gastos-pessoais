import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { MemberItem, MemberRole } from '../api/membersApi'
import { membersApi } from '../api/membersApi'
import { SessionExpiredError } from '../errors/memberErrors'

interface UseUpdateMemberRoleResult {
  updateRole: (role: Exclude<MemberRole, 'Titular'>) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
  data: MemberItem | null
}

export function useUpdateMemberRole(id: string): UseUpdateMemberRoleResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const [data, setData] = useState<MemberItem | null>(null)
  const token = useAuthStore((state) => state.token)

  async function updateRole(role: Exclude<MemberRole, 'Titular'>): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      const updated = await membersApi.updateMemberRole(token ?? '', id, role)
      setData(updated)
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

  return { updateRole, isLoading, error, success, data }
}
