import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { CategorySummaryItem } from '../api/summaryApi'
import { CategorySpendingList } from './CategorySpendingList'

const items: CategorySummaryItem[] = [
  { categoryId: 'cat-1', nome: 'Alimentação', gastoCents: 30670, orcamentoMensalCents: 80000 },
  { categoryId: 'cat-2', nome: 'Lazer', gastoCents: 50000, orcamentoMensalCents: 40000 },
]

describe('CategorySpendingList', () => {
  it('renderiza cada categoria com gasto/orçamento formatado', () => {
    render(<CategorySpendingList items={items} />)

    expect(screen.getByText('Alimentação')).toBeInTheDocument()
    expect(screen.getByText('R$ 306,70 / R$ 800,00')).toBeInTheDocument()
    expect(screen.getByText('Lazer')).toBeInTheDocument()
    expect(screen.getByText('R$ 500,00 / R$ 400,00')).toBeInTheDocument()
  })

  it('categoria acima do orçamento aparece com cor accent e barra travada em 100%', () => {
    render(<CategorySpendingList items={items} />)

    const overText = screen.getByText('R$ 500,00 / R$ 400,00')
    expect(overText.style.color).toBe('var(--color-accent-700)')

    const fills = document.querySelectorAll('.je-fill')
    expect((fills[1] as HTMLElement).style.width).toBe('100%')
    expect((fills[1] as HTMLElement).style.background).toBe('var(--color-accent)')
  })

  it('categoria dentro do orçamento aparece com cor neutra e barra proporcional', () => {
    render(<CategorySpendingList items={items} />)

    const underText = screen.getByText('R$ 306,70 / R$ 800,00')
    expect(underText.style.color).toBe('var(--color-text)')

    const fills = document.querySelectorAll('.je-fill')
    expect(parseFloat((fills[0] as HTMLElement).style.width)).toBeCloseTo(38.3375, 4)
    expect((fills[0] as HTMLElement).style.background).toBe('var(--color-neutral-800)')
  })

  it('lista vazia mostra estado vazio', () => {
    render(<CategorySpendingList items={[]} />)

    expect(screen.getByText('Nenhuma categoria com orçamento definido ainda.')).toBeInTheDocument()
  })
})
