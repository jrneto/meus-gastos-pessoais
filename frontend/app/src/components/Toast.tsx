import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'

interface ToastProps {
  message: string | null
  onDismiss: () => void
}

const AUTO_DISMISS_MS = 3200

// Toast genérico (FEAT-28) — resolve o débito técnico "Componente de
// toast genérico" do backlog, adiado nas FEAT-24/FEAT-26. `position:
// fixed` (não `absolute` como no `.dc.html`): lá funciona porque o
// protótipo tem um container `relative` cobrindo a tela inteira,
// artefato do harness de demonstração — `fixed` obtém o mesmo
// resultado visual (canto inferior direito da viewport) num app real
// com roteamento client-side.
export function Toast({ message, onDismiss }: ToastProps) {
  useEffect(() => {
    if (message === null) {
      return undefined
    }
    const timer = setTimeout(onDismiss, AUTO_DISMISS_MS)
    return () => clearTimeout(timer)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [message])

  if (message === null) {
    return null
  }

  return (
    <div
      className="ds-modernist je-toast"
      role="status"
      style={{
        position: 'fixed',
        bottom: '20px',
        right: '20px',
        zIndex: 60,
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
        background: 'var(--color-text)',
        color: 'var(--color-bg)',
        padding: '14px 18px',
        boxShadow: 'var(--shadow-lg)',
        maxWidth: '340px',
      }}
    >
      <svg width="18" height="18" viewBox="0 0 24 24" fill="none" style={{ flex: 'none' }}>
        <polyline
          points="20 6 9 17 4 12"
          stroke="currentColor"
          strokeWidth="2.5"
          fill="none"
          strokeLinecap="round"
          strokeLinejoin="round"
        />
      </svg>
      <span style={{ fontSize: '13px', fontWeight: 600 }}>{message}</span>
    </div>
  )
}
