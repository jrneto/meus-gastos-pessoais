import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { SettingsPage } from './SettingsPage'

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

  it('clicar em "Sair" limpa a sessão e navega para /login', async () => {
    const user = userEvent.setup()
    renderSettingsPage()

    await user.click(screen.getByRole('button', { name: /sair/i }))

    expect(useAuthStore.getState().token).toBeNull()
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })

  it('exibe a versão do build publicado (rastreabilidade FEAT-09)', () => {
    renderSettingsPage()

    expect(screen.getByText(/versão/i)).toBeInTheDocument()
  })
})
