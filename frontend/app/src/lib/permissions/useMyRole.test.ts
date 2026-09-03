import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { SessionExpiredError } from './permissionErrors'
import { useMyRole } from './useMyRole'

const ME_URL = 'http://localhost:5049/auth/me'
const MEMBERS_URL = 'http://localhost:5049/members'

const currentUser = {
  userId: 'user-1',
  email: 'convidado@email.com',
  name: 'Fulano da Silva',
}

const members = [
  { email: 'titular@email.com', role: 'Titular' },
  { email: 'convidado@email.com', role: 'Lancar' },
]

describe('useMyRole', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('resolve role e userId quando o e-mail bate com um item de GET /members', async () => {
    server.use(
      http.get(ME_URL, () => HttpResponse.json(currentUser)),
      http.get(MEMBERS_URL, () => HttpResponse.json({ items: members })),
    )

    const { result } = renderHook(() => useMyRole())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.role).toBe('Lancar')
    expect(result.current.userId).toBe('user-1')
    expect(result.current.error).toBeNull()
  })

  it('role permanece null se nenhum item de GET /members bater com o e-mail', async () => {
    server.use(
      http.get(ME_URL, () => HttpResponse.json(currentUser)),
      http.get(MEMBERS_URL, () => HttpResponse.json({ items: [members[0]] })),
    )

    const { result } = renderHook(() => useMyRole())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.role).toBeNull()
  })

  it('isLoading fica true enquanto GET /auth/me ainda não respondeu', async () => {
    server.use(
      http.get(ME_URL, async () => {
        await new Promise((resolve) => setTimeout(resolve, 50))
        return HttpResponse.json(currentUser)
      }),
      http.get(MEMBERS_URL, () => HttpResponse.json({ items: members })),
    )

    const { result } = renderHook(() => useMyRole())

    expect(result.current.isLoading).toBe(true)
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.role).toBe('Lancar')
  })

  it('isLoading fica true enquanto GET /members ainda não respondeu', async () => {
    server.use(
      http.get(ME_URL, () => HttpResponse.json(currentUser)),
      http.get(MEMBERS_URL, async () => {
        await new Promise((resolve) => setTimeout(resolve, 50))
        return HttpResponse.json({ items: members })
      }),
    )

    const { result } = renderHook(() => useMyRole())

    await waitFor(() => expect(result.current.role).toBe('Lancar'))
    expect(result.current.isLoading).toBe(false)
  })

  it('quando GET /auth/me falha, expõe o erro e não fica preso carregando', async () => {
    server.use(http.get(ME_URL, () => new HttpResponse(null, { status: 500 })))

    const { result } = renderHook(() => useMyRole())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.error).not.toBeNull()
    expect(result.current.role).toBeNull()
  })

  it('quando GET /members falha com 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(
      http.get(ME_URL, () => HttpResponse.json(currentUser)),
      http.get(MEMBERS_URL, () => new HttpResponse(null, { status: 401 })),
    )

    const { result } = renderHook(() => useMyRole())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })
})
