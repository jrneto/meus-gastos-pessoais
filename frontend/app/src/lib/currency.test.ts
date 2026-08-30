import { describe, expect, it } from 'vitest'
import { centsToAmountInput, formatCentsToCurrency, parseCurrencyToCents } from './currency'

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

describe('formatCentsToCurrency', () => {
  it('formata centavos como moeda pt-BR', () => {
    expect(formatCentsToCurrency(4590)).toBe('R$ 45,90')
  })

  it('formata valor com separador de milhar', () => {
    expect(formatCentsToCurrency(123456)).toBe('R$ 1.234,56')
  })

  it('formata valor sem centavos', () => {
    expect(formatCentsToCurrency(10000)).toBe('R$ 100,00')
  })

  it('formata valor pequeno', () => {
    expect(formatCentsToCurrency(50)).toBe('R$ 0,50')
  })
})

describe('centsToAmountInput', () => {
  it('converte centavos para o formato de input (sem símbolo de moeda)', () => {
    expect(centsToAmountInput(4590)).toBe('45,90')
  })

  it('converte valor com separador de milhar', () => {
    expect(centsToAmountInput(123456)).toBe('1.234,56')
  })

  it('converte valor sem centavos', () => {
    expect(centsToAmountInput(10000)).toBe('100,00')
  })

  it('converte valor pequeno', () => {
    expect(centsToAmountInput(50)).toBe('0,50')
  })
})
