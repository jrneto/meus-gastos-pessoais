import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { EditExpensePage } from './EditExpensePage'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/expenses/exp-1/edit']}>
      <Routes>
        <Route path="/expenses" element={<div>Expenses List Page</div>} />
        <Route path="/expenses/:id/edit" element={<EditExpensePage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('EditExpensePage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('exibe estado de carregamento e depois o formulário pré-preenchido', async () => {
    server.use(
      http.get(EXPENSE_URL, () =>
        HttpResponse.json({
          id: 'exp-1',
          description: 'Almoço no restaurante',
          amountInCents: 4590,
          category: 'Alimentacao',
          expenseDate: '2025-06-15',
          createdAt: '2025-06-15T12:00:00Z',
        }),
      ),
    )

    renderPage()

    expect(screen.getByText('Carregando...')).toBeInTheDocument()
    expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
  })

  it('404 ao carregar renderiza ExpenseNotFound', async () => {
    server.use(http.get(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

    renderPage()

    expect(await screen.findByText('Despesa não encontrada.')).toBeInTheDocument()
  })
})
