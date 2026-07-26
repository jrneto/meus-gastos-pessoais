import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from './authStore'

describe('authStore', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('começa sem sessão', () => {
    const state = useAuthStore.getState()
    expect(state.token).toBeNull()
    expect(state.userId).toBeNull()
    expect(state.expiresAt).toBeNull()
  })

  it('setSession popula token, userId e calcula expiresAt a partir de expiresIn', () => {
    const now = Date.now()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)

    const state = useAuthStore.getState()
    expect(state.token).toBe('tok-123')
    expect(state.userId).toBe('user-1')
    expect(state.expiresAt).toBeGreaterThanOrEqual(now + 3600 * 1000)
  })

  it('sessão é válida (Date.now() < expiresAt) logo após o login', () => {
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    const { expiresAt } = useAuthStore.getState()
    expect(Date.now() < (expiresAt as number)).toBe(true)
  })

  it('sessão fica inválida (Date.now() >= expiresAt) depois do tempo de expiração passar', () => {
    vi.useFakeTimers()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)

    vi.advanceTimersByTime(3601 * 1000)

    const { expiresAt } = useAuthStore.getState()
    expect(Date.now() >= (expiresAt as number)).toBe(true)
  })

  it('clearSession remove token, userId e expiresAt', () => {
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    useAuthStore.getState().clearSession()

    const state = useAuthStore.getState()
    expect(state.token).toBeNull()
    expect(state.userId).toBeNull()
    expect(state.expiresAt).toBeNull()
  })
})
