import { useNavigate } from 'react-router-dom'
import { useLogout } from '@/features/auth/hooks/useLogout'
import '@/styles/modernist/modernist.css'

interface AccountFooterProps {
  // Colapsa pro mesmo modo ícone-só de `DesktopSidebar`/`NavItemRow` —
  // sem equivalente no protótipo (que não modela sidebar colapsável),
  // mesmo racional de fallback já usado nos itens de menu.
  collapsed?: boolean
  // `NavMoreSheet` fecha o painel antes de navegar pro login, pra não
  // deixar o modal aberto sobre a tela de login por um instante.
  onBeforeLogout?: () => void
}

const avatarStyle: React.CSSProperties = {
  width: '30px',
  height: '30px',
  border: '2px solid var(--color-text)',
  display: 'flex',
  alignItems: 'center',
  justifyContent: 'center',
  font: '800 11px var(--font-heading)',
  flexShrink: 0,
}

// Rodapé "Sua conta / Sair" — protótipo web (`.dc.html`, rodapé da
// sidebar, fora do bloco de conteúdo `isSet`). Compartilhado entre
// `DesktopSidebar` e `NavMoreSheet` (mobile), já que nenhuma das duas
// telas de conteúdo (`SettingsPage`) mais tem seu próprio botão "Sair"
// (FEAT-30). "VC" é abreviação fixa de "Você" (mesma convenção já
// usada em `createdByLabel` pro autor da própria conta), não iniciais
// calculadas a partir do nome real do usuário — evita uma chamada de
// API a mais rodando sempre que a casca de navegação monta.
export function AccountFooter({ collapsed = false, onBeforeLogout }: AccountFooterProps) {
  const { logout } = useLogout()
  const navigate = useNavigate()

  async function handleLogout() {
    onBeforeLogout?.()
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <div
      className="ds-modernist"
      style={{
        marginTop: '16px',
        paddingTop: '16px',
        borderTop: '2px solid var(--color-divider)',
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
      }}
    >
      {collapsed ? (
        <button
          type="button"
          onClick={handleLogout}
          aria-label="Sair"
          title="Sair"
          style={{ background: 'none', border: 'none', padding: 0, cursor: 'pointer' }}
        >
          <div style={avatarStyle}>VC</div>
        </button>
      ) : (
        <>
          <div style={avatarStyle}>VC</div>
          <div style={{ flex: 1, minWidth: 0 }}>
            <div
              style={{
                fontSize: '12.5px',
                fontWeight: 600,
                whiteSpace: 'nowrap',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
              }}
            >
              Sua conta
            </div>
            <button
              type="button"
              onClick={handleLogout}
              style={{
                fontSize: '11px',
                color: 'var(--color-accent-700)',
                cursor: 'pointer',
                background: 'none',
                border: 'none',
                padding: 0,
              }}
            >
              Sair
            </button>
          </div>
        </>
      )}
    </div>
  )
}
