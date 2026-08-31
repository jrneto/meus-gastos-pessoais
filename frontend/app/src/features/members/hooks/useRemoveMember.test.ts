import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { CannotRemoveTitularError, ForbiddenError, NotFoundError } from '../errors/memberErrors'
import { useRemoveMember } from './useRemoveMember'

const MEMBER_URL = 'http://localhost:5049/members/mem-2'

describe('useRemoveMember', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('sucesso expõe success', async () => {
    server.use(http.delete(MEMBER_URL, () => new HttpResponse(null, { status: 204 })))
    const { result } = renderHook(() => useRemoveMember())

    await act(() => result.current.removeMember('mem-2'))

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('404 expõe NotFoundError', async () => {
    server.use(http.delete(MEMBER_URL, () => new HttpResponse(null, { status: 404 })))
    const { result } = renderHook(() => useRemoveMember())

    await act(() => result.current.removeMember('mem-2'))

    expect(result.current.error).toBeInstanceOf(NotFoundError)
  })

  it('422 cannot-remove-titular expõe CannotRemoveTitularError', async () => {
    server.use(
      http.delete(
        MEMBER_URL,
        () =>
          new HttpResponse(JSON.stringify({ type: 'https://gastosapp.dev/errors/cannot-remove-titular' }), {
            status: 422,
          }),
      ),
    )
    const { result } = renderHook(() => useRemoveMember())

    await act(() => result.current.removeMember('mem-2'))

    expect(result.current.error).toBeInstanceOf(CannotRemoveTitularError)
  })

  it('403 expõe ForbiddenError', async () => {
    server.use(http.delete(MEMBER_URL, () => new HttpResponse(null, { status: 403 })))
    const { result } = renderHook(() => useRemoveMember())

    await act(() => result.current.removeMember('mem-2'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(ForbiddenError))
  })
})
