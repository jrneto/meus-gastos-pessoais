import { useEffect, useState } from 'react'
import { AppVersion } from '@/components/AppVersion'
import { Toast } from '@/components/Toast'
import { useExportTransactions } from '@/features/settings/hooks/useExportTransactions'
import '@/styles/modernist/modernist.css'

// Tela "Ajustes" (FEAT-30) — migrada de shadcn/ui/Tailwind pro
// Modernist. Só o título e a exportação CSV (`18-ajustes.png`); as
// linhas "Moeda"/"Notificações" do protótipo ficam de fora (sem
// suporte de backend hoje, decisão 1 da spec). O botão "Sair" que
// existia aqui migrou pro rodapé "Sua conta" da sidebar/NavMoreSheet
// (`components/nav/AccountFooter.tsx`, decisão 2 da spec).
export function SettingsPage() {
  const { exportCsv, isExporting, error, success } = useExportTransactions()
  const [toastMessage, setToastMessage] = useState<string | null>(null)

  useEffect(() => {
    if (success) {
      setToastMessage('Transações exportadas.')
    }
  }, [success])

  return (
    <div
      className="ds-modernist"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-6)',
        maxWidth: '520px',
        margin: '0 auto',
        padding: '40px 40px 60px',
        boxSizing: 'border-box',
      }}
    >
      <h1 style={{ fontSize: '30px', margin: 0 }}>Ajustes</h1>

      {error && (
        <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '13px' }}>
          {error.message}
        </p>
      )}

      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '14px 0',
          borderBottom: '1px solid var(--color-divider)',
        }}
      >
        <span style={{ fontSize: '14px' }}>Exportar dados</span>
        <button type="button" className="btn btn-secondary" disabled={isExporting} onClick={exportCsv}>
          {isExporting ? 'Exportando...' : 'Exportar CSV'}
        </button>
      </div>

      <AppVersion />

      <Toast message={toastMessage} onDismiss={() => setToastMessage(null)} />
    </div>
  )
}
