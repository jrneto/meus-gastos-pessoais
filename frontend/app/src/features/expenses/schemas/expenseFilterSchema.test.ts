import { describe, expect, it } from 'vitest'
import { expenseFilterSchema } from './expenseFilterSchema'

describe('expenseFilterSchema', () => {
  it('aceita todos os campos vazios/ausentes', () => {
    const result = expenseFilterSchema.safeParse({})
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data).toEqual({
        yearMonth: undefined,
        categoryId: undefined,
        dateFrom: undefined,
        dateTo: undefined,
        minAmountInCents: undefined,
        maxAmountInCents: undefined,
      })
    }
  })

  it('aceita apenas yearMonth informado', () => {
    const result = expenseFilterSchema.safeParse({ yearMonth: '2025-06' })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.yearMonth).toBe('2025-06')
    }
  })

  it('aceita apenas categoryId informado', () => {
    const result = expenseFilterSchema.safeParse({ categoryId: 'cat-1' })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.categoryId).toBe('cat-1')
    }
  })

  it('converte minAmount/maxAmount para centavos', () => {
    const result = expenseFilterSchema.safeParse({ minAmount: '10,00', maxAmount: '100,00' })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.minAmountInCents).toBe(1000)
      expect(result.data.maxAmountInCents).toBe(10000)
    }
  })

  it('rejeita minAmount em formato inválido', () => {
    const result = expenseFilterSchema.safeParse({ minAmount: 'dez reais' })
    expect(result.success).toBe(false)
  })

  it('aceita dateFrom e dateTo válidos (intervalo consistente)', () => {
    const result = expenseFilterSchema.safeParse({ dateFrom: '2025-06-01', dateTo: '2025-06-30' })
    expect(result.success).toBe(true)
  })

  it('rejeita dateFrom posterior a dateTo', () => {
    const result = expenseFilterSchema.safeParse({ dateFrom: '2025-06-30', dateTo: '2025-06-01' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].path).toEqual(['dateTo'])
    }
  })

  it('rejeita minAmount maior que maxAmount', () => {
    const result = expenseFilterSchema.safeParse({ minAmount: '100,00', maxAmount: '10,00' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].path).toEqual(['maxAmount'])
    }
  })

  it('aceita todos os filtros combinados', () => {
    const result = expenseFilterSchema.safeParse({
      yearMonth: '2025-06',
      categoryId: 'cat-1',
      dateFrom: '2025-06-01',
      dateTo: '2025-06-30',
      minAmount: '10,00',
      maxAmount: '100,00',
    })
    expect(result.success).toBe(true)
  })
})
