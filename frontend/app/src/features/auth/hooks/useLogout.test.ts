import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { useAuthStore } from '../store/authStore'
import { useLogout } from './useLogout'

const LOGOUT_URL = 'http://localhost:5049/auth/logout'

describe('useLogout', () => {
  beforeEach(() => {
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('chama POST /auth/logout e limpa a sessão local', async () => {
    let logoutCalled = false
    server.use(
      http.post(LOGOUT_URL, () => {
        logoutCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )

    const { result } = renderHook(() => useLogout())

    await act(async () => {
      await result.current.logout()
    })

    expect(logoutCalled).toBe(true)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('falha em /auth/logout não impede o logout local', async () => {
    server.use(http.post(LOGOUT_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useLogout())

    await act(async () => {
      await result.current.logout()
    })

    expect(useAuthStore.getState().token).toBeNull()
  })
})
