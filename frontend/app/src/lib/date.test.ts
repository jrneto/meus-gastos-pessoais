import { afterEach, describe, expect, it, vi } from 'vitest'
import { getCurrentDate } from './date'

describe('getCurrentDate', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('retorna a data corrente no formato YYYY-MM-DD', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 7, 30)) // 30 de agosto (mês 7, zero-based)

    expect(getCurrentDate()).toBe('2026-08-30')
  })

  it('preenche mês e dia com zero à esquerda quando necessário', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 0, 5)) // 5 de janeiro

    expect(getCurrentDate()).toBe('2026-01-05')
  })
})
