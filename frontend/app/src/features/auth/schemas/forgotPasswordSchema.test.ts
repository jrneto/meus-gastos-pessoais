import { describe, expect, it } from 'vitest'
import { forgotPasswordEmailSchema, newPasswordSchema } from './forgotPasswordSchema'

describe('forgotPasswordEmailSchema', () => {
  it('aceita email válido', () => {
    expect(forgotPasswordEmailSchema.safeParse({ email: 'fulano@email.com' }).success).toBe(true)
  })

  it('rejeita email inválido', () => {
    const result = forgotPasswordEmailSchema.safeParse({ email: 'nao-e-email' })
    expect(result.success).toBe(false)
  })

  it('rejeita email vazio', () => {
    const result = forgotPasswordEmailSchema.safeParse({ email: '' })
    expect(result.success).toBe(false)
  })
})

describe('newPasswordSchema', () => {
  it('aceita quando os dois campos coincidem e atendem a política', () => {
    const result = newPasswordSchema.safeParse({ newPassword: 'Senha123@', confirmNewPassword: 'Senha123@' })
    expect(result.success).toBe(true)
  })

  it('rejeita quando os campos divergem, mesmo ambos válidos individualmente', () => {
    const result = newPasswordSchema.safeParse({ newPassword: 'Senha123@', confirmNewPassword: 'OutraSenha1@' })
    expect(result.success).toBe(false)
    if (!result.success) {
      const issue = result.error.issues.find((i) => i.path.includes('confirmNewPassword'))
      expect(issue?.message).toBe('As senhas não coincidem.')
    }
  })

  it('propaga o erro de passwordPolicySchema quando newPassword é fraca', () => {
    const result = newPasswordSchema.safeParse({ newPassword: 'senha123', confirmNewPassword: 'senha123' })
    expect(result.success).toBe(false)
  })
})
