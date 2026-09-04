import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { useAuthStore } from '../store/authStore'
import { LoginForm } from './LoginForm'

const LOGIN_URL = 'http://localhost:5049/auth/login'
const REGISTER_URL = 'http://localhost:5049/auth/register'
const CONFIRM_URL = 'http://localhost:5049/auth/confirm'
const RESEND_URL = 'http://localhost:5049/auth/resend-confirmation'

function renderLoginForm() {
  return render(<LoginForm />)
}

function problem(status: number, type: string) {
  return HttpResponse.json({ status, title: '...', detail: '...', type: `https://gastosapp.dev/errors/${type}` }, { status })
}

async function fillValidSignupForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Nome'), 'Fulano da Silva')
  await user.type(screen.getByLabelText('CPF'), '12345678909')
  await user.type(screen.getByLabelText('Telefone'), '11999998888')
  await user.type(screen.getByLabelText('Email'), 'fulano@email.com')
  await user.type(screen.getByLabelText('Senha'), 'Senha123')
}

describe('LoginForm — modo Entrar', () => {
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

  it('exibe alerta de credenciais inválidas em caso de 401 sem type e não popula a store', async () => {
    const user = userEvent.setup()
    server.use(http.post(LOGIN_URL, () => new HttpResponse(null, { status: 401 })))

    renderLoginForm()

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(await screen.findByText('Email ou senha inválidos.')).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('exibe alerta de conta não confirmada e o CTA "Confirmar cadastro" em caso de 401 user-not-confirmed', async () => {
    const user = userEvent.setup()
    server.use(http.post(LOGIN_URL, () => problem(401, 'user-not-confirmed')))

    renderLoginForm()

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))

    expect(
      await screen.findByText('Confirme seu cadastro pelo código enviado por e-mail antes de entrar.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Confirmar cadastro' })).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('clicar em "Confirmar cadastro" abre a tela de confirmação com o email do login e dispara reenvio automático', async () => {
    const user = userEvent.setup()
    let resendCalled = false
    server.use(
      http.post(LOGIN_URL, () => problem(401, 'user-not-confirmed')),
      http.post(RESEND_URL, () => {
        resendCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )

    renderLoginForm()

    await user.type(screen.getByLabelText('Email'), 'neto@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Entrar' }))
    await user.click(await screen.findByRole('button', { name: 'Confirmar cadastro' }))

    expect(screen.getByText(/Enviamos um código de 6 dígitos para/)).toBeInTheDocument()
    expect(screen.getByText('neto@email.com')).toBeInTheDocument()
    await waitFor(() => expect(resendCalled).toBe(true))
  })
})

describe('LoginForm — modo Criar conta', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
  })

  it('alternar para "Criar conta" exibe os campos do cadastro e troca o rótulo do botão, sem chamar a API de login', async () => {
    const user = userEvent.setup()
    let loginCalled = false
    server.use(
      http.post(LOGIN_URL, () => {
        loginCalled = true
        return HttpResponse.json({ accessToken: 't', expiresIn: 3600, userId: 'u' })
      }),
    )

    renderLoginForm()

    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))

    expect(screen.getByLabelText('Nome')).toBeInTheDocument()
    expect(screen.getByLabelText('CPF')).toBeInTheDocument()
    expect(screen.getByLabelText('Telefone')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Criar conta' })).toBeInTheDocument()
    expect(loginCalled).toBe(false)
  })

  it('aplica máscara progressiva em CPF e Telefone durante a digitação', async () => {
    const user = userEvent.setup()
    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))

    await user.type(screen.getByLabelText('CPF'), '12345678909')
    await user.type(screen.getByLabelText('Telefone'), '11999998888')

    expect(screen.getByLabelText('CPF')).toHaveValue('123.456.789-09')
    expect(screen.getByLabelText('Telefone')).toHaveValue('(11) 99999-8888')
  })

  it('cadastro com sucesso navega direto pra tela de confirmação (sem tela de "aguarde aprovação")', async () => {
    const user = userEvent.setup()
    let loginCalled = false
    server.use(
      http.post(LOGIN_URL, () => {
        loginCalled = true
        return HttpResponse.json({ accessToken: 't', expiresIn: 3600, userId: 'u' })
      }),
      http.post(REGISTER_URL, () =>
        HttpResponse.json(
          { userId: 'uuid-1', email: 'fulano@email.com', name: 'Fulano da Silva', phoneNumber: '11999998888', cpf: '12345678909' },
          { status: 201 },
        ),
      ),
    )

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await fillValidSignupForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('fulano@email.com')).toBeInTheDocument()
    expect(screen.getByLabelText('Dígito 1 do código')).toBeInTheDocument()
    expect(loginCalled).toBe(false)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('voltar da tela de confirmação retorna ao modo "Entrar"', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(REGISTER_URL, () =>
        HttpResponse.json(
          { userId: 'uuid-1', email: 'fulano@email.com', name: 'Fulano da Silva', phoneNumber: '11999998888', cpf: '12345678909' },
          { status: 201 },
        ),
      ),
    )

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await fillValidSignupForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))
    await screen.findByLabelText('Dígito 1 do código')

    await user.click(screen.getByRole('button', { name: '← Voltar' }))

    expect(screen.getByRole('button', { name: 'Entrar' })).toBeInTheDocument()
  })

  it('confirmar o código com sucesso volta ao login com o aviso e o email preenchido', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(REGISTER_URL, () =>
        HttpResponse.json(
          { userId: 'uuid-1', email: 'fulano@email.com', name: 'Fulano da Silva', phoneNumber: '11999998888', cpf: '12345678909' },
          { status: 201 },
        ),
      ),
      http.post(CONFIRM_URL, () => new HttpResponse(null, { status: 200 })),
    )

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await fillValidSignupForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))
    await screen.findByLabelText('Dígito 1 do código')

    for (let i = 1; i <= 6; i++) {
      await user.type(screen.getByLabelText(`Dígito ${i} do código`), String(i))
    }
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    expect(
      await screen.findByText('Email confirmado. Sua conta está ativa — entre com seus dados.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Email')).toHaveValue('fulano@email.com')
  })

  it('em 409 email-already-exists, exibe mensagem específica', async () => {
    const user = userEvent.setup()
    server.use(http.post(REGISTER_URL, () => problem(409, 'email-already-exists')))

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await fillValidSignupForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('Este email já está cadastrado.')).toBeInTheDocument()
  })

  it('em 409 cpf-already-exists, exibe mensagem específica', async () => {
    const user = userEvent.setup()
    server.use(http.post(REGISTER_URL, () => problem(409, 'cpf-already-exists')))

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await fillValidSignupForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('Este CPF já está cadastrado.')).toBeInTheDocument()
  })

  it('em erro de rede, exibe mensagem de rede e mantém os campos preenchidos', async () => {
    const user = userEvent.setup()
    server.use(http.post(REGISTER_URL, () => HttpResponse.error()))

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await fillValidSignupForm(user)
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('Não foi possível conectar à API. Verifique sua conexão.')).toBeInTheDocument()
    expect(screen.getByLabelText('Nome')).toHaveValue('Fulano da Silva')
  })

  it('bloqueia submit com CPF inválido, sem chamar a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(REGISTER_URL, () => {
        apiCalled = true
        return HttpResponse.json({}, { status: 201 })
      }),
    )

    renderLoginForm()
    await user.click(screen.getByRole('radio', { name: 'Criar conta' }))
    await user.type(screen.getByLabelText('Nome'), 'Fulano da Silva')
    await user.type(screen.getByLabelText('CPF'), '11111111111')
    await user.type(screen.getByLabelText('Telefone'), '11999998888')
    await user.type(screen.getByLabelText('Email'), 'fulano@email.com')
    await user.type(screen.getByLabelText('Senha'), 'Senha123')
    await user.click(screen.getByRole('button', { name: 'Criar conta' }))

    expect(await screen.findByText('CPF inválido.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })
})
