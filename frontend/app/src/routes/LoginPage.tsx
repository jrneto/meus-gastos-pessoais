import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { LoginForm } from '@/features/auth/components/LoginForm'
import { useAuthSession } from '@/features/auth/hooks/useAuthSession'

export function LoginPage() {
  const { isAuthenticated } = useAuthSession()
  const navigate = useNavigate()

  // Redireciona reativamente quando o login popula a authStore — não
  // depende de callback/retorno do LoginForm, evita acoplamento entre
  // o formulário e a navegação.
  useEffect(() => {
    if (isAuthenticated) {
      navigate('/', { replace: true })
    }
  }, [isAuthenticated, navigate])

  return (
    <main className="flex min-h-svh items-center justify-center p-4">
      <div className="flex w-full max-w-sm flex-col gap-6">
        <h1 className="text-2xl font-semibold">Entrar</h1>
        <LoginForm />
      </div>
    </main>
  )
}