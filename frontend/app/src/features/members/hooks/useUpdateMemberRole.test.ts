import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { CannotModifyTitularError, ForbiddenError, NotFoundError } from '../errors/memberErrors'
import { useUpdateMemberRole } from './useUpdateMemberRole'

const MEMBER_URL = 'http://localhost:5049/members/mem-2'

const updated = {
  id: 'mem-2',
  email: 'convidado@email.com',
  role: 'Total',
  status: 'Ativo',
  createdAt: '2025-06-16T09:00:00Z',
}

describe('useUpdateMemberRole', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('sucesso expõe success e data', async () => {
    server.use(http.put(MEMBER_URL, () => HttpResponse.json(updated)))
    const { result } = renderHook(() => useUpdateMemberRole('mem-2'))

    await act(() => result.current.updateRole('Total'))

    expect(result.current.success).toBe(true)
    expect(result.current.data).toEqual(updated)
  })

  it('404 expõe NotFoundError', async () => {
    server.use(http.put(MEMBER_URL, () => new HttpResponse(null, { status: 404 })))
    const { result } = renderHook(() => useUpdateMemberRole('mem-2'))

    await act(() => result.current.updateRole('Total'))

    expect(result.current.error).toBeInstanceOf(NotFoundError)
  })

  it('422 cannot-modify-titular expõe CannotModifyTitularError', async () => {
    server.use(
      http.put(
        MEMBER_URL,
        () =>
          new HttpResponse(JSON.stringify({ type: 'https://gastosapp.dev/errors/cannot-modify-titular' }), {
            status: 422,
          }),
      ),
    )
    const { result } = renderHook(() => useUpdateMemberRole('mem-2'))

    await act(() => result.current.updateRole('Total'))

    expect(result.current.error).toBeInstanceOf(CannotModifyTitularError)
  })

  it('403 expõe ForbiddenError', async () => {
    server.use(http.put(MEMBER_URL, () => new HttpResponse(null, { status: 403 })))
    const { result } = renderHook(() => useUpdateMemberRole('mem-2'))

    await act(() => result.current.updateRole('Total'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(ForbiddenError))
  })
})
