import { describe, expect, it } from 'vitest'
import { signupSchema } from './signupSchema'

describe('signupSchema', () => {
  it('aceita nome, email válido e senha com 8+ caracteres', () => {
    const result = signupSchema.safeParse({
      name: 'Neto',
      email: 'neto@email.com',
      password: 'Senha123',
    })
    expect(result.success).toBe(true)
  })

  it('rejeita nome vazio', () => {
    const result = signupSchema.safeParse({
      name: '',
      email: 'neto@email.com',
      password: 'Senha123',
    })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((issue) => issue.path[0] === 'name')).toBe(true)
    }
  })

  it('rejeita email em formato inválido', () => {
    const result = signupSchema.safeParse({
      name: 'Neto',
      email: 'nao-e-um-email',
      password: 'Senha123',
    })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((issue) => issue.path[0] === 'email')).toBe(true)
    }
  })

  it('rejeita senha com menos de 8 caracteres', () => {
    const result = signupSchema.safeParse({
      name: 'Neto',
      email: 'neto@email.com',
      password: '1234567',
    })
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues.some((issue) => issue.path[0] === 'password')).toBe(true)
    }
  })
})
