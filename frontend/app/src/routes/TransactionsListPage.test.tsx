import { fireEvent, render, screen, waitFor } from '@testing-library/react'
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

function renderPage(initialEntries: string[] = ['/']) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
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

  it('exibe o botão "+ Nova receita" ao lado do "+ Nova despesa" (FEAT-24)', () => {
    renderPage()

    expect(screen.getByRole('button', { name: /nova receita/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /nova despesa/i })).toBeInTheDocument()
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

  it('chega com ?yearMonth= já filtrada, e com o painel de filtros avançados aberto (FEAT-26)', async () => {
    let requestedYearMonth: string | null = null
    server.use(
      http.get(TRANSACTIONS_URL, ({ request }) => {
        requestedYearMonth = new URL(request.url).searchParams.get('yearMonth')
        return HttpResponse.json({ items: [item], nextCursor: null })
      }),
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })),
    )

    renderPage(['/transactions?yearMonth=2026-08'])

    await screen.findByText('Almoço no restaurante')
    expect(requestedYearMonth).toBe('2026-08')
    expect(screen.getByLabelText('Mês')).toHaveValue('2026-08')
    expect(screen.getByRole('button', { name: /filtros avançados/i })).toHaveAttribute(
      'aria-expanded',
      'true',
    )
  })

  it('clicar em "+ Nova receita", preencher e submeter cria a receita e ela aparece na listagem (FEAT-24)', async () => {
    const user = userEvent.setup()
    const incomeCategory = {
      id: 'cat-2',
      nome: 'Salário',
      tipo: 'receita',
      orcamentoMensalCents: null,
      createdAt: '2025-06-15T12:00:00Z',
    }
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [incomeCategory] })))

    let posted: unknown = null
    server.use(
      http.post(TRANSACTIONS_URL, async ({ request }) => {
        posted = await request.json()
        return HttpResponse.json({
          id: 'tx-2',
          description: 'Salário mensal',
          amountInCents: 500000,
          categoryId: 'cat-2',
          tipo: 'receita',
          date: '2025-06-05',
          createdByUserId: 'user-1',
          createdByLabel: 'Você',
          createdAt: '2025-06-05T12:00:00Z',
        })
      }),
    )
    server.use(
      http.get(
        TRANSACTIONS_URL,
        () => HttpResponse.json({
          items: [
            {
              id: 'tx-2',
              description: 'Salário mensal',
              amountInCents: 500000,
              categoryId: 'cat-2',
              tipo: 'receita',
              date: '2025-06-05',
              createdByUserId: 'user-1',
              createdByLabel: 'Você',
              createdAt: '2025-06-05T12:00:00Z',
            },
          ],
          nextCursor: null,
        }),
        { once: false },
      ),
    )

    renderPage()

    await user.click(screen.getByRole('button', { name: /nova receita/i }))

    expect(await screen.findByText('Nova receita')).toBeInTheDocument()
    await user.type(screen.getByLabelText('Descrição'), 'Salário mensal')
    await user.type(screen.getByLabelText('Valor'), '5000,00')
    await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-2')
    fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2025-06-05' } })
    await user.click(screen.getByRole('button', { name: /registrar receita/i }))

    await waitFor(() => expect(posted).toMatchObject({ tipo: 'receita' }))
    expect(await screen.findByText('Salário mensal')).toBeInTheDocument()
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
