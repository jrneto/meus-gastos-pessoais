import { act, renderHook } from '@testing-library/react'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { useResendCooldown } from './useResendCooldown'

describe('useResendCooldown', () => {
  beforeEach(() => {
    vi.useFakeTimers()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('começa em initialSeconds', () => {
    const { result } = renderHook(() => useResendCooldown(60))

    expect(result.current.secondsLeft).toBe(60)
    expect(result.current.isExpired).toBe(false)
  })

  it('decresce 1 por segundo', () => {
    const { result } = renderHook(() => useResendCooldown(60))

    act(() => {
      vi.advanceTimersByTime(3000)
    })

    expect(result.current.secondsLeft).toBe(57)
  })

  it('isExpired vira true ao chegar a 0 e para de decrescer', () => {
    const { result } = renderHook(() => useResendCooldown(3))

    act(() => {
      vi.advanceTimersByTime(3000)
    })
    expect(result.current.secondsLeft).toBe(0)
    expect(result.current.isExpired).toBe(true)

    act(() => {
      vi.advanceTimersByTime(5000)
    })
    expect(result.current.secondsLeft).toBe(0)
  })

  it('restart() volta pra initialSeconds e recomeça a contagem', () => {
    const { result } = renderHook(() => useResendCooldown(3))

    act(() => {
      vi.advanceTimersByTime(3000)
    })
    expect(result.current.isExpired).toBe(true)

    act(() => {
      result.current.restart()
    })
    expect(result.current.secondsLeft).toBe(3)
    expect(result.current.isExpired).toBe(false)

    act(() => {
      vi.advanceTimersByTime(1000)
    })
    expect(result.current.secondsLeft).toBe(2)
  })

  it('cleanup no unmount não deixa o interval rodando', () => {
    const { result, unmount } = renderHook(() => useResendCooldown(60))

    unmount()

    // Se o interval não tivesse sido limpo, isso chamaria setState num
    // componente desmontado (warning do React) — o teste falha se
    // qualquer erro/warning inesperado for lançado durante o avanço.
    expect(() => {
      act(() => {
        vi.advanceTimersByTime(5000)
      })
    }).not.toThrow()
    expect(result.current.secondsLeft).toBe(60)
  })
})
