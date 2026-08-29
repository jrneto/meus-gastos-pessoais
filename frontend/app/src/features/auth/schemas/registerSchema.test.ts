import { describe, expect, it } from 'vitest'
import { registerSchema } from './registerSchema'

const validData = {
  name: 'Fulano da Silva',
  phoneDigits: '11999998888',
  cpfDigits: '12345678909',
  email: 'fulano@email.com',
  password: 'Senha123',
}

describe('registerSchema', () => {
  it('aceita dados válidos', () => {
    expect(registerSchema.safeParse(validData).success).toBe(true)
  })

  it('rejeita nome vazio', () => {
    const result = registerSchema.safeParse({ ...validData, name: '  ' })
    expect(result.success).toBe(false)
  })

  it('rejeita nome com mais de 150 caracteres', () => {
    const result = registerSchema.safeParse({ ...validData, name: 'a'.repeat(151) })
    expect(result.success).toBe(false)
  })

  it('rejeita telefone com 9 dígitos', () => {
    const result = registerSchema.safeParse({ ...validData, phoneDigits: '119999988' })
    expect(result.success).toBe(false)
  })

  it('rejeita telefone com 12 dígitos', () => {
    const result = registerSchema.safeParse({ ...validData, phoneDigits: '119999988889' })
    expect(result.success).toBe(false)
  })

  it('rejeita CPF com dígito verificador inválido', () => {
    const result = registerSchema.safeParse({ ...validData, cpfDigits: '12345678900' })
    expect(result.success).toBe(false)
  })

  it('rejeita CPF com sequência repetida', () => {
    const result = registerSchema.safeParse({ ...validData, cpfDigits: '11111111111' })
    expect(result.success).toBe(false)
  })

  it('rejeita email inválido', () => {
    const result = registerSchema.safeParse({ ...validData, email: 'não-é-email' })
    expect(result.success).toBe(false)
  })

  it('rejeita senha com menos de 8 caracteres', () => {
    const result = registerSchema.safeParse({ ...validData, password: '1234567' })
    expect(result.success).toBe(false)
  })
})
