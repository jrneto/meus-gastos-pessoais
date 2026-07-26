import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { useAuthStore } from '../store/authStore'
import { LoginForm } from './LoginForm'

const LOGIN_URL = 'http://localhost:5049/auth/login'

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

    render(<LoginForm />)

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

    render(<LoginForm />)

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    await waitFor(() => expect(useAuthStore.getState().token).toBe('tok-123'))
    expect(screen.queryByText(/não foi possível entrar/i)).not.toBeInTheDocument()
  })

  it('exibe alerta de credenciais inválidas em caso de 401 e não popula a store', async () => {
    const user = userEvent.setup()
    server.use(http.post(LOGIN_URL, () => new HttpResponse(null, { status: 401 })))

    render(<LoginForm />)

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Email ou senha inválidos.')).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })
})
