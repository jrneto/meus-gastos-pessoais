import { afterEach, describe, expect, it, vi } from 'vitest'
import { formatMonthLabel, getCurrentYearMonth } from './month'

describe('getCurrentYearMonth', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('retorna o mês corrente no formato YYYY-MM', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 7, 30)) // agosto (mês 7, zero-based)

    expect(getCurrentYearMonth()).toBe('2026-08')
  })

  it('preenche o mês com zero à esquerda quando necessário', () => {
    vi.useFakeTimers()
    vi.setSystemTime(new Date(2026, 0, 15)) // janeiro

    expect(getCurrentYearMonth()).toBe('2026-01')
  })
})

describe('formatMonthLabel', () => {
  it('formata um mês no meio do ano', () => {
    expect(formatMonthLabel('2026-08')).toBe('Agosto de 2026')
  })

  it('formata janeiro corretamente', () => {
    expect(formatMonthLabel('2026-01')).toBe('Janeiro de 2026')
  })

  it('formata dezembro corretamente', () => {
    expect(formatMonthLabel('2025-12')).toBe('Dezembro de 2025')
  })
})
