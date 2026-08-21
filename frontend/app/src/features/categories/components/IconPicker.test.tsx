import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { IconPicker } from './IconPicker'

describe('IconPicker', () => {
  it('nenhum ícone marcado como pressionado quando value é undefined', () => {
    render(<IconPicker value={undefined} onChange={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Alimentação' })).toHaveAttribute(
      'aria-pressed',
      'false',
    )
  })

  it('marca o ícone correspondente ao value como pressionado', () => {
    render(<IconPicker value="car" onChange={vi.fn()} />)

    expect(screen.getByRole('button', { name: 'Transporte' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
    expect(screen.getByRole('button', { name: 'Alimentação' })).toHaveAttribute(
      'aria-pressed',
      'false',
    )
  })

  it('clicar em um ícone chama onChange com o value correspondente', async () => {
    const user = userEvent.setup()
    const onChange = vi.fn()
    render(<IconPicker value={undefined} onChange={onChange} />)

    await user.click(screen.getByRole('button', { name: 'Viagem' }))

    expect(onChange).toHaveBeenCalledWith('plane')
  })
})
