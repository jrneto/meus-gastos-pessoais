import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { LoginPage } from './LoginPage'

function renderLoginPage() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/" element={<div>Home autenticada</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('LoginPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
  })

  it('exibe a wordmark e o formulário de login quando não autenticado', () => {
    renderLoginPage()

    expect(
      screen.getByText((_, element) => element?.textContent === 'jrn.'),
    ).toBeInTheDocument()
    expect(screen.getByText('expenses')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument()
  })

  it('redireciona para a rota inicial quando já autenticado', async () => {
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)

    renderLoginPage()

    expect(await screen.findByText('Home autenticada')).toBeInTheDocument()
  })
})
