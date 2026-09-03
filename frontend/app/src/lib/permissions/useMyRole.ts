import { useEffect, useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { useCurrentUser } from '@/lib/auth/useCurrentUser'
import { membershipReadApi } from './membershipReadApi'
import { SessionExpiredError } from './permissionErrors'
import type { MemberRole } from './types'

interface UseMyRoleResult {
  role: MemberRole | null
  userId: string | null
  isLoading: boolean
  error: Error | null
}

// Não existe endpoint que devolva "qual é o meu papel" — cruza GET
// /auth/me (lib/auth/useCurrentUser, já existente) com GET /members por
// e-mail, mesma mecânica já usada (inline) em routes/MembersPage.tsx.
// Consumido por features/transactions e features/categories, que não
// podem importar uma da outra — por isso este hook mora em lib/, não em
// features/members (ver plan.md, decisões 1/2).
export function useMyRole(): UseMyRoleResult {
  const { data: currentUser, isLoading: userLoading, error: userError } = useCurrentUser()
  const [role, setRole] = useState<MemberRole | null>(null)
  const [membersLoading, setMembersLoading] = useState(true)
  const [membersError, setMembersError] = useState<Error | null>(null)
  const token = useAuthStore((state) => state.token)

  useEffect(() => {
    // GET /auth/me ainda em andamento — nada pra cruzar ainda, mantém
    // membersLoading true (estado inicial) até o e-mail estar disponível.
    if (userLoading) {
      return undefined
    }
    // GET /auth/me terminou sem sucesso (userError) — não há e-mail pra
    // buscar em GET /members, então não há mais nada carregando aqui.
    if (!currentUser) {
      setMembersLoading(false)
      return undefined
    }

    let cancelled = false
    setMembersLoading(true)
    setMembersError(null)
    membershipReadApi
      .getMembers(token ?? '')
      .then((result) => {
        if (cancelled) return
        const own = result.items.find((item) => item.email === currentUser.email)
        setRole(own?.role ?? null)
      })
      .catch((err) => {
        if (cancelled) return
        if (err instanceof SessionExpiredError) {
          useAuthStore.getState().clearSession()
        }
        setMembersError(err as Error)
      })
      .finally(() => {
        if (!cancelled) setMembersLoading(false)
      })
    return () => {
      cancelled = true
    }
  }, [token, userLoading, currentUser])

  return {
    role,
    userId: currentUser?.userId ?? null,
    isLoading: userLoading || membersLoading,
    error: userError ?? membersError,
  }
}
