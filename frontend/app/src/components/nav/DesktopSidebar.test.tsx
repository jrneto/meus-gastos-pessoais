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
  it('renderiza a hierarquia completa', () => {
    renderSidebar('/')

    expect(screen.getByText('Início')).toBeInTheDocument()
    expect(screen.getByText('Despesas')).toBeInTheDocument()
    expect(screen.getByText('Nova despesa')).toBeInTheDocument()
    expect(screen.getByText('Listagem / Filtros')).toBeInTheDocument()
    expect(screen.getByText('Relatórios')).toBeInTheDocument()
    expect(screen.getByText('Categorias')).toBeInTheDocument()
    expect(screen.getByText('Configurações')).toBeInTheDocument()
  })

  it('destaca o item correspondente à rota atual', () => {
    renderSidebar('/expenses')

    const listagem = screen.getByRole('link', { name: /listagem \/ filtros/i })
    expect(listagem).toHaveAttribute('aria-current', 'page')

    const novaDespesa = screen.getByRole('link', { name: /nova despesa/i })
    expect(novaDespesa).not.toHaveAttribute('aria-current')
  })

  it('Relatórios e Categorias não são links e não navegam ao clicar', async () => {
    const user = userEvent.setup()
    renderSidebar('/')

    const relatorios = screen.getByText('Relatórios').closest('[role="button"]')
    expect(relatorios).toHaveAttribute('aria-disabled', 'true')
    expect(relatorios?.tagName).not.toBe('A')

    await user.click(relatorios!)

    expect(screen.getByTestId('current-path')).toHaveTextContent('/')
  })

  it('colapsar oculta rótulos mas mantém todos os itens folha acessíveis', async () => {
    const user = userEvent.setup()
    renderSidebar('/')

    await user.click(screen.getByRole('button', { name: /colapsar menu/i }))

    expect(screen.queryByText('Nova despesa')).not.toBeInTheDocument()
    expect(screen.getByTitle('Nova despesa')).toBeInTheDocument()
    expect(screen.getByTitle('Listagem / Filtros')).toBeInTheDocument()
    expect(screen.getByTitle('Configurações')).toBeInTheDocument()
    expect(screen.getByTitle('Início')).toBeInTheDocument()
  })
})
