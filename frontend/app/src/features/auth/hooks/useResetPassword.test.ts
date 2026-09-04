import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { InvalidResetCodeError, NetworkError, WeakPasswordError } from '../errors/authErrors'
import { useResetPassword } from './useResetPassword'

const RESET_PASSWORD_URL = 'http://localhost:5049/auth/reset-password'
const payload = { email: 'fulano@email.com', code: '123456', newPassword: 'Senha123@' }

function problem(status: number, type: string) {
  return HttpResponse.json({ status, title: '...', detail: '...', type: `https://gastosapp.dev/errors/${type}` }, { status })
}

describe('useResetPassword', () => {
  it('em caso de sucesso, liga success e não deixa erro', async () => {
    server.use(http.post(RESET_PASSWORD_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useResetPassword())

    await act(async () => {
      await result.current.resetPassword(payload)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
  })

  it('em código inválido/expirado (400), popula error com InvalidResetCodeError e mantém success false', async () => {
    server.use(http.post(RESET_PASSWORD_URL, () => problem(400, 'invalid-reset-code')))

    const { result } = renderHook(() => useResetPassword())

    await act(async () => {
      await result.current.resetPassword(payload)
    })

    expect(result.current.error).toBeInstanceOf(InvalidResetCodeError)
    expect(result.current.success).toBe(false)
  })

  it('em senha fora da política (400 bad-request), popula error com WeakPasswordError e mantém success false', async () => {
    server.use(http.post(RESET_PASSWORD_URL, () => problem(400, 'bad-request')))

    const { result } = renderHook(() => useResetPassword())

    await act(async () => {
      await result.current.resetPassword(payload)
    })

    expect(result.current.error).toBeInstanceOf(WeakPasswordError)
    expect(result.current.success).toBe(false)
  })

  it('em falha de rede, expõe NetworkError', async () => {
    server.use(http.post(RESET_PASSWORD_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useResetPassword())

    await act(async () => {
      await result.current.resetPassword(payload)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
    expect(result.current.success).toBe(false)
  })

  it('isLoading fica true durante a chamada e volta a false ao final', async () => {
    server.use(http.post(RESET_PASSWORD_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useResetPassword())

    let resetPasswordPromise!: Promise<void>
    act(() => {
      resetPasswordPromise = result.current.resetPassword(payload)
    })
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      await resetPasswordPromise
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
