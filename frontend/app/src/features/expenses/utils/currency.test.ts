import { describe, expect, it } from 'vitest'
import { parseCurrencyToCents } from './currency'

describe('parseCurrencyToCents', () => {
  it('converte valor simples com centavos', () => {
    expect(parseCurrencyToCents('45,90')).toBe(4590)
  })

  it('converte valor com separador de milhar', () => {
    expect(parseCurrencyToCents('1.234,56')).toBe(123456)
  })

  it('converte valor inteiro sem centavos', () => {
    expect(parseCurrencyToCents('100')).toBe(10000)
  })

  it('converte valor com centavos de um dígito corretamente arredondado', () => {
    expect(parseCurrencyToCents('10,10')).toBe(1010)
  })

  it('converte valor pequeno', () => {
    expect(parseCurrencyToCents('0,50')).toBe(50)
  })

  it('lida com espaços em branco ao redor do valor', () => {
    expect(parseCurrencyToCents('  45,90  ')).toBe(4590)
  })
})