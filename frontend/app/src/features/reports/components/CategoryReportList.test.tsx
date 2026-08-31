import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { ReportCategoryItem } from '../api/reportsApi'
import { CategoryReportList } from './CategoryReportList'

const items: ReportCategoryItem[] = [
  { categoryId: 'cat-1', nome: 'Alimentação', gastoCents: 43510 },
  { categoryId: 'cat-2', nome: 'Moradia', gastoCents: 31020 },
  { categoryId: 'cat-3', nome: 'Transporte', gastoCents: 10878 },
]

describe('CategoryReportList', () => {
  it('renderiza os itens com nome e valor formatado, na ordem recebida', () => {
    render(<CategoryReportList items={items} />)

    expect(screen.getByText('Alimentação')).toBeInTheDocument()
    expect(screen.getByText('R$ 435,10')).toBeInTheDocument()
    expect(screen.getByText('Moradia')).toBeInTheDocument()
    expect(screen.getByText('R$ 310,20')).toBeInTheDocument()
    expect(screen.getByText('Transporte')).toBeInTheDocument()
    expect(screen.getByText('R$ 108,78')).toBeInTheDocument()
  })

  it('barra do primeiro item (maior gasto) fica em 100%', () => {
    const { container } = render(<CategoryReportList items={items} />)

    const bars = container.querySelectorAll('div[style*="background: var(--color-neutral-800)"]')
    expect(bars[0]).toHaveStyle({ width: '100%' })
  })

  it('barra dos demais itens é proporcional ao maior gasto', () => {
    const { container } = render(<CategoryReportList items={items} />)

    const bars = container.querySelectorAll('div[style*="background: var(--color-neutral-800)"]')
    // 31020 / 43510 * 100 = 71.30... → arredonda pra 71
    expect(bars[1]).toHaveStyle({ width: '71%' })
    // 10878 / 43510 * 100 = 25.00... → arredonda pra 25
    expect(bars[2]).toHaveStyle({ width: '25%' })
  })

  it('lista vazia mostra estado vazio', () => {
    render(<CategoryReportList items={[]} />)

    expect(screen.getByText('Nenhuma despesa neste período.')).toBeInTheDocument()
  })
})
