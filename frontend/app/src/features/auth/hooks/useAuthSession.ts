import { useAuthStore } from '../store/authStore'

interface UseAuthSessionResult {
  isAuthenticated: boolean
}

export function useAuthSession(): UseAuthSessionResult {
  const token = useAuthStore((state) => state.token)
  const expiresAt = useAuthStore((state) => state.expiresAt)

  const isAuthenticated = token !== null && expiresAt !== null && Date.now() < expiresAt

  return { isAuthenticated }
}
