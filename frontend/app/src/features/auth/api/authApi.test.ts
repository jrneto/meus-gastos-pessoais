import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import {
  AccountNotConfirmedError,
  CpfAlreadyExistsError,
  EmailAlreadyExistsError,
  InvalidConfirmationCodeError,
  InvalidCredentialsError,
  NetworkError,
  RefreshFailedError,
  RegisterValidationError,
  UnknownAuthError,
} from '../errors/authErrors'
import { authApi } from './authApi'

const REFRESH_URL = 'http://localhost:5049/auth/refresh'
const LOGOUT_URL = 'http://localhost:5049/auth/logout'
const LOGIN_URL = 'http://localhost:5049/auth/login'
const REGISTER_URL = 'http://localhost:5049/auth/register'
const CONFIRM_URL = 'http://localhost:5049/auth/confirm'
const RESEND_URL = 'http://localhost:5049/auth/resend-confirmation'

const registerPayload = {
  email: 'fulano@email.com',
  password: 'Senha123',
  name: 'Fulano da Silva',
  phoneNumber: '11999998888',
  cpf: '12345678909',
}

function problem(type: string) {
  return HttpResponse.json(
    { status: 409, title: 'Conflito', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 409 },
  )
}

describe('authApi.login', () => {
  it('em 401 sem corpo, lança InvalidCredentialsError (comportamento já existente)', async () => {
    server.use(http.post(LOGIN_URL, () => new HttpResponse(null, { status: 401 })))

    await expect(authApi.login({ email: 'a@a.com', password: 'Senha123' })).rejects.toBeInstanceOf(
      InvalidCredentialsError,
    )
  })

  it('em 401 com type invalid-credentials, lança InvalidCredentialsError', async () => {
    server.use(
      http.post(LOGIN_URL, () =>
        HttpResponse.json(
          { status: 401, title: 'Não autorizado', detail: '...', type: 'https://gastosapp.dev/errors/invalid-credentials' },
          { status: 401 },
        ),
      ),
    )

    await expect(authApi.login({ email: 'a@a.com', password: 'Senha123' })).rejects.toBeInstanceOf(
      InvalidCredentialsError,
    )
  })

  it('em 401 com type user-not-confirmed, lança AccountNotConfirmedError', async () => {
    server.use(
      http.post(LOGIN_URL, () =>
        HttpResponse.json(
          { status: 401, title: 'Não autorizado', detail: '...', type: 'https://gastosapp.dev/errors/user-not-confirmed' },
          { status: 401 },
        ),
      ),
    )

    await expect(authApi.login({ email: 'a@a.com', password: 'Senha123' })).rejects.toBeInstanceOf(
      AccountNotConfirmedError,
    )
  })
})

describe('authApi.register', () => {
  it('em caso de sucesso, retorna o usuário criado', async () => {
    const created = { userId: 'uuid-1', ...registerPayload }
    server.use(http.post(REGISTER_URL, () => HttpResponse.json(created, { status: 201 })))

    const result = await authApi.register(registerPayload)

    expect(result).toEqual(created)
  })

  it('em 409 email-already-exists, lança EmailAlreadyExistsError', async () => {
    server.use(http.post(REGISTER_URL, () => problem('email-already-exists')))

    await expect(authApi.register(registerPayload)).rejects.toBeInstanceOf(EmailAlreadyExistsError)
  })

  it('em 409 cpf-already-exists, lança CpfAlreadyExistsError', async () => {
    server.use(http.post(REGISTER_URL, () => problem('cpf-already-exists')))

    await expect(authApi.register(registerPayload)).rejects.toBeInstanceOf(CpfAlreadyExistsError)
  })

  it('em 400, lança RegisterValidationError', async () => {
    server.use(http.post(REGISTER_URL, () => new HttpResponse(null, { status: 400 })))

    await expect(authApi.register(registerPayload)).rejects.toBeInstanceOf(RegisterValidationError)
  })

  it('em erro de rede, lança NetworkError', async () => {
    server.use(http.post(REGISTER_URL, () => HttpResponse.error()))

    await expect(authApi.register(registerPayload)).rejects.toThrow('Não foi possível conectar à API')
  })

  it('em erro inesperado (5xx), lança UnknownAuthError', async () => {
    server.use(http.post(REGISTER_URL, () => new HttpResponse(null, { status: 500 })))

    await expect(authApi.register(registerPayload)).rejects.toBeInstanceOf(UnknownAuthError)
  })
})

