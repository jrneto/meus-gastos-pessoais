import { describe, expect, it } from 'vitest'
import { flattenNavItems, NAV_TREE } from './navConfig'

describe('flattenNavItems', () => {
  it('achata a árvore incluindo os filhos de grupos', () => {
    const flat = flattenNavItems()
    const ids = flat.map((item) => item.id)

    expect(ids).toContain('expenses-new')
    expect(ids).toContain('expenses-list')
  })

  it('não inclui o grupo "Despesas" em si, só seus filhos (grupo não tem `to`)', () => {
    const flat = flattenNavItems()
    const ids = flat.map((item) => item.id)

    expect(ids).not.toContain('expenses')
  })

  it('inclui itens folha de topo sem filhos (Início, Relatórios, Categorias, Configurações)', () => {
    const flat = flattenNavItems()
    const ids = flat.map((item) => item.id)

    expect(ids).toEqual(
      expect.arrayContaining(['home', 'reports', 'categories', 'settings']),
    )
  })

  it('retorna exatamente 6 itens folha no total', () => {
    expect(flattenNavItems()).toHaveLength(6)
  })

  it('itens mobilePrimary são exatamente os 4 destinos navegáveis esperados', () => {
    const mobilePrimaryIds = flattenNavItems()
      .filter((item) => item.mobilePrimary)
      .map((item) => item.id)

    expect(mobilePrimaryIds).toEqual(['home', 'expenses-new', 'expenses-list', 'settings'])
  })

  it('itens desabilitados (Relatórios) não têm rota nem são mobilePrimary', () => {
    const disabled = NAV_TREE.filter((item) => item.status === 'disabled')

    expect(disabled).toHaveLength(1)
    for (const item of disabled) {
      expect(item.to).toBeUndefined()
      expect(item.mobilePrimary).toBeFalsy()
    }
  })

  it('"Categorias" está ativa, navegável e fora do mobilePrimary (FEAT-13)', () => {
    const categories = flattenNavItems().find((item) => item.id === 'categories')

    expect(categories?.status).toBe('active')
    expect(categories?.to).toBe('/categories')
    expect(categories?.mobilePrimary).toBeFalsy()
  })
})
