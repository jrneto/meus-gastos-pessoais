import { useNavigate } from 'react-router-dom'
import { AppVersion } from '@/components/AppVersion'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/features/auth/store/authStore'

export function SettingsPage() {
  const clearSession = useAuthStore((state) => state.clearSession)
  const navigate = useNavigate()

  function handleLogout() {
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <div className="flex flex-col gap-4 p-4">
      <h1 className="text-2xl font-semibold">Configurações</h1>
      <Button variant="outline" onClick={handleLogout}>
        Sair
      </Button>
      <AppVersion />
    </div>
  )
}
