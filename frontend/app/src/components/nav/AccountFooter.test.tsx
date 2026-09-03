import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { AccountFooter } from './AccountFooter'

const LOGOUT_URL = 'http://localhost:5049/auth/logout'

function renderFooter(props: Parameters<typeof AccountFooter>[0] = {}) {
  return render(
    <MemoryRouter initialEntries={['/']}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
        <Route path="/" element={<AccountFooter {...props} />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AccountFooter', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('renderiza o avatar e o rótulo "Sua conta"', () => {
    renderFooter()

    expect(screen.getByText('VC')).toBeInTheDocument()
    expect(screen.getByText('Sua conta')).toBeInTheDocument()
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
    renderFooter()

    await user.click(screen.getByRole('button', { name: /sair/i }))

    expect(logoutCalled).toBe(true)
    expect(useAuthStore.getState().token).toBeNull()
    expect(await screen.findByText('Login Page')).toBeInTheDocument()
  })

  it('chama onBeforeLogout antes de navegar (uso do NavMoreSheet, que fecha o painel)', async () => {
    server.use(http.post(LOGOUT_URL, () => new HttpResponse(null, { status: 200 })))
    let calledBefore = false
    const user = userEvent.setup()
    renderFooter({ onBeforeLogout: () => { calledBefore = true } })

    await user.click(screen.getByRole('button', { name: /sair/i }))

    expect(calledBefore).toBe(true)
    expect(await screen.findByText('Login Page')).toBeInTheDocument()
  })

  it('modo colapsado: mostra só o avatar como botão de logout, sem o texto "Sua conta"', async () => {
    server.use(http.post(LOGOUT_URL, () => new HttpResponse(null, { status: 200 })))
    const user = userEvent.setup()
    renderFooter({ collapsed: true })

    expect(screen.queryByText('Sua conta')).not.toBeInTheDocument()
    const logoutButton = screen.getByRole('button', { name: /sair/i })

    await user.click(logoutButton)

    expect(await screen.findByText('Login Page')).toBeInTheDocument()
  })
})
