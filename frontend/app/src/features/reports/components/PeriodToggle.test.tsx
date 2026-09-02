import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { PeriodToggle } from './PeriodToggle'

describe('PeriodToggle', () => {
  it('mostra as três opções, com a selecionada marcada', () => {
    render(<PeriodToggle value="month" onChange={() => {}} />)

    expect(screen.getByLabelText('Semana')).not.toBeChecked()
    expect(screen.getByLabelText('Mês')).toBeChecked()
    expect(screen.getByLabelText('Ano')).not.toBeChecked()
  })

  it('clicar em "Semana" chama onChange com "week"', async () => {
    const onChange = vi.fn()
    render(<PeriodToggle value="month" onChange={onChange} />)

    await userEvent.click(screen.getByLabelText('Semana'))

    expect(onChange).toHaveBeenCalledWith('week')
  })

  it('clicar em "Ano" chama onChange com "year"', async () => {
    const onChange = vi.fn()
    render(<PeriodToggle value="month" onChange={onChange} />)

    await userEvent.click(screen.getByLabelText('Ano'))

    expect(onChange).toHaveBeenCalledWith('year')
  })

  it('clicar em "Mês" chama onChange com "month"', async () => {
    const onChange = vi.fn()
    render(<PeriodToggle value="week" onChange={onChange} />)

    await userEvent.click(screen.getByLabelText('Mês'))

    expect(onChange).toHaveBeenCalledWith('month')
  })
})
