import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { MobileBottomNav } from './MobileBottomNav'

function renderBottomNav(initialPath = '/') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="*" element={<MobileBottomNav />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('MobileBottomNav', () => {
  it('renderiza os 3 itens primários (FEAT-15) e o botão "Mais"', () => {
    renderBottomNav()

    const nav = screen.getByRole('navigation', { name: /navegação principal/i })
    expect(within(nav).getByRole('link', { name: /início/i })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /transações/i })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /configurações/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /mais/i })).toBeInTheDocument()
  })

  it('destaca o item ativo pela rota atual', () => {
    renderBottomNav('/expenses')

    const nav = screen.getByRole('navigation', { name: /navegação principal/i })
    expect(within(nav).getByRole('link', { name: /transações/i })).toHaveAttribute(
      'aria-current',
      'page',
    )
    expect(within(nav).getByRole('link', { name: /início/i })).not.toHaveAttribute('aria-current')
  })

  it('clicar em "Mais" abre o painel com Relatórios e Categorias, ambos navegáveis', async () => {
    const user = userEvent.setup()
    renderBottomNav()

    await user.click(screen.getByRole('button', { name: /mais/i }))

    const dialog = await screen.findByRole('dialog')
    const relatorios = within(dialog).getByRole('link', { name: /relatórios/i })
    expect(relatorios).toHaveAttribute('href', '/reports')

    const categorias = within(dialog).getByRole('link', { name: /categorias/i })
    expect(categorias).toHaveAttribute('href', '/categories')
  })
})
