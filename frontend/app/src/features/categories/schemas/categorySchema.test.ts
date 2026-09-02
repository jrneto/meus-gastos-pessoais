import { describe, expect, it } from 'vitest'
import { categorySchema } from './categorySchema'

describe('categorySchema', () => {
  it('rejeita nome vazio', () => {
    const result = categorySchema.safeParse({ nome: '', tipo: 'despesa' })
    expect(result.success).toBe(false)
  })

  it('rejeita nome com mais de 50 caracteres', () => {
    const result = categorySchema.safeParse({ nome: 'a'.repeat(51), tipo: 'despesa' })
    expect(result.success).toBe(false)
  })

  it('rejeita tipo ausente', () => {
    const result = categorySchema.safeParse({ nome: 'Alimentação' })
    expect(result.success).toBe(false)
  })

  it('rejeita tipo fora de despesa/receita', () => {
    const result = categorySchema.safeParse({ nome: 'Alimentação', tipo: 'outro' })
    expect(result.success).toBe(false)
  })

  it('rejeita teto em formato inválido', () => {
    const result = categorySchema.safeParse({
      nome: 'Alimentação',
      tipo: 'despesa',
      orcamentoMensal: 'abc',
    })
    expect(result.success).toBe(false)
  })

  it('rejeita teto igual a zero', () => {
    const result = categorySchema.safeParse({
      nome: 'Alimentação',
      tipo: 'despesa',
      orcamentoMensal: '0,00',
    })
    expect(result.success).toBe(false)
  })

  it('rejeita teto negativo (formato inválido, não representável como 0,00)', () => {
    const result = categorySchema.safeParse({
      nome: 'Alimentação',
      tipo: 'despesa',
      orcamentoMensal: '-10,00',
    })
    expect(result.success).toBe(false)
  })

  it('aceita categoria de despesa sem teto (omitido)', () => {
    const result = categorySchema.safeParse({ nome: 'Alimentação', tipo: 'despesa' })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data).toEqual({
        nome: 'Alimentação',
        tipo: 'despesa',
        orcamentoMensalCents: undefined,
      })
    }
  })

  it('aceita categoria de despesa com teto válido, convertendo para centavos', () => {
    const result = categorySchema.safeParse({
      nome: 'Alimentação',
      tipo: 'despesa',
      orcamentoMensal: '800,00',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data.orcamentoMensalCents).toBe(80000)
    }
  })

  it('aceita categoria de receita, ignorando um teto preenchido por engano', () => {
    const result = categorySchema.safeParse({
      nome: 'Salário',
      tipo: 'receita',
      orcamentoMensal: '800,00',
    })
    expect(result.success).toBe(true)
    if (result.success) {
      expect(result.data).toEqual({
        nome: 'Salário',
        tipo: 'receita',
        orcamentoMensalCents: undefined,
      })
    }
  })
})
