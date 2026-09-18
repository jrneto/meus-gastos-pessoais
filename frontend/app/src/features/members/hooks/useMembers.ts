import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { MemberItem } from '../api/membersApi'
import { membersApi } from '../api/membersApi'
import { SessionExpiredError } from '../errors/memberErrors'

interface UseMembersResult {
  items: MemberItem[]
  isLoading: boolean
  isRefetching: boolean
  error: Error | null
  refetch: () => void
}

// Busca a lista de membros da conta ativa. Convidar/trocar papel
// seguem atualizando o estado local da página sem recarregar a lista
// inteira (mesmo racional de `useReports`, FEAT-27) — remover é
// diferente: a resposta de `DELETE /members/{id}` não diferencia
// remoção de fato de inativação (FEAT-41 do backend), então só dá pra
// saber o que realmente aconteceu buscando a lista de novo (`refetch`,
// FEAT-35, ver plan.md). `refetch` usa `isRefetching`, não `isLoading`
// — a página não deve esconder a lista inteira só porque uma remoção
// disparou uma nova busca dos mesmos dados na maioria das vezes.
export function useMembers(): UseMembersResult {
  const [items, setItems] = useState<MemberItem[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isRefetching, setIsRefetching] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  async function fetchMembers(showLoading: boolean, cancelledRef?: { current: boolean }): Promise<void> {
    if (showLoading) setIsLoading(true)
    else setIsRefetching(true)
    setError(null)
    try {
      const result = await membersApi.getMembers(token ?? '')
      if (!cancelledRef?.current) setItems(result.items)
    } catch (err) {
      if (cancelledRef?.current) return
      if (err instanceof SessionExpiredError) {
        useAuthStore.getState().clearSession()
      }
      setError(err as Error)
    } finally {
      if (!cancelledRef?.current) {
        if (showLoading) setIsLoading(false)
        else setIsRefetching(false)
      }
    }
  }

  useEffect(() => {
    const cancelledRef = { current: false }
    fetchMembers(true, cancelledRef)
    return () => {
      cancelledRef.current = true
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [token])

  function refetch(): void {
    fetchMembers(false)
  }

  return { items, isLoading, isRefetching, error, refetch }
}
