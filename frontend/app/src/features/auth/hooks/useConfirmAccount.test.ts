import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { InvalidConfirmationCodeError, NetworkError } from '../errors/authErrors'
import { useConfirmAccount } from './useConfirmAccount'

const CONFIRM_URL = 'http://localhost:5049/auth/confirm'
const payload = { email: 'fulano@email.com', code: '123456' }

describe('useConfirmAccount', () => {
  it('em caso de sucesso, liga success e não deixa erro', async () => {
    server.use(http.post(CONFIRM_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useConfirmAccount())

    await act(async () => {
      await result.current.confirm(payload)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
  })

  it('em código incorreto (400), popula error e mantém success false', async () => {
    server.use(
      http.post(CONFIRM_URL, () =>
        HttpResponse.json(
          { status: 400, title: '...', detail: '...', type: 'https://gastosapp.dev/errors/invalid-confirmation-code' },
          { status: 400 },
        ),
      ),
    )

    const { result } = renderHook(() => useConfirmAccount())

    await act(async () => {
      await result.current.confirm(payload)
    })

    expect(result.current.error).toBeInstanceOf(InvalidConfirmationCodeError)
    expect(result.current.success).toBe(false)
  })

  it('em falha de rede, expõe NetworkError', async () => {
    server.use(http.post(CONFIRM_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useConfirmAccount())

    await act(async () => {
      await result.current.confirm(payload)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
    expect(result.current.success).toBe(false)
  })

  it('isLoading fica true durante a chamada e volta a false ao final', async () => {
    server.use(http.post(CONFIRM_URL, () => new HttpResponse(null, { status: 200 })))

    const { result } = renderHook(() => useConfirmAccount())

    let confirmPromise!: Promise<void>
    act(() => {
      confirmPromise = result.current.confirm(payload)
    })
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      await confirmPromise
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
