import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ExpenseFilters } from './ExpenseFilters'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('ExpenseFilters', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('submit sem nenhum filtro chama onApply com todos os campos undefined', async () => {
    const user = userEvent.setup()
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)
    await user.click(screen.getByRole('button', { name: /filtrar/i }))

    expect(onApply).toHaveBeenCalledWith({
      yearMonth: undefined,
      categoryId: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      minAmountInCents: undefined,
      maxAmountInCents: undefined,
    })
  })

  it('submit com filtros preenchidos chama onApply com dados transformados', async () => {
    const user = userEvent.setup()
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)

    fireEvent.change(screen.getByLabelText('Mês'), { target: { value: '2025-06' } })
    await user.click(screen.getByRole('combobox', { name: 'Categoria' }))
    await user.click(await screen.findByRole('option', { name: 'Alimentação' }))
    fireEvent.change(screen.getByLabelText('De'), { target: { value: '2025-06-01' } })
    fireEvent.change(screen.getByLabelText('Até'), { target: { value: '2025-06-30' } })
    await user.type(screen.getByLabelText('Valor mín.'), '10,00')
    await user.type(screen.getByLabelText('Valor máx.'), '100,00')

    await user.click(screen.getByRole('button', { name: /filtrar/i }))

    expect(onApply).toHaveBeenCalledWith({
      yearMonth: '2025-06',
      categoryId: 'cat-1',
      dateFrom: '2025-06-01',
      dateTo: '2025-06-30',
      minAmountInCents: 1000,
      maxAmountInCents: 10000,
    })
  })

  it('exibe erro inline e não chama onApply quando dateFrom é posterior a dateTo', async () => {
    const user = userEvent.setup()
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)

    fireEvent.change(screen.getByLabelText('De'), { target: { value: '2025-06-30' } })
    fireEvent.change(screen.getByLabelText('Até'), { target: { value: '2025-06-01' } })

    await user.click(screen.getByRole('button', { name: /filtrar/i }))

    expect(
      await screen.findByText('Data inicial não pode ser depois da data final.'),
    ).toBeInTheDocument()
    expect(onApply).not.toHaveBeenCalled()
  })

  it('exibe erro inline e não chama onApply quando minAmount é maior que maxAmount', async () => {
    const user = userEvent.setup()
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)

    await user.type(screen.getByLabelText('Valor mín.'), '100,00')
    await user.type(screen.getByLabelText('Valor máx.'), '10,00')

    await user.click(screen.getByRole('button', { name: /filtrar/i }))

    expect(
      await screen.findByText('Valor mínimo não pode ser maior que o máximo.'),
    ).toBeInTheDocument()
    expect(onApply).not.toHaveBeenCalled()
  })
})
