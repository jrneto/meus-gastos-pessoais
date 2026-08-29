import { describe, expect, it } from 'vitest'
import { maskPhone } from './phoneMask'

describe('maskPhone', () => {
  it('não formata com 2 dígitos (DDD incompleto)', () => {
    expect(maskPhone('11')).toBe('11')
  })

  it('formata DDD + início do número', () => {
    expect(maskPhone('119999')).toBe('(11) 9999')
  })

  it('formata completo com 10 dígitos (fixo)', () => {
    expect(maskPhone('1187654321')).toBe('(11) 8765-4321')
  })

  it('formata completo com 11 dígitos (celular)', () => {
    expect(maskPhone('11999998888')).toBe('(11) 99999-8888')
  })

  it('ignora dígitos além do limite', () => {
    expect(maskPhone('119999988889999')).toBe('(11) 99999-8888')
  })
})
