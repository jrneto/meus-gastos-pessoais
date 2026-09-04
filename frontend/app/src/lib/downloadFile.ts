/**
 * Aciona o download de um `Blob` no navegador, criando um link `<a
 * download>` temporário — mesma técnica usada em qualquer SPA sem
 * backend próprio pra servir arquivo (a resposta já chegou via
 * `fetch`, não há URL pública pra apontar um `<a href>` direto).
 * Utilitário genérico (sem regra de negócio) — pra ser reaproveitado
 * por qualquer feature futura que precise salvar um arquivo vindo da
 * API (ver FEAT-30, `features/settings/hooks/useExportTransactions.ts`).
 */
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
