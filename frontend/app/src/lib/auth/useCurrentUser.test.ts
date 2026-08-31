import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, UnknownCurrentUserError } from './currentUserErrors'
import { useCurrentUser } from './useCurrentUser'

const ME_URL = 'http://localhost:5049/auth/me'

const currentUser = {
  userId: 'user-1',
  email: 'titular@email.com',
  name: 'Fulano da Silva',
}

describe('useCurrentUser', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega o usuário corrente com sucesso', async () => {
    server.use(http.get(ME_URL, () => HttpResponse.json(currentUser)))

    const { result } = renderHook(() => useCurrentUser())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.data).toEqual(currentUser)
    expect(result.current.error).toBeNull()
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(ME_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useCurrentUser())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(ME_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useCurrentUser())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })

  it('em caso de erro inesperado, expõe UnknownCurrentUserError', async () => {
    server.use(http.get(ME_URL, () => new HttpResponse(null, { status: 500 })))

    const { result } = renderHook(() => useCurrentUser())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(UnknownCurrentUserError))
  })
})
