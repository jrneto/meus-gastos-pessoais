import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { DesktopSidebar } from './DesktopSidebar'

function renderSidebar(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route
          path="*"
          element={
            <>
              <DesktopSidebar />
              <span data-testid="current-path">{initialPath}</span>
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  )
}

describe('DesktopSidebar', () => {
  it('renderiza os 5 itens de menu, sem grupos/subitens (FEAT-15)', () => {
    renderSidebar('/')

    expect(screen.getByText('Início')).toBeInTheDocument()
    expect(screen.getByText('Transações')).toBeInTheDocument()
    expect(screen.queryByText('Nova despesa')).not.toBeInTheDocument()
    expect(screen.queryByText('Listagem / Filtros')).not.toBeInTheDocument()
    expect(screen.getByText('Relatórios')).toBeInTheDocument()
    expect(screen.getByText('Categorias')).toBeInTheDocument()
    expect(screen.getByText('Configurações')).toBeInTheDocument()
  })

  it('destaca o item correspondente à rota atual', () => {
    renderSidebar('/expenses')

    const transacoes = screen.getByRole('link', { name: /transações/i })
    expect(transacoes).toHaveAttribute('aria-current', 'page')

    const categorias = screen.getByRole('link', { name: /categorias/i })
    expect(categorias).not.toHaveAttribute('aria-current')
  })

  it('"Relatórios" é um link navegável (não fica mais desabilitado)', () => {
    renderSidebar('/')

    const relatorios = screen.getByRole('link', { name: /relatórios/i })
    expect(relatorios).toHaveAttribute('href', '/reports')
  })

  it('colapsar oculta rótulos mas mantém todos os itens acessíveis', async () => {
    const user = userEvent.setup()
    renderSidebar('/')

    await user.click(screen.getByRole('button', { name: /colapsar menu/i }))

    expect(screen.queryByText('Transações')).not.toBeInTheDocument()
    expect(screen.getByTitle('Transações')).toBeInTheDocument()
    expect(screen.getByTitle('Início')).toBeInTheDocument()
    expect(screen.getByTitle('Relatórios')).toBeInTheDocument()
    expect(screen.getByTitle('Categorias')).toBeInTheDocument()
    expect(screen.getByTitle('Configurações')).toBeInTheDocument()
  })
})
