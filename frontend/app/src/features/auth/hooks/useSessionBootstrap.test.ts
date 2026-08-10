import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { useAuthStore } from '../store/authStore'
import { useSessionBootstrap } from './useSessionBootstrap'

const REFRESH_URL = 'http://localhost:5049/auth/refresh'

describe('useSessionBootstrap', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
  })

  it('em caso de sucesso, popula a authStore e termina isBootstrapping', async () => {
    server.use(
      http.post(REFRESH_URL, () =>
        HttpResponse.json({ accessToken: 'tok-novo', expiresIn: 3600, userId: 'user-1' }),
      ),
    )

    const { result } = renderHook(() => useSessionBootstrap())

    expect(result.current.isBootstrapping).toBe(true)

    await waitFor(() => expect(result.current.isBootstrapping).toBe(false))
    expect(useAuthStore.getState().token).toBe('tok-novo')
    expect(useAuthStore.getState().userId).toBe('user-1')
  })

  it('em 401, mantém a authStore vazia e termina isBootstrapping', async () => {
    server.use(http.post(REFRESH_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useSessionBootstrap())

    await waitFor(() => expect(result.current.isBootstrapping).toBe(false))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em falha de rede, mantém a authStore vazia e termina isBootstrapping', async () => {
    server.use(http.post(REFRESH_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useSessionBootstrap())

    await waitFor(() => expect(result.current.isBootstrapping).toBe(false))
    expect(useAuthStore.getState().token).toBeNull()
  })
})
