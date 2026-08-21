import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { ExpenseDeleteDialog } from './ExpenseDeleteDialog'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

const expense: ExpenseQueryItem = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('ExpenseDeleteDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('fica fechado quando expense é null', () => {
    render(
      <ExpenseDeleteDialog expense={null} onOpenChange={vi.fn()} onDeleted={vi.fn()} />,
    )

    expect(screen.queryByText('Excluir despesa')).not.toBeInTheDocument()
  })

  it('aberto exibe a descrição da despesa', () => {
    render(
      <ExpenseDeleteDialog expense={expense} onOpenChange={vi.fn()} onDeleted={vi.fn()} />,
    )

    expect(screen.getByText('Excluir despesa')).toBeInTheDocument()
    expect(screen.getByText(/Almoço no restaurante/)).toBeInTheDocument()
  })

  it('cancelar não chama a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.delete(EXPENSE_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const onOpenChange = vi.fn()

    render(
      <ExpenseDeleteDialog expense={expense} onOpenChange={onOpenChange} onDeleted={vi.fn()} />,
    )

    await user.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(apiCalled).toBe(false)
    expect(onOpenChange).toHaveBeenCalledWith(false, expect.anything())
  })

  it('confirmar com sucesso chama a API e onDeleted', async () => {
    const user = userEvent.setup()
    server.use(http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 204 })))
    const onDeleted = vi.fn()

    render(
      <ExpenseDeleteDialog expense={expense} onOpenChange={vi.fn()} onDeleted={onDeleted} />,
    )

    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('exp-1'))
  })

  it('confirmar com 404 chama onDeleted (despesa já não existia)', async () => {
    const user = userEvent.setup()
    server.use(http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))
    const onDeleted = vi.fn()

    render(
      <ExpenseDeleteDialog expense={expense} onOpenChange={vi.fn()} onDeleted={onDeleted} />,
    )

    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('exp-1'))
  })

  it('confirmar com erro inesperado mantém o dialog aberto com alerta, sem chamar onDeleted', async () => {
    const user = userEvent.setup()
    server.use(http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 500 })))
    const onDeleted = vi.fn()

    render(
      <ExpenseDeleteDialog expense={expense} onOpenChange={vi.fn()} onDeleted={onDeleted} />,
    )

    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(await screen.findByText('Não foi possível excluir')).toBeInTheDocument()
    expect(screen.getByText('Excluir despesa')).toBeInTheDocument()
    expect(onDeleted).not.toHaveBeenCalled()
  })
})
