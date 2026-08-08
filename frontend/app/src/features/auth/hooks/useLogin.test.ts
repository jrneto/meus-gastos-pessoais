import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { InvalidCredentialsError, NetworkError } from '../errors/authErrors'
import { useAuthStore } from '../store/authStore'
import { useLogin } from './useLogin'

const LOGIN_URL = 'http://localhost:5049/auth/login'
const credentials = { email: 'neto@email.com', password: 'Senha123' }

describe('useLogin', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
  })

  it('em caso de sucesso, popula a authStore e não deixa erro', async () => {
    server.use(
      http.post(LOGIN_URL, () =>
        HttpResponse.json({ accessToken: 'tok-123', expiresIn: 3600, userId: 'user-1' }),
      ),
    )

    const { result } = renderHook(() => useLogin())

    await act(async () => {
      await result.current.login(credentials)
    })

    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
    expect(useAuthStore.getState().token).toBe('tok-123')
    expect(useAuthStore.getState().userId).toBe('user-1')
  })

  it('em caso de 401, expõe InvalidCredentialsError e não popula a store', async () => {
    server.use(http.post(LOGIN_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useLogin())

    await act(async () => {
      await result.current.login(credentials)
    })

    expect(result.current.error).toBeInstanceOf(InvalidCredentialsError)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError e não popula a store', async () => {
    server.use(http.post(LOGIN_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useLogin())

    await act(async () => {
      await result.current.login(credentials)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('isLoading fica true durante a chamada e volta a false ao final', async () => {
    server.use(
      http.post(LOGIN_URL, () =>
        HttpResponse.json({ accessToken: 'tok-123', expiresIn: 3600, userId: 'user-1' }),
      ),
    )

    const { result } = renderHook(() => useLogin())

    let loginPromise!: Promise<void>
    act(() => {
      loginPromise = result.current.login(credentials)
    })
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      await loginPromise
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
