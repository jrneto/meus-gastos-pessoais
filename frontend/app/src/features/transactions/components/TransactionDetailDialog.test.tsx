import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { ExpenseQueryItem } from '../api/transactionsApi'
import { ExpenseDetailDialog } from './TransactionDetailDialog'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

const expense: ExpenseQueryItem = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('ExpenseDetailDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('fica fechado quando expense é null', () => {
    render(
      <ExpenseDetailDialog expense={null} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    expect(screen.queryByText('Detalhe da despesa')).not.toBeInTheDocument()
  })

  it('aberto exibe valor, data, categoria e descrição', async () => {
    render(
      <ExpenseDetailDialog expense={expense} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    expect(screen.getByText('Detalhe da despesa')).toBeInTheDocument()
    expect(screen.getByText('R$ 45,90')).toBeInTheDocument()
    expect(screen.getByText('2025-06-15')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
    expect(screen.getByText('Almoço no restaurante')).toBeInTheDocument()
  })

  it('"Editar" chama onEdit com a despesa e fecha o popup', async () => {
    const user = userEvent.setup()
    const onEdit = vi.fn()
    const onOpenChange = vi.fn()

    render(
      <ExpenseDetailDialog expense={expense} onOpenChange={onOpenChange} onEdit={onEdit} onDelete={vi.fn()} />,
    )
    await user.click(screen.getByRole('button', { name: /^editar$/i }))

    expect(onEdit).toHaveBeenCalledWith(expense)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('"Excluir" chama onDelete com a despesa e fecha o popup', async () => {
    const user = userEvent.setup()
    const onDelete = vi.fn()
    const onOpenChange = vi.fn()

    render(
      <ExpenseDetailDialog expense={expense} onOpenChange={onOpenChange} onEdit={vi.fn()} onDelete={onDelete} />,
    )
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(onDelete).toHaveBeenCalledWith(expense)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('"Fechar" só fecha, sem chamar onEdit/onDelete', async () => {
    const user = userEvent.setup()
    const onEdit = vi.fn()
    const onDelete = vi.fn()
    const onOpenChange = vi.fn()

    render(
      <ExpenseDetailDialog expense={expense} onOpenChange={onOpenChange} onEdit={onEdit} onDelete={onDelete} />,
    )
    await user.click(screen.getByRole('button', { name: /^fechar$/i }))

    expect(onOpenChange).toHaveBeenCalledWith(false)
    expect(onEdit).not.toHaveBeenCalled()
    expect(onDelete).not.toHaveBeenCalled()
  })

  it('fecha ao pressionar Esc', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(
      <ExpenseDetailDialog expense={expense} onOpenChange={onOpenChange} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )
    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(
      <ExpenseDetailDialog expense={expense} onOpenChange={onOpenChange} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )
    await user.click(screen.getByRole('dialog').parentElement as HTMLElement)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