describe('authApi.confirm', () => {
  it('em caso de sucesso, resolve sem lançar', async () => {
    server.use(http.post(CONFIRM_URL, () => new HttpResponse(null, { status: 200 })))

    await expect(authApi.confirm({ email: 'fulano@email.com', code: '123456' })).resolves.toBeUndefined()
  })

  it('em 400 invalid-confirmation-code, lança InvalidConfirmationCodeError', async () => {
    server.use(
      http.post(CONFIRM_URL, () =>
        HttpResponse.json(
          { status: 400, title: '...', detail: '...', type: 'https://gastosapp.dev/errors/invalid-confirmation-code' },
          { status: 400 },
        ),
      ),
    )

    await expect(authApi.confirm({ email: 'fulano@email.com', code: '000000' })).rejects.toBeInstanceOf(
      InvalidConfirmationCodeError,
    )
  })

  it('em 400 expired-confirmation-code, lança InvalidConfirmationCodeError', async () => {
    server.use(
      http.post(CONFIRM_URL, () =>
        HttpResponse.json(
          { status: 400, title: '...', detail: '...', type: 'https://gastosapp.dev/errors/expired-confirmation-code' },
          { status: 400 },
        ),
      ),
    )

    await expect(authApi.confirm({ email: 'fulano@email.com', code: '000000' })).rejects.toBeInstanceOf(
      InvalidConfirmationCodeError,
    )
  })

  it('em erro de rede, lança NetworkError', async () => {
    server.use(http.post(CONFIRM_URL, () => HttpResponse.error()))

    await expect(authApi.confirm({ email: 'fulano@email.com', code: '123456' })).rejects.toBeInstanceOf(
      NetworkError,
    )
  })
})

describe('authApi.resendConfirmation', () => {
  it('em caso de sucesso, resolve sem lançar', async () => {
    server.use(http.post(RESEND_URL, () => new HttpResponse(null, { status: 200 })))

    await expect(authApi.resendConfirmation({ email: 'fulano@email.com' })).resolves.toBeUndefined()
  })

  it('em erro de rede, lança NetworkError', async () => {
    server.use(http.post(RESEND_URL, () => HttpResponse.error()))

    await expect(authApi.resendConfirmation({ email: 'fulano@email.com' })).rejects.toBeInstanceOf(NetworkError)
  })
})

describe('authApi.refresh', () => {
  it('em caso de sucesso, retorna accessToken/expiresIn/userId', async () => {
    server.use(
      http.post(REFRESH_URL, () =>
        HttpResponse.json({ accessToken: 'tok-novo', expiresIn: 3600, userId: 'user-1' }),
      ),
    )

    const result = await authApi.refresh()

    expect(result).toEqual({ accessToken: 'tok-novo', expiresIn: 3600, userId: 'user-1' })
  })

  it('em 401 (cookie ausente/expirado/inválido), lança RefreshFailedError', async () => {
    server.use(http.post(REFRESH_URL, () => new HttpResponse(null, { status: 401 })))

    await expect(authApi.refresh()).rejects.toBeInstanceOf(RefreshFailedError)
  })

  it('em falha de rede, lança NetworkError', async () => {
    server.use(http.post(REFRESH_URL, () => HttpResponse.error()))

    await expect(authApi.refresh()).rejects.toThrow('Não foi possível conectar à API')
  })

  it('em erro inesperado (5xx), lança UnknownAuthError', async () => {
    server.use(http.post(REFRESH_URL, () => new HttpResponse(null, { status: 500 })))

    await expect(authApi.refresh()).rejects.toBeInstanceOf(UnknownAuthError)
  })
})

describe('authApi.logout', () => {
  it('em caso de sucesso, resolve sem lançar', async () => {
    server.use(http.post(LOGOUT_URL, () => new HttpResponse(null, { status: 200 })))

    await expect(authApi.logout()).resolves.toBeUndefined()
  })

  it('em falha, lança erro (tratamento fica a cargo do chamador)', async () => {
    server.use(http.post(LOGOUT_URL, () => new HttpResponse(null, { status: 500 })))

    await expect(authApi.logout()).rejects.toBeInstanceOf(UnknownAuthError)
  })
})
