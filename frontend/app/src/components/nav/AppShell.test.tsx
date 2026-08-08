import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { AppShell } from './AppShell'

function renderAppShell(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route element={<AppShell />}>
          <Route path="/" element={<div>Home Content</div>} />
          <Route path="/expenses" element={<div>Expenses Content</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

describe('AppShell', () => {
  it('renderiza o conteúdo da rota filha via Outlet, mantendo o shell montado', () => {
    renderAppShell('/')

    expect(screen.getByText('Home Content')).toBeInTheDocument()
    expect(screen.getAllByRole('navigation', { name: /navegação principal/i })).toHaveLength(2)
  })

  it('trocar de rota troca o conteúdo do Outlet mantendo a navegação', () => {
    renderAppShell('/expenses')

    expect(screen.getByText('Expenses Content')).toBeInTheDocument()
    expect(screen.queryByText('Home Content')).not.toBeInTheDocument()
    expect(screen.getAllByRole('navigation', { name: /navegação principal/i })).toHaveLength(2)
  })
})
