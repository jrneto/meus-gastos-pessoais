import { describe, expect, it } from 'vitest'
import { flattenNavItems, NAV_TREE } from './navConfig'

describe('flattenNavItems', () => {
  it('não há mais grupos com filhos — a árvore já é uma lista de itens folha', () => {
    const flat = flattenNavItems()

    expect(flat).toHaveLength(NAV_TREE.length)
    expect(flat.map((item) => item.id)).toEqual(NAV_TREE.map((item) => item.id))
  })

  it('inclui os 5 itens folha esperados (Início, Despesas, Relatórios, Categorias, Configurações)', () => {
    const ids = flattenNavItems().map((item) => item.id)

    expect(ids).toEqual(['home', 'expenses', 'reports', 'categories', 'settings'])
  })

  it('retorna exatamente 5 itens no total', () => {
    expect(flattenNavItems()).toHaveLength(5)
  })

  it('itens mobilePrimary são exatamente os 3 destinos esperados (Início, Despesas, Configurações)', () => {
    const mobilePrimaryIds = flattenNavItems()
      .filter((item) => item.mobilePrimary)
      .map((item) => item.id)

    expect(mobilePrimaryIds).toEqual(['home', 'expenses', 'settings'])
  })

  it('nenhum item fica sem rota — toda funcionalidade "não existente" navega para uma página fake', () => {
    for (const item of NAV_TREE) {
      expect(item.to).toBeDefined()
    }
  })

  it('"Relatórios" está ativo, navegável e fora do mobilePrimary (FEAT-15)', () => {
    const reports = flattenNavItems().find((item) => item.id === 'reports')

    expect(reports?.status).toBe('active')
    expect(reports?.to).toBe('/reports')
    expect(reports?.mobilePrimary).toBeFalsy()
  })

  it('"Categorias" está ativa, navegável e fora do mobilePrimary (FEAT-13)', () => {
    const categories = flattenNavItems().find((item) => item.id === 'categories')

    expect(categories?.status).toBe('active')
    expect(categories?.to).toBe('/categories')
    expect(categories?.mobilePrimary).toBeFalsy()
  })

  it('"Despesas" é um único item apontando para a listagem (FEAT-15)', () => {
    const expenses = flattenNavItems().find((item) => item.id === 'expenses')

    expect(expenses?.to).toBe('/expenses')
    expect(expenses?.children).toBeUndefined()
  })
})
