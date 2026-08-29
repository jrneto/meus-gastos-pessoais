import { describe, expect, it } from 'vitest'
import { extractDigits, isValidCpf, maskCpf } from './cpf'

describe('extractDigits', () => {
  it('remove tudo que não é dígito', () => {
    expect(extractDigits('123.456.789-09', 11)).toBe('12345678909')
  })

  it('respeita o limite máximo de dígitos', () => {
    expect(extractDigits('123456789099999', 11)).toBe('12345678909')
  })
})

describe('maskCpf', () => {
  it('não formata com 1 dígito', () => {
    expect(maskCpf('1')).toBe('1')
  })

  it('formata com 4 dígitos', () => {
    expect(maskCpf('1234')).toBe('123.4')
  })

  it('formata com 7 dígitos', () => {
    expect(maskCpf('1234567')).toBe('123.456.7')
  })

  it('formata completo com 11 dígitos', () => {
    expect(maskCpf('12345678909')).toBe('123.456.789-09')
  })

  it('ignora dígitos além do limite', () => {
    expect(maskCpf('123456789099999')).toBe('123.456.789-09')
  })
})

describe('isValidCpf', () => {
  it('aceita um CPF matematicamente válido', () => {
    expect(isValidCpf('12345678909')).toBe(true)
  })

  it('rejeita CPF com dígito verificador inválido', () => {
    expect(isValidCpf('12345678900')).toBe(false)
  })

  it('rejeita sequências de dígitos repetidos', () => {
    for (let i = 0; i <= 9; i++) {
      expect(isValidCpf(String(i).repeat(11))).toBe(false)
    }
  })

  it('rejeita CPF com menos de 11 dígitos', () => {
    expect(isValidCpf('123456789')).toBe(false)
  })

  it('rejeita valor com caracteres não numéricos', () => {
    expect(isValidCpf('1234567890a')).toBe(false)
  })
})
