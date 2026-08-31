import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, UnknownMemberError } from '../errors/memberErrors'
import { useMembers } from './useMembers'

const MEMBERS_URL = 'http://localhost:5049/members'

const titular = {
  id: 'mem-1',
  email: 'titular@email.com',
  role: 'Titular',
  status: 'Ativo',
  createdAt: '2025-06-15T12:34:56Z',
}

describe('useMembers', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega a lista de membros com sucesso', async () => {
    server.use(http.get(MEMBERS_URL, () => HttpResponse.json({ items: [titular] })))

    const { result } = renderHook(() => useMembers())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.items).toEqual([titular])
    expect(result.current.error).toBeNull()
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(MEMBERS_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useMembers())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(MEMBERS_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useMembers())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })

  it('em caso de erro inesperado, expõe UnknownMemberError', async () => {
    server.use(http.get(MEMBERS_URL, () => new HttpResponse(null, { status: 500 })))

    const { result } = renderHook(() => useMembers())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(UnknownMemberError))
  })
})
