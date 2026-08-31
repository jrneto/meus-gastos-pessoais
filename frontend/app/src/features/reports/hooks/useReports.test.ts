import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { ReportPeriod } from '../api/reportsApi'
import { NetworkError, SessionExpiredError, UnknownReportsError } from '../errors/reportsErrors'
import { useReports } from './useReports'

const REPORTS_URL = 'http://localhost:5049/reports'

function reportsResponse(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    period: 'month',
    startDate: '2026-08-01',
    endDate: '2026-08-31',
    totalCents: 138120,
    variacaoPercentual: 12.0,
    porCategoria: [],
    maiorGasto: null,
    ...overrides,
  }
}

describe('useReports', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega o relatório do period/date informados ao montar', async () => {
    server.use(http.get(REPORTS_URL, () => HttpResponse.json(reportsResponse())))

    const { result } = renderHook(() => useReports('month', '2026-08-15'))

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.data).toEqual(reportsResponse())
    expect(result.current.error).toBeNull()
  })

  it('refaz a busca quando period muda', async () => {
    let lastPeriod: string | null = null
    server.use(
      http.get(REPORTS_URL, ({ request }) => {
        lastPeriod = new URL(request.url).searchParams.get('period')
        return HttpResponse.json(reportsResponse({ period: lastPeriod }))
      }),
    )

    const { result, rerender } = renderHook(({ period }: { period: ReportPeriod }) => useReports(period, '2026-08-15'), {
      initialProps: { period: 'month' },
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(lastPeriod).toBe('month')

    rerender({ period: 'week' })

    await waitFor(() => expect(lastPeriod).toBe('week'))
  })

  it('refaz a busca quando date muda', async () => {
    let lastDate: string | null = null
    server.use(
      http.get(REPORTS_URL, ({ request }) => {
        lastDate = new URL(request.url).searchParams.get('date')
        return HttpResponse.json(reportsResponse())
      }),
    )

    const { rerender } = renderHook(({ date }) => useReports('month', date), {
      initialProps: { date: '2026-08-15' },
    })
    await waitFor(() => expect(lastDate).toBe('2026-08-15'))

    rerender({ date: '2026-08-16' })

    await waitFor(() => expect(lastDate).toBe('2026-08-16'))
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(REPORTS_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useReports('month', '2026-08-15'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(REPORTS_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useReports('month', '2026-08-15'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })

  it('em caso de erro inesperado, expõe UnknownReportsError', async () => {
    server.use(http.get(REPORTS_URL, () => new HttpResponse(null, { status: 500 })))

    const { result } = renderHook(() => useReports('month', '2026-08-15'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(UnknownReportsError))
  })
})
