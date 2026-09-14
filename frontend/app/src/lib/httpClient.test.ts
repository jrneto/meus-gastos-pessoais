import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { server } from '@/test/msw/server'
import { startNewSession } from './sessionId'
import { httpClient, registerAuthPlugin, resetAuthCircuitBreaker } from './httpClient'

const BASE_URL = 'http://localhost:5049'
const RESOURCE_URL = `${BASE_URL}/protected/resource`

describe('httpClient — headers de observabilidade (FEAT-38)', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('envia trace-id (um valor novo por chamada), client-platform e client-version em toda requisição', async () => {
    const received: { traceIds: string[]; platform: string | null; version: string | null } = {
      traceIds: [],
      platform: null,
      version: null,
    }
    server.use(
      http.get(RESOURCE_URL, ({ request }) => {
        received.traceIds.push(request.headers.get('trace-id')!)
        received.platform = request.headers.get('client-platform')
        received.version = request.headers.get('client-version')
        return HttpResponse.json({ ok: true })
      }),
    )
    vi.stubEnv('VITE_APP_VERSION', 'v1.4.0')

    await httpClient.get('/protected/resource')
    await httpClient.get('/protected/resource')

    expect(received.traceIds).toHaveLength(2)
    expect(received.traceIds[0]).not.toBe(received.traceIds[1])
    expect(received.platform).toBe('web')
    expect(received.version).toBe('v1.4.0')
  })

  it('não envia session-id quando ainda não há sessão', async () => {
    let receivedSessionId: string | null = 'not-called'
    server.use(
      http.get(RESOURCE_URL, ({ request }) => {
        receivedSessionId = request.headers.get('session-id')
        return HttpResponse.json({ ok: true })
      }),
    )

    await httpClient.get('/protected/resource')

    expect(receivedSessionId).toBeNull()
  })

  it('envia session-id quando há uma sessão iniciada', async () => {
    const sessionId = startNewSession()
    let receivedSessionId: string | null = null
    server.use(
      http.get(RESOURCE_URL, ({ request }) => {
        receivedSessionId = request.headers.get('session-id')
        return HttpResponse.json({ ok: true })
      }),
    )

    await httpClient.get('/protected/resource')

    expect(receivedSessionId).toBe(sessionId)
  })
})

describe('httpClient — plugin de auth', () => {
  beforeEach(() => {
    // Plugin "neutro" por padrão — cada teste sobrescreve o que precisa.
    registerAuthPlugin({
      getAccessToken: () => null,
      refreshAccessToken: vi.fn(),
      onSessionExpired: vi.fn(),
    })
    // O disjuntor de refresh (módulo compartilhado) não pode vazar
    // estado de um teste que o aciona (ex.: o de loop, abaixo) para o
    // próximo — mesmo racional do reset em `useLogin.ts` para sessões
    // reais.
    resetAuthCircuitBreaker()
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

  it('em 401 persistente mesmo após refresh bem-sucedido (loop), para de tentar depois de algumas tentativas em vez de renovar pra sempre', async () => {
    let refreshCount = 0
    server.use(http.get(RESOURCE_URL, () => new HttpResponse(null, { status: 401 })))
    const onSessionExpired = vi.fn()
    const refreshAccessToken = vi.fn(async () => {
      refreshCount += 1
      return `new-token-${refreshCount}`
    })
    registerAuthPlugin({
      getAccessToken: () => 'stale-token',
      refreshAccessToken,
      onSessionExpired,
    })

    // Simula chamadas sucessivas como as de hooks independentes que
    // reagem à troca de `token` no authStore a cada refresh — sem o
    // disjuntor, cada uma dispararia um novo refresh indefinidamente.
    for (let i = 0; i < 6; i += 1) {
      // eslint-disable-next-line no-await-in-loop
      await httpClient.get('/protected/resource')
    }

    expect(refreshAccessToken.mock.calls.length).toBeLessThan(6)
    expect(onSessionExpired).toHaveBeenCalled()
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
