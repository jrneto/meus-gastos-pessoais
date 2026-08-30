import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { TransactionsListPage } from './TransactionsListPage'

const TRANSACTIONS_URL = 'http://localhost:5049/transactions'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const item = {
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

function renderPage() {
  return render(
    <MemoryRouter>
      <TransactionsListPage />
    </MemoryRouter>,
  )
}

describe('TransactionsListPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(TRANSACTIONS_URL, () => HttpResponse.json({ items: [], nextCursor: null })))
  })

  it('exibe o título "Transações" (FEAT-16)', () => {
    renderPage()

    expect(screen.getByRole('heading', { name: 'Transações' })).toBeInTheDocument()
  })

  it('não exibe o botão "+ Nova receita" nesta feature (FEAT-23)', () => {
    renderPage()

    expect(screen.queryByRole('button', { name: /nova receita/i })).not.toBeInTheDocument()
  })

  it('clicar em "+ Nova despesa" abre o popup de cadastro (FEAT-17)', async () => {
    const user = userEvent.setup()
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [] })))

    renderPage()

    await user.click(screen.getByRole('button', { name: /nova despesa/i }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
  })

  it('clicar numa linha abre o popup de detalhe (FEAT-20)', async () => {
    const user = userEvent.setup()
    server.use(
      http.get(TRANSACTIONS_URL, () => HttpResponse.json({ items: [item], nextCursor: null })),
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })),
    )

    renderPage()

    await user.click(await screen.findByText('Almoço no restaurante'))

    expect(await screen.findByText('Detalhe da despesa')).toBeInTheDocument()
  })

  it('"Editar" no detalhe abre o popup de edição pré-preenchido (FEAT-20)', async () => {
    const user = userEvent.setup()
    server.use(
      http.get(TRANSACTIONS_URL, () => HttpResponse.json({ items: [item], nextCursor: null })),
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })),
      http.get('http://localhost:5049/transactions/tx-1', () => HttpResponse.json(item)),
    )

    renderPage()

    await user.click(await screen.findByText('Almoço no restaurante'))
    await screen.findByText('Detalhe da despesa')
    await user.click(screen.getByRole('button', { name: /^editar$/i }))

    expect(screen.getByText('Editar despesa')).toBeInTheDocument()
    expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
  })

  it('"Excluir" no detalhe abre a confirmação e exclui com sucesso (FEAT-20)', async () => {
    const user = userEvent.setup()
    server.use(
      http.get(TRANSACTIONS_URL, () => HttpResponse.json({ items: [item], nextCursor: null })),
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })),
      http.delete('http://localhost:5049/transactions/tx-1', () => new HttpResponse(null, { status: 204 })),
    )

    renderPage()

    await user.click(await screen.findByText('Almoço no restaurante'))
    await screen.findByText('Detalhe da despesa')
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(await screen.findByText('Excluir despesa')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(screen.queryByText('Almoço no restaurante')).not.toBeInTheDocument())
  })
})
