import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { downloadBlob } from '@/lib/downloadFile'
import { EXPORT_FILENAME, settingsApi } from '../api/settingsApi'
import { SessionExpiredError } from '../errors/settingsErrors'

interface UseExportTransactionsResult {
  exportCsv: () => Promise<void>
  isExporting: boolean
  error: Error | null
  success: boolean
}

/**
 * Exporta todas as transações da conta ativa em CSV (sem filtro, botão
 * único do design — decisão 3 da spec) e aciona o download no
 * navegador. `success` vira gatilho pro toast de confirmação em
 * `SettingsPage` (mesmo idioma de `useInviteMember`/`MembersPage`).
 */
export function useExportTransactions(): UseExportTransactionsResult {
  const [isExporting, setIsExporting] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function exportCsv(): Promise<void> {
    setIsExporting(true)
    setError(null)
    setSuccess(false)
    try {
      const blob = await settingsApi.exportTransactionsCsv(token ?? '')
      downloadBlob(blob, EXPORT_FILENAME)
      setSuccess(true)
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        useAuthStore.getState().clearSession()
      }
      setError(err as Error)
    } finally {
      setIsExporting(false)
    }
  }

  return { exportCsv, isExporting, error, success }
}
