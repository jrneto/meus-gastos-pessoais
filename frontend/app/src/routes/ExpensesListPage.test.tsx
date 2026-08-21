import { render, screen } from '@testing-library/react'
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

  it('exibe um link "+ Nova despesa" para /expenses/new (FEAT-15)', () => {
    render(
      <MemoryRouter>
        <ExpensesListPage />
      </MemoryRouter>,
    )

    const link = screen.getByRole('link', { name: /nova despesa/i })
    expect(link).toHaveAttribute('href', '/expenses/new')
  })
})
