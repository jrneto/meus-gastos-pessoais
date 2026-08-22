import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { server } from '@/test/msw/server'
import { useAuthStore } from '../store/authStore'
import { LoginForm } from './LoginForm'

const LOGIN_URL = 'http://localhost:5049/auth/login'

function renderLoginForm() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login" element={<LoginForm />} />
        <Route path="/cadastro-em-breve" element={<div>Página de cadastro fake</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('LoginForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
  })

  it('exibe erros de validação inline e não chama a API com campos inválidos', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(LOGIN_URL, () => {
        apiCalled = true
        return HttpResponse.json({ accessToken: 't', expiresIn: 3600, userId: 'u' })
      }),
    )

    renderLoginForm()

    await user.type(screen.getByLabelText('Email'), 'nao-e-email')
    await user.type(screen.getByLabelText('Senha'), '123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Informe um email válido.')).toBeInTheDocument()
    expect(screen.getByText('A senha deve ter no mínimo 8 caracteres.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('login com sucesso popula a authStore e não exibe alerta de erro', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(LOGIN_URL, () =>
        HttpResponse.json({ accessToken: 'tok-123', expiresIn: 3600, userId: 'user-1' }),
      ),
    )

    renderLoginForm()

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    await waitFor(() => expect(useAuthStore.getState().token).toBe('tok-123'))
    expect(screen.queryByText(/não foi possível entrar/i)).not.toBeInTheDocument()
  })

  it('exibe alerta de credenciais inválidas em caso de 401 e não popula a store', async () => {
    const user = userEvent.setup()
    server.use(http.post(LOGIN_URL, () => new HttpResponse(null, { status: 401 })))

    renderLoginForm()

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Email ou senha inválidos.')).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('alternar para "Criar conta" exibe o campo Nome e troca o rótulo do botão, sem chamar a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(LOGIN_URL, () => {
        apiCalled = true
        return HttpResponse.json({ accessToken: 't', expiresIn: 3600, userId: 'u' })
      }),
    )

    renderLoginForm()

    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))

    expect(screen.getByLabelText('Nome')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Criar conta' })).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('submeter o modo "Criar conta" não chama a API de login e navega para a página fake', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(LOGIN_URL, () => {
        apiCalled = true
        return HttpResponse.json({ accessToken: 't', expiresIn: 3600, userId: 'u' })
      }),
    )

    renderLoginForm()

    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await user.type(screen.getByLabelText('Nome'), 'Neto')
    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('Página de cadastro fake')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })
})
