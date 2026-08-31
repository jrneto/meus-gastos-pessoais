import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { MemberItem } from '../api/membersApi'
import { membersApi } from '../api/membersApi'
import { SessionExpiredError } from '../errors/memberErrors'

interface UseMembersResult {
  items: MemberItem[]
  isLoading: boolean
  error: Error | null
}

// Busca a lista de membros da conta ativa — sem `refetch`: as próprias
// mutações desta feature (convidar/trocar papel/remover) atualizam o
// estado local da página que orquestra este hook, sem precisar
// recarregar a lista inteira (mesmo racional de `useReports`, FEAT-27,
// ver plan.md).
export function useMembers(): UseMembersResult {
  const [items, setItems] = useState<MemberItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    let cancelled = false
    setIsLoading(true)
    setError(null)
    membersApi
      .getMembers(token ?? '')
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
