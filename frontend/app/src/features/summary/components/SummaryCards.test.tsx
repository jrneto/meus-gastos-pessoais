import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import type { SummaryResponse } from '../api/summaryApi'
import { SummaryCards } from './SummaryCards'

const baseSummary: SummaryResponse = {
  month: '2026-08',
  saldoCents: 394720,
  receitasCents: 520000,
  gastoCents: 125280,
  orcamentoTotalCents: 299000,
  restanteCents: 173720,
  porCategoria: [],
  ultimosLancamentos: [],
}

describe('SummaryCards', () => {
  it('renderiza os cinco cartões com os valores formatados', () => {
    render(<SummaryCards summary={baseSummary} />)

    expect(screen.getByText('Saldo do mês')).toBeInTheDocument()
    expect(screen.getByText('R$ 3.947,20')).toBeInTheDocument()
    expect(screen.getByText('Receitas no mês')).toBeInTheDocument()
    expect(screen.getByText('R$ 5.200,00')).toBeInTheDocument()
    expect(screen.getByText('Gasto no mês')).toBeInTheDocument()
    expect(screen.getByText('R$ 1.252,80')).toBeInTheDocument()
    expect(screen.getByText('Orçamento total')).toBeInTheDocument()
    expect(screen.getByText('R$ 2.990,00')).toBeInTheDocument()
    expect(screen.getByText('Restante')).toBeInTheDocument()
    expect(screen.getByText('R$ 1.737,20')).toBeInTheDocument()
  })

  it('saldo negativo aparece com sinal "-" e cor accent', () => {
    render(<SummaryCards summary={{ ...baseSummary, saldoCents: -5000 }} />)

    const amount = screen.getByText('- R$ 50,00')
    expect(amount.style.color).toBe('var(--color-accent-700)')
  })

  it('restante negativo aparece com o valor real, cor accent, e barra travada em 100%', () => {
    render(
      <SummaryCards
        summary={{ ...baseSummary, gastoCents: 350000, orcamentoTotalCents: 300000, restanteCents: -50000 }}
      />,
    )

    const amount = screen.getByText('- R$ 500,00')
    expect(amount.style.color).toBe('var(--color-accent-700)')

    const fill = document.querySelector('.je-fill') as HTMLElement
    expect(fill.style.width).toBe('100%')
  })

  it('restante positivo mostra a barra proporcional ao gasto sobre o orçamento total', () => {
    render(<SummaryCards summary={{ ...baseSummary, gastoCents: 150000, orcamentoTotalCents: 300000 }} />)

    const fill = document.querySelector('.je-fill') as HTMLElement
    expect(fill.style.width).toBe('50%')
  })

  it('sem orçamento total definido, a barra fica em 0%', () => {
    render(<SummaryCards summary={{ ...baseSummary, orcamentoTotalCents: 0 }} />)

    const fill = document.querySelector('.je-fill') as HTMLElement
    expect(fill.style.width).toBe('0%')
  })
})
