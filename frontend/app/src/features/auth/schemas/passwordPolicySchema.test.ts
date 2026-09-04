import { describe, expect, it } from 'vitest'
import { passwordPolicySchema } from './passwordPolicySchema'

describe('passwordPolicySchema', () => {
  it('aceita senha que atende toda a política', () => {
    expect(passwordPolicySchema.safeParse('Senha123@').success).toBe(true)
  })

  it('rejeita senha com menos de 8 caracteres', () => {
    const result = passwordPolicySchema.safeParse('Ab1@')
    expect(result.success).toBe(false)
  })

  it('rejeita senha sem letra maiúscula', () => {
    const result = passwordPolicySchema.safeParse('senha123@')
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].message).toBe('A senha deve ter ao menos uma letra maiúscula.')
    }
  })

  it('rejeita senha sem letra minúscula', () => {
    const result = passwordPolicySchema.safeParse('SENHA123@')
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].message).toBe('A senha deve ter ao menos uma letra minúscula.')
    }
  })

  it('rejeita senha sem número', () => {
    const result = passwordPolicySchema.safeParse('SenhaSegura@')
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].message).toBe('A senha deve ter ao menos um número.')
    }
  })

  it('rejeita senha sem símbolo', () => {
    const result = passwordPolicySchema.safeParse('Senha1234')
    expect(result.success).toBe(false)
    if (!result.success) {
      expect(result.error.issues[0].message).toBe('A senha deve ter ao menos um símbolo.')
    }
  })
})
