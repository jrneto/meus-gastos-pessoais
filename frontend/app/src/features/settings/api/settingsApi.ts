import { httpClient } from '@/lib/httpClient'
import { NetworkError, SessionExpiredError, UnknownExportError } from '../errors/settingsErrors'

// Nome de arquivo fixo pelo contrato (backend FEAT-25,
// `Content-Disposition: attachment; filename="transacoes.csv"`), sem
// embutir filtro/data — nunca lido dinamicamente do header da resposta.
export const EXPORT_FILENAME = 'transacoes.csv'

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

function assertOk(response: Response): void {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownExportError()
  }
}

// Sempre sem query params — exporta todas as transações da conta
// ativa, mesmo botão único do design (decisão 3 da spec, sem seletor
// de filtro nesta tela).
async function exportTransactionsCsv(token: string): Promise<Blob> {
  const response = await safeFetch(() =>
    httpClient.get('/transactions/export', {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response)
  return response.blob()
}

export const settingsApi = {
  exportTransactionsCsv,
}
