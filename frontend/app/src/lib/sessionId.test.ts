import { beforeEach, describe, expect, it } from 'vitest'
import { clearSessionId, ensureSessionId, getSessionId, startNewSession } from './sessionId'

describe('sessionId', () => {
  beforeEach(() => {
    sessionStorage.clear()
  })

  it('getSessionId retorna null quando ainda não há sessão', () => {
    expect(getSessionId()).toBeNull()
  })

  it('startNewSession gera e guarda um valor, sempre diferente do anterior', () => {
    const first = startNewSession()
    const second = startNewSession()

    expect(first).toBeTruthy()
    expect(second).toBeTruthy()
    expect(first).not.toBe(second)
    expect(getSessionId()).toBe(second)
  })

  it('ensureSessionId gera um valor novo quando não há nenhum guardado', () => {
    const id = ensureSessionId()

    expect(id).toBeTruthy()
    expect(getSessionId()).toBe(id)
  })

  it('ensureSessionId reaproveita o valor já guardado, sem gerar um novo', () => {
    const existing = startNewSession()

    expect(ensureSessionId()).toBe(existing)
    expect(getSessionId()).toBe(existing)
  })

  it('clearSessionId remove o valor guardado', () => {
    startNewSession()

    clearSessionId()

    expect(getSessionId()).toBeNull()
  })
})
