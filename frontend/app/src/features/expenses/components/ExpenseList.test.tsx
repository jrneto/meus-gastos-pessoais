import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { ExpenseList } from './ExpenseList'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

const item: ExpenseQueryItem = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

function renderExpenseList(props: Partial<React.ComponentProps<typeof ExpenseList>> = {}) {
  return render(
    <MemoryRouter>
      <ExpenseList
        items={[]}
        isLoading={false}
        isLoadingMore={false}
        error={null}
        hasMore={false}
        onLoadMore={vi.fn()}
        onDeleted={vi.fn()}
        {...props}
      />
    </MemoryRouter>,
  )
}

describe('ExpenseList', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('renderiza os itens formatados', async () => {
    renderExpenseList({ items: [item] })

    expect(screen.getByText('Almoço no restaurante')).toBeInTheDocument()
    expect(screen.getByText('R$ 45,90')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
  })

  it('categoryId sem correspondência renderiza rótulo genérico, sem quebrar', () => {
    renderExpenseList({ items: [{ ...item, categoryId: 'inexistente' }] })

    expect(screen.getByText('Categoria não encontrada')).toBeInTheDocument()
  })

  it('exibe estado vazio quando não há itens', () => {
    renderExpenseList()

    expect(
      screen.getByText('Nenhuma despesa encontrada para os filtros selecionados.'),
    ).toBeInTheDocument()
  })

  it('não exibe botão "Carregar mais" quando hasMore é false', () => {
    renderExpenseList({ items: [item] })

    expect(screen.queryByRole('button', { name: /carregar mais/i })).not.toBeInTheDocument()
  })

  it('chama onLoadMore ao clicar em "Carregar mais" quando hasMore é true', async () => {
    const user = userEvent.setup()
    const onLoadMore = vi.fn()

    renderExpenseList({ items: [item], hasMore: true, onLoadMore })

    await user.click(screen.getByRole('button', { name: /carregar mais/i }))
    expect(onLoadMore).toHaveBeenCalled()
  })

  it('exibe alerta de erro quando error está setado', () => {
    renderExpenseList({ error: new Error('Um ou mais filtros são inválidos.') })

    expect(screen.getByText('Não foi possível buscar as despesas')).toBeInTheDocument()
    expect(screen.getByText('Um ou mais filtros são inválidos.')).toBeInTheDocument()
  })

  it('cada item tem um link de editar apontando para /expenses/{id}/edit', () => {
    renderExpenseList({ items: [item] })

    const editLink = screen.getByRole('link', { name: /editar despesa/i })
    expect(editLink).toHaveAttribute('href', '/expenses/exp-1/edit')
  })

  it('clicar em excluir abre o dialog com a descrição da despesa', async () => {
    const user = userEvent.setup()
    renderExpenseList({ items: [item] })

    await user.click(screen.getByRole('button', { name: /excluir despesa/i }))

    expect(screen.getByText('Excluir despesa')).toBeInTheDocument()
    expect(screen.getAllByText(/Almoço no restaurante/).length).toBeGreaterThan(0)
  })

  it('confirmar a exclusão chama a API e onDeleted com o id correto', async () => {
    const user = userEvent.setup()
    server.use(
      http.delete('http://localhost:5049/expenses/exp-1', () => new HttpResponse(null, { status: 204 })),
    )
    const onDeleted = vi.fn()

    renderExpenseList({ items: [item], onDeleted })

    await user.click(screen.getByRole('button', { name: /excluir despesa/i }))
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('exp-1'))
  })
})
