import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { TransactionQueryItem } from '../api/transactionsApi'
import { TransactionList } from './TransactionList'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const item: TransactionQueryItem = {
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

const incomeItem: TransactionQueryItem = {
  id: 'tx-2',
  description: 'Salário',
  amountInCents: 500000,
  categoryId: 'cat-2',
  tipo: 'receita',
  date: '2025-06-05',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2025-06-05T12:00:00Z',
}

function renderTransactionList(props: Partial<React.ComponentProps<typeof TransactionList>> = {}) {
  return render(
    <TransactionList
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

describe('TransactionList', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
  })

  it('renderiza os itens formatados', async () => {
    renderTransactionList({ items: [item] })

    expect(screen.getByText('Almoço no restaurante')).toBeInTheDocument()
    expect(screen.getByText('- R$ 45,90')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
  })

  it('receita aparece com sinal "+" e cor positive; despesa com sinal "-" e cor accent', () => {
    renderTransactionList({ items: [item, incomeItem] })

    const expenseAmount = screen.getByText('- R$ 45,90')
    const incomeAmount = screen.getByText('+ R$ 5.000,00')

    expect(expenseAmount.style.color).toBe('var(--color-accent-700)')
    expect(incomeAmount.style.color).toBe('var(--color-positive-700)')
  })

  it('categoria sem correspondência renderiza rótulo genérico, sem quebrar', () => {
    renderTransactionList({ items: [{ ...item, categoryId: 'inexistente' }] })

    expect(screen.getByText('Categoria não encontrada')).toBeInTheDocument()
  })

  it('não mostra cor customizada no texto da categoria (FEAT-20)', async () => {
    renderTransactionList({ items: [item] })

    const tag = await screen.findByText('Alimentação')
    expect(tag.getAttribute('style') ?? '').not.toContain('color')
  })

  it('exibe estado vazio quando não há itens', () => {
    renderTransactionList()

    expect(
      screen.getByText('Nenhuma despesa encontrada para os filtros selecionados.'),
    ).toBeInTheDocument()
  })

  it('não exibe botão "Carregar mais" quando hasMore é false', () => {
    renderTransactionList({ items: [item] })

    expect(screen.queryByRole('button', { name: /carregar mais/i })).not.toBeInTheDocument()
  })

  it('chama onLoadMore ao clicar em "Carregar mais" quando hasMore é true', async () => {
    const user = userEvent.setup()
    const onLoadMore = vi.fn()

    renderTransactionList({ items: [item], hasMore: true, onLoadMore })

    await user.click(screen.getByRole('button', { name: /carregar mais/i }))
    expect(onLoadMore).toHaveBeenCalled()
  })

  it('exibe alerta de erro quando error está setado', () => {
    renderTransactionList({ error: new Error('Um ou mais filtros são inválidos.') })

    expect(screen.getByText('Não foi possível buscar as despesas')).toBeInTheDocument()
    expect(screen.getByText('Um ou mais filtros são inválidos.')).toBeInTheDocument()
  })

  it('não exibe mais coluna/ícones de ações por linha (FEAT-20)', () => {
    renderTransactionList({ items: [item] })

    expect(screen.queryByText('Ações')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /editar despesa/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /excluir despesa/i })).not.toBeInTheDocument()
  })

  it('clicar numa linha chama onRowClick com o item correto', async () => {
    const user = userEvent.setup()
    const onRowClick = vi.fn()
    renderTransactionList({ items: [item], onRowClick })

    await user.click(screen.getByText('Almoço no restaurante'))

    expect(onRowClick).toHaveBeenCalledWith(item)
  })
})
