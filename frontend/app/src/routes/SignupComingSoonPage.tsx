import { Link } from 'react-router-dom'
import '@/styles/modernist/modernist.css'

// Destino fake do modo "Criar conta" da tela de Login (FEAT-14). Não há
// endpoint de cadastro no backend hoje — esta página só comunica isso e
// devolve o visitante ao login. Ver spec.md, "Fora do escopo".
export function SignupComingSoonPage() {
  return (
    <div
      className="ds-modernist"
      style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '16px' }}
    >
      <div style={{ width: '100%', maxWidth: '360px', display: 'flex', flexDirection: 'column', gap: '16px' }}>
        <h1>Cadastro em breve</h1>
        <p>
          Ainda não é possível criar uma conta por aqui. Estamos preparando esse fluxo — por
          enquanto, entre com uma conta já existente.
        </p>
        <Link to="/login" className="btn btn-secondary btn-block">
          Voltar para o login
        </Link>
      </div>
    </div>
  )
}
