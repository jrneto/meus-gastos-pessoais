import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { TransactionQueryItem } from '../api/transactionsApi'
import { TransactionDeleteDialog } from './TransactionDeleteDialog'

const TRANSACTION_URL = 'http://localhost:5049/transactions/tx-1'

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

describe('TransactionDeleteDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('fica fechado quando transaction é null', () => {
    render(
      <TransactionDeleteDialog transaction={null} onOpenChange={vi.fn()} onDeleted={vi.fn()} />,
    )

    expect(screen.queryByText('Excluir despesa')).not.toBeInTheDocument()
  })

  it('aberto exibe a descrição da transação', () => {
    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={vi.fn()} onDeleted={vi.fn()} />,
    )

    expect(screen.getByText('Excluir despesa')).toBeInTheDocument()
    expect(screen.getByText(/Almoço no restaurante/)).toBeInTheDocument()
  })

  it('receita mostra "Excluir receita" no título (FEAT-24)', () => {
    const incomeTransaction: TransactionQueryItem = { ...transaction, tipo: 'receita' }

    render(
      <TransactionDeleteDialog transaction={incomeTransaction} onOpenChange={vi.fn()} onDeleted={vi.fn()} />,
    )

    expect(screen.getByText('Excluir receita')).toBeInTheDocument()
  })

  it('cancelar não chama a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.delete(TRANSACTION_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const onOpenChange = vi.fn()

    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={onOpenChange} onDeleted={vi.fn()} />,
    )

    await user.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(apiCalled).toBe(false)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('confirmar com sucesso chama a API e onDeleted', async () => {
    const user = userEvent.setup()
    server.use(http.delete(TRANSACTION_URL, () => new HttpResponse(null, { status: 204 })))
    const onDeleted = vi.fn()

    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={vi.fn()} onDeleted={onDeleted} />,
    )

    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('tx-1'))
  })

  it('confirmar com 404 chama onDeleted (transação já não existia)', async () => {
    const user = userEvent.setup()
    server.use(http.delete(TRANSACTION_URL, () => new HttpResponse(null, { status: 404 })))
    const onDeleted = vi.fn()

    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={vi.fn()} onDeleted={onDeleted} />,
    )

    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('tx-1'))
  })

  it('fecha ao pressionar Esc', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={onOpenChange} onDeleted={vi.fn()} />,
    )

    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={onOpenChange} onDeleted={vi.fn()} />,
    )

    await user.click(screen.getByRole('alertdialog').parentElement as HTMLElement)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('confirmar com erro inesperado mantém o dialog aberto com alerta, sem chamar onDeleted', async () => {
    const user = userEvent.setup()
    server.use(http.delete(TRANSACTION_URL, () => new HttpResponse(null, { status: 500 })))
    const onDeleted = vi.fn()

    render(
      <TransactionDeleteDialog transaction={transaction} onOpenChange={vi.fn()} onDeleted={onDeleted} />,
    )

    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(await screen.findByText('Não foi possível excluir')).toBeInTheDocument()
    expect(screen.getByText('Excluir despesa')).toBeInTheDocument()
    expect(onDeleted).not.toHaveBeenCalled()
  })
})
