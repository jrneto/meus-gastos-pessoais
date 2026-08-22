import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ExpensesListPage } from './ExpensesListPage'

const EXPENSES_URL = 'http://localhost:5049/expenses'

describe('ExpensesListPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(EXPENSES_URL, () => HttpResponse.json({ items: [], nextCursor: null })))
  })

  it('exibe o título "Transações" (FEAT-16)', () => {
    render(
      <MemoryRouter>
        <ExpensesListPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('heading', { name: 'Transações' })).toBeInTheDocument()
  })

  it('clicar em "+ Nova despesa" abre o popup de cadastro (FEAT-17)', async () => {
    const user = userEvent.setup()
    server.use(http.get('http://localhost:5049/categories', () => HttpResponse.json({ items: [] })))

    render(
      <MemoryRouter>
        <ExpensesListPage />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('button', { name: /nova despesa/i }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })

  it('clicar no ícone de editar de uma linha abre o popup pré-preenchido (FEAT-18)', async () => {
    const user = userEvent.setup()
    const category = {
      id: 'cat-1',
      nome: 'Alimentação',
      cor: '#F97316',
      icone: 'utensils',
      createdAt: '2025-06-15T12:00:00Z',
    }
    const item = {
      id: 'exp-1',
      description: 'Almoço no restaurante',
      amountInCents: 4590,
      categoryId: 'cat-1',
      expenseDate: '2025-06-15',
      createdAt: '2025-06-15T12:00:00Z',
    }
    server.use(
      http.get(EXPENSES_URL, () => HttpResponse.json({ items: [item], nextCursor: null })),
      http.get('http://localhost:5049/categories', () => HttpResponse.json({ items: [category] })),
      http.get('http://localhost:5049/expenses/exp-1', () => HttpResponse.json(item)),
    )

    render(
      <MemoryRouter>
        <ExpensesListPage />
      </MemoryRouter>,
    )

    await user.click(await screen.findByRole('button', { name: /editar despesa/i }))

    expect(await screen.findByText('Editar despesa')).toBeInTheDocument()
    expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
  })
})
