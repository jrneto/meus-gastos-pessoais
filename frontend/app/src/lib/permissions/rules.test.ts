import { describe, expect, it } from 'vitest'
import { canCreateTransaction, canManageTransaction, canWriteCategories } from './rules'

describe('canCreateTransaction', () => {
  it.each([
    ['Leitura', false],
    ['Lancar', true],
    ['Total', true],
    ['Titular', true],
    [null, false],
  ] as const)('para role=%s retorna %s', (role, expected) => {
    expect(canCreateTransaction(role)).toBe(expected)
  })
})

describe('canManageTransaction', () => {
  it.each([
    ['Leitura', true, false],
    ['Leitura', false, false],
    ['Lancar', true, true],
    ['Lancar', false, false],
    ['Total', true, true],
    ['Total', false, true],
    ['Titular', true, true],
    ['Titular', false, true],
    [null, true, false],
    [null, false, false],
  ] as const)('para role=%s, isOwn=%s retorna %s', (role, isOwn, expected) => {
    expect(canManageTransaction(role, isOwn)).toBe(expected)
  })
})

describe('canWriteCategories', () => {
  it.each([
    ['Leitura', false],
    ['Lancar', false],
    ['Total', true],
    ['Titular', true],
    [null, false],
  ] as const)('para role=%s retorna %s', (role, expected) => {
    expect(canWriteCategories(role)).toBe(expected)
  })
})
