import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { NavMoreSheet } from './NavMoreSheet'

describe('NavMoreSheet', () => {
  it('não renderiza nada quando `open` é false', () => {
    render(
      <MemoryRouter>
        <NavMoreSheet open={false} onOpenChange={vi.fn()} />
      </MemoryRouter>,
    )

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('lista Relatórios e Categorias quando aberto', () => {
    render(
      <MemoryRouter>
        <NavMoreSheet open onOpenChange={vi.fn()} />
      </MemoryRouter>,
    )

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByRole('link', { name: /relatórios/i })).toBeInTheDocument()
    expect(within(dialog).getByRole('link', { name: /categorias/i })).toBeInTheDocument()
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    render(
      <MemoryRouter>
        <NavMoreSheet open onOpenChange={onOpenChange} />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('dialog').parentElement!)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('não fecha ao clicar no título do painel', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    render(
      <MemoryRouter>
        <NavMoreSheet open onOpenChange={onOpenChange} />
      </MemoryRouter>,
    )

    await user.click(screen.getByText('Mais'))

    expect(onOpenChange).not.toHaveBeenCalled()
  })

  it('fecha ao clicar em um item de navegação', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    render(
      <MemoryRouter>
        <NavMoreSheet open onOpenChange={onOpenChange} />
      </MemoryRouter>,
    )

    await user.click(screen.getByRole('link', { name: /categorias/i }))

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao pressionar Esc', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    render(
      <MemoryRouter>
        <NavMoreSheet open onOpenChange={onOpenChange} />
      </MemoryRouter>,
    )

    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
