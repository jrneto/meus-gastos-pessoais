import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { DesktopSidebar } from './DesktopSidebar'

const LOGOUT_URL = 'http://localhost:5049/auth/logout'

function renderSidebar(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
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
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('renderiza os 5 itens de menu, sem grupos/subitens (FEAT-15)', () => {
    renderSidebar('/')

    expect(screen.getByText('Início')).toBeInTheDocument()
    expect(screen.getByText('Transações')).toBeInTheDocument()
    expect(screen.queryByText('Nova despesa')).not.toBeInTheDocument()
    expect(screen.queryByText('Listagem / Filtros')).not.toBeInTheDocument()
    expect(screen.getByText('Relatórios')).toBeInTheDocument()
    expect(screen.getByText('Categorias')).toBeInTheDocument()
    expect(screen.getByText('Ajustes')).toBeInTheDocument()
  })

  it('destaca o item correspondente à rota atual', () => {
    renderSidebar('/transactions')

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
    expect(screen.getByTitle('Ajustes')).toBeInTheDocument()
  })

  it('tem o rodapé "Sua conta / Sair" (FEAT-30)', () => {
    renderSidebar('/')

    expect(screen.getByText('VC')).toBeInTheDocument()
    expect(screen.getByText('Sua conta')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /sair/i })).toBeInTheDocument()
  })

  it('colapsar esconde o texto "Sua conta" mas mantém o logout acessível pelo avatar', async () => {
    const user = userEvent.setup()
    renderSidebar('/')

    await user.click(screen.getByRole('button', { name: /colapsar menu/i }))

    expect(screen.queryByText('Sua conta')).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: /sair/i })).toBeInTheDocument()
  })

  it('clicar em "Sair" chama POST /auth/logout, limpa a sessão e navega para /login', async () => {
    let logoutCalled = false
    server.use(
      http.post(LOGOUT_URL, () => {
        logoutCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    const user = userEvent.setup()
    renderSidebar('/')

    await user.click(screen.getByRole('button', { name: /sair/i }))

    expect(logoutCalled).toBe(true)
    expect(useAuthStore.getState().token).toBeNull()
    expect(await screen.findByText('Login Page')).toBeInTheDocument()
  })
})
