import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NavMoreSheet } from './NavMoreSheet'

const LOGOUT_URL = 'http://localhost:5049/auth/logout'

function renderSheet(open: boolean, onOpenChange: (open: boolean) => void) {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
        <Route path="/" element={<NavMoreSheet open={open} onOpenChange={onOpenChange} />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('NavMoreSheet', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('não renderiza nada quando `open` é false', () => {
    renderSheet(false, vi.fn())

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('lista Relatórios e Categorias quando aberto', () => {
    renderSheet(true, vi.fn())

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByRole('link', { name: /relatórios/i })).toBeInTheDocument()
    expect(within(dialog).getByRole('link', { name: /categorias/i })).toBeInTheDocument()
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    renderSheet(true, onOpenChange)

    await user.click(screen.getByRole('dialog').parentElement!)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('não fecha ao clicar no título do painel', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    renderSheet(true, onOpenChange)

    await user.click(screen.getByText('Mais'))

    expect(onOpenChange).not.toHaveBeenCalled()
  })

  it('fecha ao clicar em um item de navegação', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    renderSheet(true, onOpenChange)

    await user.click(screen.getByRole('link', { name: /categorias/i }))

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao pressionar Esc', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    renderSheet(true, onOpenChange)

    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('tem o rodapé "Sua conta / Sair" (FEAT-30)', () => {
    renderSheet(true, vi.fn())

    const dialog = screen.getByRole('dialog')
    expect(within(dialog).getByText('Sua conta')).toBeInTheDocument()
    expect(within(dialog).getByRole('button', { name: /sair/i })).toBeInTheDocument()
  })

  it('clicar em "Sair" fecha o painel, chama POST /auth/logout, limpa a sessão e navega para /login', async () => {
    let logoutCalled = false
    server.use(
      http.post(LOGOUT_URL, () => {
        logoutCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    const user = userEvent.setup()
    const onOpenChange = vi.fn()
    renderSheet(true, onOpenChange)

    await user.click(screen.getByRole('button', { name: /sair/i }))

    expect(onOpenChange).toHaveBeenCalledWith(false)
    expect(logoutCalled).toBe(true)
    expect(useAuthStore.getState().token).toBeNull()
    expect(await screen.findByText('Login Page')).toBeInTheDocument()
  })
})
