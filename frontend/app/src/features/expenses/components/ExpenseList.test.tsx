import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
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
    <ExpenseList
      items={[]}
      isLoading={false}
      isLoadingMore={false}
      error={null}
      hasMore={false}
      onLoadMore={vi.fn()}
      onRowClick={vi.fn()}
      {...props}
    />,
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

  it('categoria sem correspondência renderiza rótulo genérico, sem quebrar', () => {
    renderExpenseList({ items: [{ ...item, categoryId: 'inexistente' }] })

    expect(screen.getByText('Categoria não encontrada')).toBeInTheDocument()
  })

  it('não mostra cor customizada no texto da categoria (FEAT-20)', async () => {
    renderExpenseList({ items: [item] })

    const tag = await screen.findByText('Alimentação')
    expect(tag.getAttribute('style') ?? '').not.toContain('color')
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

  it('não exibe mais coluna/ícones de ações por linha (FEAT-20)', () => {
    renderExpenseList({ items: [item] })

    expect(screen.queryByText('Ações')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /editar despesa/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /excluir despesa/i })).not.toBeInTheDocument()
  })

  it('clicar numa linha chama onRowClick com o item correto', async () => {
    const user = userEvent.setup()
    const onRowClick = vi.fn()
    renderExpenseList({ items: [item], onRowClick })

    await user.click(screen.getByText('Almoço no restaurante'))

    expect(onRowClick).toHaveBeenCalledWith(item)
  })
})
