import { describe, expect, it } from 'vitest'
import { loginSchema } from './loginSchema'

describe('loginSchema', () => {
  it('aceita email válido e senha com 8+ caracteres', () => {
    const result = loginSchema.safeParse({ email: 'neto@email.com', password: 'Senha123' })
    expect(result.success).toBe(true)
  })

  it('rejeita email em formato inválido', () => {
    const result = loginSchema.safeParse({ email: 'nao-e-um-email', password: 'Senha123' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((issue) => issue.path[0] === 'email')).toBe(true)
    }
  })

  it('rejeita senha com menos de 8 caracteres', () => {
    const result = loginSchema.safeParse({ email: 'neto@email.com', password: '1234567' })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((issue) => issue.path[0] === 'password')).toBe(true)
    }
  })

  it('rejeita campos vazios', () => {
    const result = loginSchema.safeParse({ email: '', password: '' })
    expect(result.success).toBe(false)
  })
})
