import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { DashboardPage } from './DashboardPage'

const SUMMARY_URL = 'http://localhost:5049/summary'
const CATEGORIES_URL = 'http://localhost:5049/categories'
const TRANSACTIONS_URL = 'http://localhost:5049/transactions'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: 80000,
  createdAt: '2025-06-15T12:00:00Z',
}

const incomeCategory = {
  id: 'cat-2',
  nome: 'Salário',
  tipo: 'receita',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

function summaryResponse(overrides: Partial<Record<string, unknown>> = {}) {
  return {
    month: '2026-08',
    saldoCents: 394720,
    receitasCents: 520000,
    gastoCents: 125280,
    orcamentoTotalCents: 299000,
    restanteCents: 173720,
    porCategoria: [
      { categoryId: 'cat-1', nome: 'Alimentação', gastoCents: 30670, orcamentoMensalCents: 80000 },
    ],
    ultimosLancamentos: [
      {
        id: 'tx-1',
        description: 'Supermercado',
        amountInCents: 18790,
        categoryId: 'cat-1',
        tipo: 'despesa',
        date: '2026-08-24',
        createdByUserId: 'user-1',
        createdByLabel: 'Você',
        createdAt: '2026-08-24T18:12:00Z',
      },
    ],
    ...overrides,
  }
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/transactions" element={<div>Transações Content</div>} />
        <Route path="/categories" element={<div>Categorias Content</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date(2026, 7, 30))
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(
      http.get(SUMMARY_URL, () => HttpResponse.json(summaryResponse())),
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category, incomeCategory] })),
    )
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('renderiza os cinco cartões com os valores do resumo', async () => {
    renderPage()

    expect(await screen.findByText('R$ 3.947,20')).toBeInTheDocument()
    expect(screen.getByText('R$ 5.200,00')).toBeInTheDocument()
    expect(screen.getByText('R$ 1.252,80')).toBeInTheDocument()
    expect(screen.getByText('R$ 2.990,00')).toBeInTheDocument()
    expect(screen.getByText('R$ 1.737,20')).toBeInTheDocument()
  })

  it('mostra o título "Resumo" e o rótulo do mês corrente', async () => {
    renderPage()

    expect(screen.getByRole('heading', { name: 'Resumo' })).toBeInTheDocument()
    expect(await screen.findByText('Agosto de 2026')).toBeInTheDocument()
  })

  it('clicar em "+ Nova despesa" abre o popup fixo em despesa', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('R$ 3.947,20')

    await user.click(screen.getByRole('button', { name: /nova despesa/i }))

    expect(await screen.findByText('Nova despesa')).toBeInTheDocument()
  })

  it('clicar em "+ Nova receita" abre o popup fixo em receita', async () => {
    const user = userEvent.setup()
    renderPage()
    await screen.findByText('R$ 3.947,20')

    await user.click(screen.getByRole('button', { name: /nova receita/i }))

    expect(await screen.findByText('Nova receita')).toBeInTheDocument()
  })

  it('salvar uma nova transação refaz a busca do resumo', async () => {
    const user = userEvent.setup()
    let summaryRequestCount = 0
    server.use(
      http.get(SUMMARY_URL, () => {
        summaryRequestCount += 1
        return HttpResponse.json(summaryResponse())
      }),
      http.post(TRANSACTIONS_URL, () =>
        HttpResponse.json({
          id: 'tx-2',
          description: 'Almoço',
          amountInCents: 3000,
          categoryId: 'cat-1',
          tipo: 'despesa',
          date: '2026-08-20',
          createdByUserId: 'user-1',
          createdByLabel: 'Você',
          createdAt: '2026-08-20T12:00:00Z',
        }),
      ),
    )

    renderPage()
    await screen.findByText('R$ 3.947,20')
    expect(summaryRequestCount).toBe(1)

    await user.click(screen.getByRole('button', { name: /nova despesa/i }))
    await screen.findByLabelText('Descrição')
    await user.type(screen.getByLabelText('Descrição'), 'Almoço')
    await user.type(screen.getByLabelText('Valor'), '30,00')
    await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-1')
    fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2026-08-20' } })
    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    await waitFor(() => expect(summaryRequestCount).toBe(2))
  })

  it('"Ver todas" de últimos lançamentos aponta para /transactions filtrado pelo mês', async () => {
    renderPage()
    await screen.findByText('R$ 3.947,20')

    const link = screen.getByRole('link', { name: /ver todas →/i })
    expect(link).toHaveAttribute('href', '/transactions?yearMonth=2026-08')
  })

  it('"Ver todas" de categorias aponta para /categories', async () => {
    renderPage()
    await screen.findByText('R$ 3.947,20')

    const link = screen.getByRole('link', { name: /ver todas \(1\) →/i })
    expect(link).toHaveAttribute('href', '/categories')
  })

  it('estado vazio quando não há categoria com orçamento nem transação no mês', async () => {
    server.use(
      http.get(SUMMARY_URL, () =>
        HttpResponse.json(summaryResponse({ porCategoria: [], ultimosLancamentos: [] })),
      ),
    )

    renderPage()

    expect(await screen.findByText('Nenhuma categoria com orçamento definido ainda.')).toBeInTheDocument()
    expect(screen.getByText('Nenhuma transação neste mês.')).toBeInTheDocument()
  })

  it('erro de sessão expirada limpa a sessão e exibe mensagem', async () => {
    server.use(http.get(SUMMARY_URL, () => new HttpResponse(null, { status: 401 })))

    renderPage()

    expect(await screen.findByText('Não foi possível carregar o resumo')).toBeInTheDocument()
    expect(screen.getByText('Sua sessão expirou. Faça login novamente.')).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })
})
