import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { ExpenseList } from './ExpenseList'

const item: ExpenseQueryItem = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  category: 'Alimentacao',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('ExpenseList', () => {
  it('renderiza os itens formatados', () => {
    render(
      <ExpenseList
        items={[item]}
        isLoading={false}
        isLoadingMore={false}
        error={null}
        hasMore={false}
        onLoadMore={vi.fn()}
      />,
    )

    expect(screen.getByText('Almoço no restaurante')).toBeInTheDocument()
    expect(screen.getByText('R$ 45,90')).toBeInTheDocument()
    expect(screen.getByText(/Alimentação/)).toBeInTheDocument()
  })

  it('exibe estado vazio quando não há itens', () => {
    render(
      <ExpenseList
        items={[]}
        isLoading={false}
        isLoadingMore={false}
        error={null}
        hasMore={false}
        onLoadMore={vi.fn()}
      />,
    )

    expect(
      screen.getByText('Nenhuma despesa encontrada para os filtros selecionados.'),
    ).toBeInTheDocument()
  })

  it('não exibe botão "Carregar mais" quando hasMore é false', () => {
    render(
      <ExpenseList
        items={[item]}
        isLoading={false}
        isLoadingMore={false}
        error={null}
        hasMore={false}
        onLoadMore={vi.fn()}
      />,
    )

    expect(screen.queryByRole('button', { name: /carregar mais/i })).not.toBeInTheDocument()
  })

  it('chama onLoadMore ao clicar em "Carregar mais" quando hasMore é true', async () => {
    const user = userEvent.setup()
    const onLoadMore = vi.fn()

    render(
      <ExpenseList
        items={[item]}
        isLoading={false}
        isLoadingMore={false}
        error={null}
        hasMore={true}
        onLoadMore={onLoadMore}
      />,
    )

    await user.click(screen.getByRole('button', { name: /carregar mais/i }))
    expect(onLoadMore).toHaveBeenCalled()
  })

  it('exibe alerta de erro quando error está setado', () => {
    render(
      <ExpenseList
        items={[]}
        isLoading={false}
        isLoadingMore={false}
        error={new Error('Um ou mais filtros são inválidos.')}
        hasMore={false}
        onLoadMore={vi.fn()}
      />,
    )

    expect(screen.getByText('Não foi possível buscar as despesas')).toBeInTheDocument()
    expect(screen.getByText('Um ou mais filtros são inválidos.')).toBeInTheDocument()
  })
})
