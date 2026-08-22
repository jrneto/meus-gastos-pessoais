import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { vi } from 'vitest'
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

async function openAdvancedPanel() {
  const user = userEvent.setup()
  await user.click(screen.getByRole('button', { name: /filtros avançados/i }))
  return user
}

describe('ExpenseFilters', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('submit sem nenhum filtro chama onApply com todos os campos undefined', async () => {
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)
    const user = await openAdvancedPanel()
    await user.click(screen.getByRole('button', { name: /^filtrar$/i }))

    expect(onApply).toHaveBeenCalledWith({
      yearMonth: undefined,
      categoryId: undefined,
      dateFrom: undefined,
      dateTo: undefined,
      minAmountInCents: undefined,
      maxAmountInCents: undefined,
    })
  })

  it('clicar em um chip de categoria aplica o filtro imediatamente', async () => {
    const onApply = vi.fn()
    const user = userEvent.setup()

    render(<ExpenseFilters onApply={onApply} />)

    const chip = await screen.findByRole('button', { name: 'Alimentação' })
    await user.click(chip)

    expect(onApply).toHaveBeenCalledWith(expect.objectContaining({ categoryId: 'cat-1' }))
    expect(chip).toHaveAttribute('aria-pressed', 'true')
  })

  it('clicar de novo no mesmo chip limpa o filtro de categoria', async () => {
    const onApply = vi.fn()
    const user = userEvent.setup()

    render(<ExpenseFilters onApply={onApply} />)

    const chip = await screen.findByRole('button', { name: 'Alimentação' })
    await user.click(chip)
    await user.click(chip)

    expect(onApply).toHaveBeenLastCalledWith(expect.objectContaining({ categoryId: undefined }))
    expect(chip).toHaveAttribute('aria-pressed', 'false')
  })

  it('submit com filtros avançados preenchidos chama onApply com dados transformados', async () => {
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)
    const user = await openAdvancedPanel()

    fireEvent.change(screen.getByLabelText('Mês'), { target: { value: '2025-06' } })
    fireEvent.change(screen.getByLabelText('De'), { target: { value: '2025-06-01' } })
    fireEvent.change(screen.getByLabelText('Até'), { target: { value: '2025-06-30' } })
    await user.type(screen.getByLabelText('Valor mín.'), '10,00')
    await user.type(screen.getByLabelText('Valor máx.'), '100,00')

    await user.click(screen.getByRole('button', { name: /^filtrar$/i }))

    expect(onApply).toHaveBeenCalledWith({
      yearMonth: '2025-06',
      categoryId: undefined,
      dateFrom: '2025-06-01',
      dateTo: '2025-06-30',
      minAmountInCents: 1000,
      maxAmountInCents: 10000,
    })
  })

  it('exibe um indicador visual quando algum filtro avançado está ativo', async () => {
    render(<ExpenseFilters onApply={vi.fn()} />)
    await openAdvancedPanel()

    const toggle = screen.getByRole('button', { name: /filtros avançados/i })
    expect(toggle.querySelector('[aria-hidden="true"]')).not.toBeInTheDocument()

    fireEvent.change(screen.getByLabelText('De'), { target: { value: '2025-06-01' } })

    expect(toggle.querySelector('[aria-hidden="true"]')).toBeInTheDocument()
  })

  it('exibe erro inline e não chama onApply quando dateFrom é posterior a dateTo', async () => {
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)
    const user = await openAdvancedPanel()

    fireEvent.change(screen.getByLabelText('De'), { target: { value: '2025-06-30' } })
    fireEvent.change(screen.getByLabelText('Até'), { target: { value: '2025-06-01' } })

    await user.click(screen.getByRole('button', { name: /^filtrar$/i }))

    expect(
      await screen.findByText('Data inicial não pode ser depois da data final.'),
    ).toBeInTheDocument()
    expect(onApply).not.toHaveBeenCalled()
  })

  it('exibe erro inline e não chama onApply quando minAmount é maior que maxAmount', async () => {
    const onApply = vi.fn()

    render(<ExpenseFilters onApply={onApply} />)
    const user = await openAdvancedPanel()

    await user.type(screen.getByLabelText('Valor mín.'), '100,00')
    await user.type(screen.getByLabelText('Valor máx.'), '10,00')

    await user.click(screen.getByRole('button', { name: /^filtrar$/i }))

    expect(
      await screen.findByText('Valor mínimo não pode ser maior que o máximo.'),
    ).toBeInTheDocument()
    expect(onApply).not.toHaveBeenCalled()
  })

  it('"Limpar filtros" zera os campos avançados e reaplica a busca, preservando a categoria selecionada', async () => {
    const onApply = vi.fn()
    const user = userEvent.setup()

    render(<ExpenseFilters onApply={onApply} />)

    const chip = await screen.findByRole('button', { name: 'Alimentação' })
    await user.click(chip)

    await openAdvancedPanel()
    fireEvent.change(screen.getByLabelText('Mês'), { target: { value: '2025-06' } })
    fireEvent.change(screen.getByLabelText('De'), { target: { value: '2025-06-01' } })
    fireEvent.change(screen.getByLabelText('Até'), { target: { value: '2025-06-30' } })
    await user.type(screen.getByLabelText('Valor mín.'), '10,00')
    await user.type(screen.getByLabelText('Valor máx.'), '100,00')

    await user.click(screen.getByRole('button', { name: /limpar filtros/i }))

    expect(onApply).toHaveBeenLastCalledWith({
      yearMonth: undefined,
      categoryId: 'cat-1',
      dateFrom: undefined,
      dateTo: undefined,
      minAmountInCents: undefined,
      maxAmountInCents: undefined,
    })
    expect(screen.getByLabelText('Mês')).toHaveValue('')
    expect(screen.getByLabelText('De')).toHaveValue('')
    expect(screen.getByLabelText('Até')).toHaveValue('')
    expect(screen.getByLabelText('Valor mín.')).toHaveValue('')
    expect(screen.getByLabelText('Valor máx.')).toHaveValue('')
    expect(chip).toHaveAttribute('aria-pressed', 'true')
  })
})
