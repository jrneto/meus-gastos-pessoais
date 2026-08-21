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
  it('renderiza os 4 itens principais e o botão "Mais"', () => {
    renderBottomNav()

    const nav = screen.getByRole('navigation', { name: /navegação principal/i })
    expect(within(nav).getByRole('link', { name: /início/i })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /nova despesa/i })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /listagem \/ filtros/i })).toBeInTheDocument()
    expect(within(nav).getByRole('link', { name: /configurações/i })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /mais/i })).toBeInTheDocument()
  })

  it('clicar em "Mais" abre o sheet com Relatórios não-clicável e Categorias navegável', async () => {
    const user = userEvent.setup()
    renderBottomNav()

    await user.click(screen.getByRole('button', { name: /mais/i }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    const relatorios = screen.getByText('Relatórios').closest('[role="button"]')
    expect(relatorios).toHaveAttribute('aria-disabled', 'true')

    const categorias = screen.getByRole('link', { name: /categorias/i })
    expect(categorias).toHaveAttribute('href', '/categories')
  })
})
