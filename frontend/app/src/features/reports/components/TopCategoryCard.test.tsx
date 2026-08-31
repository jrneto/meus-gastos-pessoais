import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ReportTopCategory } from '../api/reportsApi'
import { TopCategoryCard } from './TopCategoryCard'

describe('TopCategoryCard', () => {
  it('mostra categoria, valor e percentual do orçamento quando definido', () => {
    const category: ReportTopCategory = {
      categoryId: 'cat-1',
      nome: 'Alimentação',
      gastoCents: 43510,
      percentualOrcamento: 54.4,
    }
    render(<TopCategoryCard category={category} />)

    expect(screen.getByText('Maior gasto')).toBeInTheDocument()
    expect(screen.getByText('Alimentação')).toBeInTheDocument()
    expect(screen.getByText('R$ 435,10 · 54,4% do orçamento')).toBeInTheDocument()
  })

  it('mostra categoria e valor sem percentual quando orçamento não é definido', () => {
    const category: ReportTopCategory = {
      categoryId: 'cat-1',
      nome: 'Alimentação',
      gastoCents: 43510,
      percentualOrcamento: null,
    }
    render(<TopCategoryCard category={category} />)

    expect(screen.getByText('R$ 435,10')).toBeInTheDocument()
    expect(screen.queryByText(/do orçamento/)).not.toBeInTheDocument()
  })

  it('mostra estado vazio quando não há categoria', () => {
    render(<TopCategoryCard category={null} />)

    expect(screen.getByText('Nenhum gasto registrado')).toBeInTheDocument()
  })
})
