import { afterEach, describe, expect, it, vi } from 'vitest'
import { formatComparisonLabel, formatPercent, getCurrentDate } from './period'

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

describe('formatPercent', () => {
  it('formata um valor inteiro sem casa decimal', () => {
    expect(formatPercent(12)).toBe('12')
  })

  it('formata um valor com 1 casa decimal usando vírgula', () => {
    expect(formatPercent(54.4)).toBe('54,4')
  })

  it('formata um valor negativo', () => {
    expect(formatPercent(-4)).toBe('-4')
  })
})

describe('formatComparisonLabel', () => {
  it('variação positiva no período mensal', () => {
    expect(formatComparisonLabel(12, 'month')).toBe('+12% vs mês passado')
  })

  it('variação negativa no período semanal', () => {
    expect(formatComparisonLabel(-4, 'week')).toBe('-4% vs semana passada')
  })

  it('variação positiva no período anual, com 1 casa decimal', () => {
    expect(formatComparisonLabel(8.5, 'year')).toBe('+8,5% vs ano passado')
  })

  it('variação zero mostra sinal +', () => {
    expect(formatComparisonLabel(0, 'month')).toBe('+0% vs mês passado')
  })
})
