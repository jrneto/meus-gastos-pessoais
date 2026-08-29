import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { EmailAlreadyExistsError, NetworkError } from '../errors/authErrors'
import { useRegister } from './useRegister'

const REGISTER_URL = 'http://localhost:5049/auth/register'
const payload = {
  email: 'fulano@email.com',
  password: 'Senha123',
  name: 'Fulano da Silva',
  phoneNumber: '11999998888',
  cpf: '12345678909',
}

describe('useRegister', () => {
  it('em caso de sucesso, liga success e não deixa erro', async () => {
    server.use(
      http.post(REGISTER_URL, () => HttpResponse.json({ userId: 'uuid-1', ...payload }, { status: 201 })),
    )

    const { result } = renderHook(() => useRegister())

    await act(async () => {
      await result.current.register(payload)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
    expect(result.current.isLoading).toBe(false)
  })

  it('em caso de erro, popula error e mantém success false', async () => {
    server.use(
      http.post(REGISTER_URL, () =>
        HttpResponse.json(
          { status: 409, title: 'Conflito', detail: '...', type: 'https://gastosapp.dev/errors/email-already-exists' },
          { status: 409 },
        ),
      ),
    )

    const { result } = renderHook(() => useRegister())

    await act(async () => {
      await result.current.register(payload)
    })

    expect(result.current.error).toBeInstanceOf(EmailAlreadyExistsError)
    expect(result.current.success).toBe(false)
  })

  it('em falha de rede, expõe NetworkError', async () => {
    server.use(http.post(REGISTER_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useRegister())

    await act(async () => {
      await result.current.register(payload)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
    expect(result.current.success).toBe(false)
  })

  it('isLoading fica true durante a chamada e volta a false ao final', async () => {
    server.use(
      http.post(REGISTER_URL, () => HttpResponse.json({ userId: 'uuid-1', ...payload }, { status: 201 })),
    )

    const { result } = renderHook(() => useRegister())

    let registerPromise!: Promise<void>
    act(() => {
      registerPromise = result.current.register(payload)
    })
    expect(result.current.isLoading).toBe(true)

    await act(async () => {
      await registerPromise
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))
  })
})
