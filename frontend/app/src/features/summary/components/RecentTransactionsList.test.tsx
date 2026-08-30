import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { SummaryTransactionItem } from '../api/summaryApi'
import { RecentTransactionsList } from './RecentTransactionsList'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: 80000,
  createdAt: '2025-06-15T12:00:00Z',
}

const incomeCategory = {
  id: 'cat-2',
  nome: 'Salário',
  tipo: 'receita',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const expenseItem: SummaryTransactionItem = {
  id: 'tx-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  tipo: 'despesa',
  date: '2026-08-24',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2026-08-24T18:12:00Z',
}

const incomeItem: SummaryTransactionItem = {
  id: 'tx-2',
  description: 'Salário mensal',
  amountInCents: 500000,
  categoryId: 'cat-2',
  tipo: 'receita',
  date: '2026-08-05',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2026-08-05T12:00:00Z',
}

describe('RecentTransactionsList', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category, incomeCategory] })))
  })

  it('renderiza os itens com categoria, descrição e data', async () => {
    render(<RecentTransactionsList items={[expenseItem]} />)

    expect(screen.getByText('Almoço no restaurante')).toBeInTheDocument()
    expect(await screen.findByText('Alimentação · 2026-08-24')).toBeInTheDocument()
  })

  it('despesa aparece com sinal "-" e cor accent; receita com sinal "+" e cor positive', () => {
    render(<RecentTransactionsList items={[expenseItem, incomeItem]} />)

    const expenseAmount = screen.getByText('- R$ 45,90')
    const incomeAmount = screen.getByText('+ R$ 5.000,00')

    expect(expenseAmount.style.color).toBe('var(--color-accent-700)')
    expect(incomeAmount.style.color).toBe('var(--color-positive-700)')
  })

  it('categoria sem correspondência renderiza rótulo genérico, sem quebrar', async () => {
    render(<RecentTransactionsList items={[{ ...expenseItem, categoryId: 'inexistente' }]} />)

    expect(await screen.findByText('Categoria não encontrada · 2026-08-24')).toBeInTheDocument()
  })

  it('lista vazia mostra estado vazio', () => {
    render(<RecentTransactionsList items={[]} />)

    expect(screen.getByText('Nenhuma transação neste mês.')).toBeInTheDocument()
  })

  it('itens não são clicáveis (sem cursor de ponteiro)', () => {
    render(<RecentTransactionsList items={[expenseItem]} />)

    const row = screen.getByText('Almoço no restaurante').closest('div')?.parentElement
    expect(row?.style.cursor).not.toBe('pointer')
  })
})
