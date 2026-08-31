import '@/styles/modernist/modernist.css'

interface ProcessingOverlayProps {
  label: string
}

// Overlay genérico de processamento de tela cheia (FEAT-28) — resolve
// o débito técnico "Overlay de processamento de tela cheia" do
// backlog, adiado nas FEAT-24/FEAT-26. `position: absolute; inset: 0`
// precisa que o ancestral direto tenha `position: relative`
// (responsabilidade de quem usa o componente, ex.: só o `.dialog` que
// exibe o overlay ganha isso inline — outros dialogs continuam sem).
export function ProcessingOverlay({ label }: ProcessingOverlayProps) {
  return (
    <div
      className="ds-modernist"
      role="status"
      style={{
        position: 'absolute',
        inset: 0,
        zIndex: 5,
        background: 'color-mix(in oklch, var(--color-bg) 86%, transparent)',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        gap: '16px',
      }}
    >
      <div className="je-spin" />
      <div
        style={{
          fontFamily: 'var(--font-heading)',
          fontSize: '12px',
          fontWeight: 800,
          letterSpacing: '0.1em',
          textTransform: 'uppercase',
        }}
      >
        {label}
      </div>
      <div style={{ width: '200px', height: '4px', background: 'var(--color-neutral-300)', overflow: 'hidden' }}>
        <div className="je-indet" />
      </div>
    </div>
  )
}
