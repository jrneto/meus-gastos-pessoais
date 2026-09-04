import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { NetworkError } from '../errors/authErrors'
import { useForgotPassword } from './useForgotPassword'

const FORGOT_PASSWORD_URL = 'http://localhost:5049/auth/forgot-password'
const payload = { email: 'fulano@email.com' }

describe('useForgotPassword', () => {
  it('em caso de sucesso, desliga isLoading sem popular error', async () => {
    server.use(http.post(FORGOT_PASSWORD_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useForgotPassword())

    await act(async () => {
      await result.current.forgotPassword(payload)
    })

    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
  })

  it('em falha de rede, popula error', async () => {
    server.use(http.post(FORGOT_PASSWORD_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useForgotPassword())

    await act(async () => {
      await result.current.forgotPassword(payload)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
  })

  it('isLoading fica true durante a chamada e volta a false ao final', async () => {
    server.use(http.post(FORGOT_PASSWORD_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useForgotPassword())

    let forgotPasswordPromise!: Promise<void>
    act(() => {
      forgotPasswordPromise = result.current.forgotPassword(payload)
    })
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      await forgotPasswordPromise
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
