import { useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/features/auth/store/authStore'

export function HomePage() {
  const clearSession = useAuthStore((state) => state.clearSession)
  const navigate = useNavigate()

  function handleLogout() {
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <main className="flex min-h-svh flex-col items-center justify-center gap-4 p-4">
      <h1 className="text-2xl font-semibold">Bem-vindo</h1>
      <p className="text-muted-foreground">Área protegida — placeholder pós-login.</p>
      <Button variant="outline" onClick={handleLogout}>
        Sair
      </Button>
    </main>
  )
}
