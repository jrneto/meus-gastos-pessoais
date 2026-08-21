import { useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { LoginForm } from '@/features/auth/components/LoginForm'
import { useAuthSession } from '@/features/auth/hooks/useAuthSession'
import '@/styles/modernist/modernist.css'

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
    <div
      className="ds-modernist"
      style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '16px' }}
    >
      <div style={{ width: '100%', maxWidth: '360px', display: 'flex', flexDirection: 'column', gap: '20px' }}>
        <div>
          <div style={{ font: '800 32px var(--font-heading)', letterSpacing: '-0.02em' }}>
            jrn
            <span style={{ color: 'var(--color-accent)' }}>.</span>
          </div>
          <div
            style={{
              fontSize: '11px',
              letterSpacing: '.12em',
              textTransform: 'uppercase',
              opacity: 0.6,
            }}
          >
            expenses
          </div>
        </div>
        <LoginForm />
      </div>
    </div>
  )
}
