import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { NetworkError } from '../errors/authErrors'
import { useResendConfirmation } from './useResendConfirmation'

const RESEND_URL = 'http://localhost:5049/auth/resend-confirmation'
const payload = { email: 'fulano@email.com' }

describe('useResendConfirmation', () => {
  it('em caso de sucesso, desliga isLoading sem popular error', async () => {
    server.use(http.post(RESEND_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useResendConfirmation())

    await act(async () => {
      await result.current.resend(payload)
    })

    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
  })

  it('em falha de rede, popula error', async () => {
    server.use(http.post(RESEND_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useResendConfirmation())

    await act(async () => {
      await result.current.resend(payload)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
  })

  it('isLoading fica true durante a chamada e volta a false ao final', async () => {
    server.use(http.post(RESEND_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useResendConfirmation())

    let resendPromise!: Promise<void>
    act(() => {
      resendPromise = result.current.resend(payload)
    })
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      await resendPromise
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
