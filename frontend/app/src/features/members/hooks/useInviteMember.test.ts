import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import {
  ConflictError,
  ForbiddenError,
  SessionExpiredError,
  ValidationError,
} from '../errors/memberErrors'
import { useInviteMember } from './useInviteMember'

const MEMBERS_URL = 'http://localhost:5049/members'

const invited = {
  id: 'mem-2',
  email: 'convidado@email.com',
  role: 'Leitura',
  status: 'ConvitePendente',
  createdAt: '2025-06-16T09:00:00Z',
}

describe('useInviteMember', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('sucesso expõe success e data', async () => {
    server.use(http.post(MEMBERS_URL, () => HttpResponse.json(invited)))
    const { result } = renderHook(() => useInviteMember())

    await act(() => result.current.inviteMember({ email: 'convidado@email.com', role: 'Leitura' }))

    expect(result.current.success).toBe(true)
    expect(result.current.data).toEqual(invited)
    expect(result.current.error).toBeNull()
  })

  it('400 expõe ValidationError', async () => {
    server.use(http.post(MEMBERS_URL, () => new HttpResponse(null, { status: 400 })))
    const { result } = renderHook(() => useInviteMember())

    await act(() => result.current.inviteMember({ email: '', role: 'Leitura' }))

    expect(result.current.error).toBeInstanceOf(ValidationError)
  })

  it('409 expõe ConflictError', async () => {
    server.use(
      http.post(
        MEMBERS_URL,
        () =>
          new HttpResponse(
            JSON.stringify({ type: 'https://gastosapp.dev/errors/member-already-exists' }),
            { status: 409 },
          ),
      ),
    )
    const { result } = renderHook(() => useInviteMember())

    await act(() => result.current.inviteMember({ email: 'convidado@email.com', role: 'Leitura' }))

    expect(result.current.error).toBeInstanceOf(ConflictError)
  })

  it('403 expõe ForbiddenError', async () => {
    server.use(http.post(MEMBERS_URL, () => new HttpResponse(null, { status: 403 })))
    const { result } = renderHook(() => useInviteMember())

    await act(() => result.current.inviteMember({ email: 'convidado@email.com', role: 'Leitura' }))

    expect(result.current.error).toBeInstanceOf(ForbiddenError)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.post(MEMBERS_URL, () => new HttpResponse(null, { status: 401 })))
    const { result } = renderHook(() => useInviteMember())

    await act(() => result.current.inviteMember({ email: 'convidado@email.com', role: 'Leitura' }))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })
})
