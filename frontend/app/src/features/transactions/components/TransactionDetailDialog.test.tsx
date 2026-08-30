import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { TransactionQueryItem } from '../api/transactionsApi'
import { TransactionDetailDialog } from './TransactionDetailDialog'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const transaction: TransactionQueryItem = {
  id: 'tx-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  tipo: 'despesa',
  date: '2025-06-15',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('TransactionDetailDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('fica fechado quando transaction é null', () => {
    render(
      <TransactionDetailDialog transaction={null} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    expect(screen.queryByText('Detalhe da despesa')).not.toBeInTheDocument()
  })

  it('aberto exibe valor, data, categoria e descrição', async () => {
    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    expect(screen.getByText('Detalhe da despesa')).toBeInTheDocument()
    expect(screen.getByText('- R$ 45,90')).toBeInTheDocument()
    expect(screen.getByText('2025-06-15')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
    expect(screen.getByText('Almoço no restaurante')).toBeInTheDocument()
  })

  it('mostra "Lançado por: Você" quando o autor é o próprio usuário logado', () => {
    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    expect(screen.getByText('Lançado por')).toBeInTheDocument()
    expect(screen.getByText('Você')).toBeInTheDocument()
  })

  it('mostra o e-mail do autor quando a transação foi lançada por outro membro', () => {
    const otherMemberTransaction: TransactionQueryItem = {
      ...transaction,
      createdByUserId: 'user-2',
      createdByLabel: 'outro@exemplo.com',
    }

    render(
      <TransactionDetailDialog
        transaction={otherMemberTransaction}
        onOpenChange={vi.fn()}
        onEdit={vi.fn()}
        onDelete={vi.fn()}
      />,
    )

    expect(screen.getByText('Lançado por')).toBeInTheDocument()
    expect(screen.getByText('outro@exemplo.com')).toBeInTheDocument()
  })

  it('receita mostra título "Detalhe da receita", cor positive e sinal "+" no valor (FEAT-24)', async () => {
    const incomeTransaction: TransactionQueryItem = {
      ...transaction,
      id: 'tx-2',
      tipo: 'receita',
      amountInCents: 500000,
    }

    render(
      <TransactionDetailDialog transaction={incomeTransaction} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    expect(screen.getByText('Detalhe da receita')).toBeInTheDocument()
    const amount = screen.getByText('+ R$ 5.000,00')
    expect(amount).toBeInTheDocument()
    expect(amount.style.color).toBe('var(--color-positive-700)')
  })

  it('despesa mostra cor accent no valor, sem regressão (FEAT-24)', () => {
    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={vi.fn()} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )

    const amount = screen.getByText('- R$ 45,90')
    expect(amount.style.color).toBe('var(--color-accent-700)')
  })

  it('"Editar" chama onEdit com a transação e fecha o popup', async () => {
    const user = userEvent.setup()
    const onEdit = vi.fn()
    const onOpenChange = vi.fn()

    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={onOpenChange} onEdit={onEdit} onDelete={vi.fn()} />,
    )
    await user.click(screen.getByRole('button', { name: /^editar$/i }))

    expect(onEdit).toHaveBeenCalledWith(transaction)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('"Excluir" chama onDelete com a transação e fecha o popup', async () => {
    const user = userEvent.setup()
    const onDelete = vi.fn()
    const onOpenChange = vi.fn()

    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={onOpenChange} onEdit={vi.fn()} onDelete={onDelete} />,
    )
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(onDelete).toHaveBeenCalledWith(transaction)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('"Fechar" só fecha, sem chamar onEdit/onDelete', async () => {
    const user = userEvent.setup()
    const onEdit = vi.fn()
    const onDelete = vi.fn()
    const onOpenChange = vi.fn()

    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={onOpenChange} onEdit={onEdit} onDelete={onDelete} />,
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
      <TransactionDetailDialog transaction={transaction} onOpenChange={onOpenChange} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )
    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(
      <TransactionDetailDialog transaction={transaction} onOpenChange={onOpenChange} onEdit={vi.fn()} onDelete={vi.fn()} />,
    )
    await user.click(screen.getByRole('dialog').parentElement as HTMLElement)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
