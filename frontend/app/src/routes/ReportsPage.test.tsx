import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ReportsPage } from './ReportsPage'

const REPORTS_URL = 'http://localhost:5049/reports'

function reportsResponse(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    period: 'month',
    startDate: '2026-08-01',
    endDate: '2026-08-31',
    totalCents: 138120,
    variacaoPercentual: 12.0,
    porCategoria: [
      { categoryId: 'cat-1', nome: 'Alimentação', gastoCents: 43510 },
      { categoryId: 'cat-2', nome: 'Moradia', gastoCents: 31020 },
    ],
    maiorGasto: {
      categoryId: 'cat-1',
      nome: 'Alimentação',
      gastoCents: 43510,
      percentualOrcamento: 54.4,
    },
    ...overrides,
  }
}

describe('ReportsPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date(2026, 7, 30))
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('busca com period=month por padrão ao carregar', async () => {
    let lastPeriod: string | null = null
    server.use(
      http.get(REPORTS_URL, ({ request }) => {
        lastPeriod = new URL(request.url).searchParams.get('period')
        return HttpResponse.json(reportsResponse())
      }),
    )

    render(<ReportsPage />)

    expect(await screen.findByText('Moradia')).toBeInTheDocument()
    expect(lastPeriod).toBe('month')
  })

  it('mostra o total, a comparação e o maior gasto', async () => {
    server.use(http.get(REPORTS_URL, () => HttpResponse.json(reportsResponse())))

    render(<ReportsPage />)

    expect(await screen.findByText('R$ 1.381,20')).toBeInTheDocument()
    expect(screen.getByText('+12% vs mês passado')).toBeInTheDocument()
    expect(screen.getByText('R$ 435,10 · 54,4% do orçamento')).toBeInTheDocument()
  })

  it('trocar para "Semana" refaz a busca com period=week e a mesma data', async () => {
    const user = userEvent.setup()
    const requestedPeriods: string[] = []
    const requestedDates: string[] = []
    server.use(
      http.get(REPORTS_URL, ({ request }) => {
        const url = new URL(request.url)
        requestedPeriods.push(url.searchParams.get('period') ?? '')
        requestedDates.push(url.searchParams.get('date') ?? '')
        return HttpResponse.json(reportsResponse())
      }),
    )

    render(<ReportsPage />)
    await screen.findByText('Moradia')

    await user.click(screen.getByLabelText('Semana'))

    expect(requestedPeriods).toEqual(['month', 'week'])
    expect(requestedDates[0]).toBe(requestedDates[1])
  })

  it('trocar para "Ano" refaz a busca com period=year', async () => {
    const user = userEvent.setup()
    const requestedPeriods: string[] = []
    server.use(
      http.get(REPORTS_URL, ({ request }) => {
        requestedPeriods.push(new URL(request.url).searchParams.get('period') ?? '')
        return HttpResponse.json(reportsResponse())
      }),
    )

    render(<ReportsPage />)
    await screen.findByText('Moradia')

    await user.click(screen.getByLabelText('Ano'))

    expect(requestedPeriods).toEqual(['month', 'year'])
  })

  it('estado vazio (sem despesa no período) não quebra a tela', async () => {
    server.use(
      http.get(REPORTS_URL, () =>
        HttpResponse.json(
          reportsResponse({ totalCents: 0, variacaoPercentual: 0, porCategoria: [], maiorGasto: null }),
        ),
      ),
    )

    render(<ReportsPage />)

    expect(await screen.findByText('Nenhuma despesa neste período.')).toBeInTheDocument()
    expect(screen.getByText('Nenhum gasto registrado')).toBeInTheDocument()
    expect(screen.getByText('R$ 0,00')).toBeInTheDocument()
  })

  it('erro de sessão expirada limpa a sessão e exibe mensagem', async () => {
    server.use(http.get(REPORTS_URL, () => new HttpResponse(null, { status: 401 })))

    render(<ReportsPage />)

    expect(await screen.findByText('Não foi possível carregar o relatório')).toBeInTheDocument()
    expect(screen.getByText('Sua sessão expirou. Faça login novamente.')).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })
})
