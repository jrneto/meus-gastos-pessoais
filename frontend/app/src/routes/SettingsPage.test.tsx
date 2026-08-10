import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { useAuthStore } from '@/features/auth/store/authStore'
import { SettingsPage } from './SettingsPage'

const LOGOUT_URL = 'http://localhost:5049/auth/logout'

function renderSettingsPage() {
  return render(
    <MemoryRouter initialEntries={['/settings']}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
        <Route path="/settings" element={<SettingsPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SettingsPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
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
    renderSettingsPage()

    await user.click(screen.getByRole('button', { name: /sair/i }))

    expect(logoutCalled).toBe(true)
    expect(useAuthStore.getState().token).toBeNull()
    expect(await screen.findByText('Login Page')).toBeInTheDocument()
  })

  it('exibe a versão do build publicado (rastreabilidade FEAT-09)', () => {
    renderSettingsPage()

    expect(screen.getByText(/versão/i)).toBeInTheDocument()
  })
})
