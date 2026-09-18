import { describe, expect, it } from 'vitest'
import { MEMBER_STATUS_LABEL } from './statusLabels'

describe('MEMBER_STATUS_LABEL', () => {
  it('mapeia os 3 status pro rótulo exibido', () => {
    expect(MEMBER_STATUS_LABEL.ConvitePendente).toBe('Convite pendente')
    expect(MEMBER_STATUS_LABEL.Ativo).toBe('Ativo')
    expect(MEMBER_STATUS_LABEL.Inativo).toBe('Inativo')
  })
})
