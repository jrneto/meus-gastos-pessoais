import { describe, expect, it } from 'vitest'
import { confirmationCodeSchema } from './confirmationSchema'

describe('confirmationCodeSchema', () => {
  it('aceita uma string de 6 dígitos', () => {
    expect(confirmationCodeSchema.safeParse('123456').success).toBe(true)
  })

  it('rejeita menos de 6 dígitos', () => {
    const result = confirmationCodeSchema.safeParse('12345')
    expect(result.success).toBe(false)
  })

  it('rejeita mais de 6 dígitos', () => {
    const result = confirmationCodeSchema.safeParse('1234567')
    expect(result.success).toBe(false)
  })

  it('rejeita string com caractere não numérico', () => {
    const result = confirmationCodeSchema.safeParse('12345a')
    expect(result.success).toBe(false)
  })
})
