import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { TotalPeriodCard } from './TotalPeriodCard'

describe('TotalPeriodCard', () => {
  it('mostra o total formatado', () => {
    render(<TotalPeriodCard totalCents={138120} variacaoPercentual={null} period="month" />)

    expect(screen.getByText('Total no período')).toBeInTheDocument()
    expect(screen.getByText('R$ 1.381,20')).toBeInTheDocument()
  })

  it('variação positiva mostra sinal + e rótulo do período mensal', () => {
    render(<TotalPeriodCard totalCents={138120} variacaoPercentual={12} period="month" />)

    expect(screen.getByText('+12% vs mês passado')).toBeInTheDocument()
  })

  it('variação negativa mostra sinal - e rótulo do período semanal', () => {
    render(<TotalPeriodCard totalCents={138120} variacaoPercentual={-4} period="week" />)

    expect(screen.getByText('-4% vs semana passada')).toBeInTheDocument()
  })

  it('rótulo do período anual', () => {
    render(<TotalPeriodCard totalCents={138120} variacaoPercentual={8} period="year" />)

    expect(screen.getByText('+8% vs ano passado')).toBeInTheDocument()
  })

  it('variação null não mostra nenhuma linha de comparação', () => {
    render(<TotalPeriodCard totalCents={0} variacaoPercentual={null} period="month" />)

    expect(screen.queryByText(/vs mês passado/)).not.toBeInTheDocument()
    expect(screen.queryByText(/vs semana passada/)).not.toBeInTheDocument()
    expect(screen.queryByText(/vs ano passado/)).not.toBeInTheDocument()
  })
})
