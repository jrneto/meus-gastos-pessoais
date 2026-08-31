import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { InviteMemberPayload, MemberItem } from '../api/membersApi'
import { membersApi } from '../api/membersApi'
import { SessionExpiredError } from '../errors/memberErrors'

interface UseInviteMemberResult {
  inviteMember: (payload: InviteMemberPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
  data: MemberItem | null
}

export function useInviteMember(): UseInviteMemberResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const [data, setData] = useState<MemberItem | null>(null)
  const token = useAuthStore((state) => state.token)

  async function inviteMember(payload: InviteMemberPayload): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      const created = await membersApi.inviteMember(token ?? '', payload)
      setData(created)
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

  return { inviteMember, isLoading, error, success, data }
}
