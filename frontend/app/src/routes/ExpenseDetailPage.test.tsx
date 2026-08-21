import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ExpenseDetailPage } from './ExpenseDetailPage'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

const expenseDetail = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/expenses/exp-1']}>
      <Routes>
        <Route path="/expenses" element={<div>Expenses List Page</div>} />
        <Route path="/expenses/:id" element={<ExpenseDetailPage />} />
        <Route path="/expenses/:id/edit" element={<div>Edit Page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('ExpenseDetailPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('exibe estado de carregamento e depois os detalhes da despesa, incluindo id e data de criação', async () => {
    server.use(http.get(EXPENSE_URL, () => HttpResponse.json(expenseDetail)))

    renderPage()

    expect(screen.getByText('Carregando...')).toBeInTheDocument()
    expect(await screen.findByText('Almoço no restaurante')).toBeInTheDocument()
    expect(screen.getByText('R$ 45,90')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
    expect(screen.getByText('exp-1')).toBeInTheDocument()
    expect(screen.getByText(new Date(expenseDetail.createdAt).toLocaleString('pt-BR'))).toBeInTheDocument()
  })

  it('exibe os botões Editar e Excluir', async () => {
    server.use(http.get(EXPENSE_URL, () => HttpResponse.json(expenseDetail)))

    renderPage()

    expect(await screen.findByRole('link', { name: /editar/i })).toHaveAttribute(
      'href',
      '/expenses/exp-1/edit',
    )
    expect(screen.getByRole('button', { name: /^excluir$/i })).toBeInTheDocument()
  })

  it('404 ao carregar renderiza ExpenseNotFound', async () => {
    server.use(http.get(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

    renderPage()

    expect(await screen.findByText('Despesa não encontrada.')).toBeInTheDocument()
  })

  it('excluir com sucesso navega para /expenses', async () => {
    const user = userEvent.setup()
    server.use(
      http.get(EXPENSE_URL, () => HttpResponse.json(expenseDetail)),
      http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 204 })),
    )

    renderPage()

    await user.click(await screen.findByRole('button', { name: /^excluir$/i }))

    const confirmButtons = await screen.findAllByRole('button', { name: /^excluir$/i })
    await user.click(confirmButtons[confirmButtons.length - 1])

    await waitFor(() => expect(screen.getByText('Expenses List Page')).toBeInTheDocument())
  })
})
