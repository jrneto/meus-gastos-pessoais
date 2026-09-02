import { httpClient } from '@/lib/httpClient'
import { NetworkError, SessionExpiredError, UnknownReportsError } from '../errors/reportsErrors'

export type ReportPeriod = 'week' | 'month' | 'year'

export interface ReportCategoryItem {
  categoryId: string
  nome: string
  gastoCents: number
}

export interface ReportTopCategory {
  categoryId: string
  nome: string
  gastoCents: number
  percentualOrcamento: number | null
}

export interface ReportsResponse {
  period: ReportPeriod
  startDate: string
  endDate: string
  totalCents: number
  variacaoPercentual: number | null
  porCategoria: ReportCategoryItem[]
  maiorGasto: ReportTopCategory | null
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
    throw new UnknownReportsError()
  }
}

// `period` vem de um seletor fechado (3 valores) e `date` é sempre
// calculada pelo client (data corrente, sem seletor nesta feature) —
// não há checagem dedicada de 400 (validation-error), que cairia no
// `UnknownReportsError` genérico se acontecer.
async function getReports(token: string, period: ReportPeriod, date: string): Promise<ReportsResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/reports?period=${period}&date=${date}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response)
  return response.json() as Promise<ReportsResponse>
}

export const reportsApi = {
  getReports,
}
