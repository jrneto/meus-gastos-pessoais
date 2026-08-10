import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { RefreshFailedError, UnknownAuthError } from '../errors/authErrors'
import { authApi } from './authApi'

const REFRESH_URL = 'http://localhost:5049/auth/refresh'
const LOGOUT_URL = 'http://localhost:5049/auth/logout'

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
