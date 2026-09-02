import { describe, expect, it } from 'vitest'
import { transactionSchema } from './transactionSchema'

const validInput = {
  description: 'Almoço no restaurante',
  amount: '45,90',
  categoryId: 'cat-1',
  date: '2025-06-15',
}

describe('transactionSchema', () => {
  it('aceita entrada válida e converte o valor para centavos', () => {
    const result = transactionSchema.safeParse(validInput)
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.amount).toBe(4590)
    }
  })

  it('rejeita descrição vazia', () => {
    const result = transactionSchema.safeParse({ ...validInput, description: '' })
    expect(result.success).toBe(false)
  })

  it('rejeita descrição com mais de 200 caracteres', () => {
    const result = transactionSchema.safeParse({ ...validInput, description: 'a'.repeat(201) })
    expect(result.success).toBe(false)
  })

  it('rejeita valor ausente', () => {
    const result = transactionSchema.safeParse({ ...validInput, amount: '' })
    expect(result.success).toBe(false)
  })

  it('rejeita valor em formato inválido', () => {
    const result = transactionSchema.safeParse({ ...validInput, amount: 'quarenta e cinco' })
    expect(result.success).toBe(false)
  })

  it('rejeita valor igual a zero', () => {
    const result = transactionSchema.safeParse({ ...validInput, amount: '0,00' })
    expect(result.success).toBe(false)
  })

  it('rejeita categoryId vazio', () => {
    const result = transactionSchema.safeParse({ ...validInput, categoryId: '' })
    expect(result.success).toBe(false)
  })

  it('rejeita data ausente', () => {
    const result = transactionSchema.safeParse({ ...validInput, date: '' })
    expect(result.success).toBe(false)
  })
})
