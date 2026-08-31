import { httpClient } from '@/lib/httpClient'
import { NetworkError, SessionExpiredError, UnknownSummaryError } from '../errors/summaryErrors'

export interface CategorySummaryItem {
  categoryId: string
  nome: string
  gastoCents: number
  orcamentoMensalCents: number
}

export interface SummaryTransactionItem {
  id: string
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
  createdByUserId: string
  createdByLabel: string
  createdAt: string
}

export interface SummaryResponse {
  month: string
  saldoCents: number
  receitasCents: number
  gastoCents: number
  orcamentoTotalCents: number
  restanteCents: number
  porCategoria: CategorySummaryItem[]
  ultimosLancamentos: SummaryTransactionItem[]
}

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
    throw new UnknownSummaryError()
  }
}

// `month` sempre calculado pelo client (mês corrente, sem navegação
// nesta feature) — não há checagem dedicada de 400 (validation-error),
// que cairia no `UnknownSummaryError` genérico se acontecer.
async function getSummary(token: string, month: string): Promise<SummaryResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/summary?month=${month}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response)
  return response.json() as Promise<SummaryResponse>
}

export const summaryApi = {
  getSummary,
}
