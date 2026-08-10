import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { server } from '@/test/msw/server'
import { httpClient, registerAuthPlugin } from './httpClient'

const BASE_URL = 'http://localhost:5049'
const RESOURCE_URL = `${BASE_URL}/protected/resource`

describe('httpClient — plugin de auth', () => {
  beforeEach(() => {
    // Plugin "neutro" por padrão — cada teste sobrescreve o que precisa.
    registerAuthPlugin({
      getAccessToken: () => null,
      refreshAccessToken: vi.fn(),
      onSessionExpired: vi.fn(),
    })
  })

  it('injeta Authorization automaticamente a partir de getAccessToken', async () => {
    let receivedAuth: string | null = null
    server.use(
      http.get(RESOURCE_URL, ({ request }) => {
        receivedAuth = request.headers.get('Authorization')
        return HttpResponse.json({ ok: true })
      }),
    )
    registerAuthPlugin({
      getAccessToken: () => 'tok-abc',
      refreshAccessToken: vi.fn(),
      onSessionExpired: vi.fn(),
    })

    await httpClient.get('/protected/resource')

    expect(receivedAuth).toBe('Bearer tok-abc')
  })

  it('em 401, renova via refreshAccessToken e repete a chamada original com o token novo', async () => {
    let currentToken = 'old-token'
    let callCount = 0
    server.use(
      http.get(RESOURCE_URL, ({ request }) => {
        callCount += 1
        const auth = request.headers.get('Authorization')
        if (auth !== 'Bearer new-token') {
          return new HttpResponse(null, { status: 401 })
        }
        return HttpResponse.json({ ok: true })
      }),
    )
    const refreshAccessToken = vi.fn(async () => {
      currentToken = 'new-token'
      return currentToken
    })
    registerAuthPlugin({
      getAccessToken: () => currentToken,
      refreshAccessToken,
      onSessionExpired: vi.fn(),
    })

    const response = await httpClient.get('/protected/resource')

    expect(response.status).toBe(200)
    expect(callCount).toBe(2)
    expect(refreshAccessToken).toHaveBeenCalledTimes(1)
  })

  it('em 401 com refresh recusado (null), chama onSessionExpired e devolve o 401 original sem retry', async () => {
    let callCount = 0
    server.use(
      http.get(RESOURCE_URL, () => {
        callCount += 1
        return new HttpResponse(null, { status: 401 })
      }),
    )
    const onSessionExpired = vi.fn()
    registerAuthPlugin({
      getAccessToken: () => 'expired-token',
      refreshAccessToken: vi.fn(async () => null),
      onSessionExpired,
    })

    const response = await httpClient.get('/protected/resource')

    expect(response.status).toBe(401)
    expect(callCount).toBe(1)
    expect(onSessionExpired).toHaveBeenCalledTimes(1)
  })

  it('em 401 com falha de rede no refresh, propaga a exceção sem chamar onSessionExpired', async () => {
    server.use(http.get(RESOURCE_URL, () => new HttpResponse(null, { status: 401 })))
    const onSessionExpired = vi.fn()
    registerAuthPlugin({
      getAccessToken: () => 'expired-token',
      refreshAccessToken: vi.fn(async () => {
        throw new Error('network down')
      }),
      onSessionExpired,
    })

    await expect(httpClient.get('/protected/resource')).rejects.toThrow('network down')
    expect(onSessionExpired).not.toHaveBeenCalled()
  })

  it('deduplica refreshes concorrentes — várias chamadas 401 disparam só um refresh', async () => {
    let currentToken = 'old-token'
    server.use(
      http.get(RESOURCE_URL, ({ request }) => {
        const auth = request.headers.get('Authorization')
        if (auth !== 'Bearer new-token') {
          return new HttpResponse(null, { status: 401 })
        }
        return HttpResponse.json({ ok: true })
      }),
    )
    const refreshAccessToken = vi.fn(async () => {
      await new Promise((resolve) => setTimeout(resolve, 10))
      currentToken = 'new-token'
      return currentToken
    })
    registerAuthPlugin({
      getAccessToken: () => currentToken,
      refreshAccessToken,
      onSessionExpired: vi.fn(),
    })

    const responses = await Promise.all([
      httpClient.get('/protected/resource'),
      httpClient.get('/protected/resource'),
      httpClient.get('/protected/resource'),
    ])

    expect(responses.every((r) => r.status === 200)).toBe(true)
    expect(refreshAccessToken).toHaveBeenCalledTimes(1)
  })
})
