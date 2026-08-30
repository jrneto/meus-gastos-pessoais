import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, UnknownSummaryError } from '../errors/summaryErrors'
import { useSummary } from './useSummary'

const SUMMARY_URL = 'http://localhost:5049/summary'

function summaryResponse(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    month: '2026-08',
    saldoCents: 394720,
    receitasCents: 520000,
    gastoCents: 125280,
    orcamentoTotalCents: 299000,
    restanteCents: 173720,
    porCategoria: [],
    ultimosLancamentos: [],
    ...overrides,
  }
}

describe('useSummary', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega o resumo do mês informado ao montar', async () => {
    server.use(http.get(SUMMARY_URL, () => HttpResponse.json(summaryResponse())))

    const { result } = renderHook(() => useSummary('2026-08'))

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.data).toEqual(summaryResponse())
    expect(result.current.error).toBeNull()
  })

  it('refetch refaz a busca do mesmo mês', async () => {
    let requestCount = 0
    server.use(
      http.get(SUMMARY_URL, () => {
        requestCount += 1
        return HttpResponse.json(summaryResponse({ saldoCents: requestCount }))
      }),
    )

    const { result } = renderHook(() => useSummary('2026-08'))
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.data?.saldoCents).toBe(1)

    act(() => {
      result.current.refetch()
    })

    await waitFor(() => expect(result.current.data?.saldoCents).toBe(2))
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(SUMMARY_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useSummary('2026-08'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(SUMMARY_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useSummary('2026-08'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })

  it('em caso de erro inesperado, expõe UnknownSummaryError', async () => {
    server.use(http.get(SUMMARY_URL, () => new HttpResponse(null, { status: 500 })))

    const { result } = renderHook(() => useSummary('2026-08'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(UnknownSummaryError))
  })
})
