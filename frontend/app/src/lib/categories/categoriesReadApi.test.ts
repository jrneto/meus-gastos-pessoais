import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, UnknownCategoryError } from './categoryErrors'
import { categoriesReadApi } from './categoriesReadApi'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('categoriesReadApi.getCategories', () => {
  it('retorna a lista de categorias em caso de sucesso', async () => {
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))

    const result = await categoriesReadApi.getCategories('tok-123')

    expect(result).toEqual({ items: [category] })
  })

  it('em caso de 401, lança SessionExpiredError', async () => {
    server.use(http.get(CATEGORIES_URL, () => new HttpResponse(null, { status: 401 })))

    await expect(categoriesReadApi.getCategories('tok-123')).rejects.toBeInstanceOf(
      SessionExpiredError,
    )
  })

  it('em caso de erro inesperado (5xx), lança UnknownCategoryError', async () => {
    server.use(http.get(CATEGORIES_URL, () => new HttpResponse(null, { status: 500 })))

    await expect(categoriesReadApi.getCategories('tok-123')).rejects.toBeInstanceOf(
      UnknownCategoryError,
    )
  })

  it('em caso de falha de rede, lança NetworkError', async () => {
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.error()))

    await expect(categoriesReadApi.getCategories('tok-123')).rejects.toBeInstanceOf(NetworkError)
  })
})
