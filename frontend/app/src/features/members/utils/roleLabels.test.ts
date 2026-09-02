import { describe, expect, it } from 'vitest'
import { ROLE_DESCRIPTION, ROLE_LABEL } from './roleLabels'

describe('ROLE_LABEL', () => {
  it('mapeia os 4 papéis pro rótulo exibido', () => {
    expect(ROLE_LABEL.Leitura).toBe('Leitura')
    expect(ROLE_LABEL.Lancar).toBe('Lançar')
    expect(ROLE_LABEL.Total).toBe('Total')
    expect(ROLE_LABEL.Titular).toBe('Titular')
  })
})

describe('ROLE_DESCRIPTION', () => {
  it('mapeia os 3 papéis atribuíveis por convite pra descrição', () => {
    expect(ROLE_DESCRIPTION.Leitura).toBe('Pode visualizar despesas e relatórios, sem editar nada.')
    expect(ROLE_DESCRIPTION.Lancar).toBe('Pode visualizar e lançar novas despesas.')
    expect(ROLE_DESCRIPTION.Total).toBe(
      'Pode visualizar, lançar despesas e criar categorias e orçamentos. Não pode gerenciar outros membros.',
    )
  })
})
