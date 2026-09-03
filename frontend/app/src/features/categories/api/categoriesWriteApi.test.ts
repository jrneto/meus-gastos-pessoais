import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import {
  CategoryInUseError,
  ForbiddenError,
  NameConflictError,
  NetworkError,
  NotFoundError,
  SessionExpiredError,
  UnknownCategoryError,
  ValidationError,
} from '../errors/categoryErrors'
import { categoriesWriteApi, type CategoryPayload } from './categoriesWriteApi'

const CATEGORIES_URL = 'http://localhost:5049/categories'
const CATEGORY_URL = 'http://localhost:5049/categories/cat-1'

const payload: CategoryPayload = { nome: 'Viagem', tipo: 'despesa', orcamentoMensalCents: 50000 }
const category = { id: 'cat-1', ...payload, createdAt: '2025-06-15T12:00:00Z' }

function problem(type: string) {
  return HttpResponse.json(
    { status: 422, title: 'Regra de negócio violada', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 422 },
  )
}

describe('categoriesWriteApi.createCategory', () => {
  it('retorna a categoria criada em caso de sucesso (despesa com teto)', async () => {
    server.use(http.post(CATEGORIES_URL, () => HttpResponse.json(category, { status: 201 })))

    const result = await categoriesWriteApi.createCategory('tok-123', payload)

    expect(result).toEqual(category)
  })

  it('envia o payload de despesa sem teto sem o campo orcamentoMensalCents', async () => {
    const semTeto: CategoryPayload = { nome: 'Assinaturas', tipo: 'despesa' }
    let receivedBody: unknown
    server.use(
      http.post(CATEGORIES_URL, async ({ request }) => {
        receivedBody = await request.json()
        return HttpResponse.json({ id: 'cat-2', ...semTeto, createdAt: '2025-06-15T12:00:00Z' }, { status: 201 })
      }),
    )

    await categoriesWriteApi.createCategory('tok-123', semTeto)

    expect(receivedBody).toEqual(semTeto)
  })

  it('envia o payload de receita sem o campo orcamentoMensalCents', async () => {
    const receita: CategoryPayload = { nome: 'Salário', tipo: 'receita' }
    let receivedBody: unknown
    server.use(
      http.post(CATEGORIES_URL, async ({ request }) => {
        receivedBody = await request.json()
        return HttpResponse.json({ id: 'cat-3', ...receita, createdAt: '2025-06-15T12:00:00Z' }, { status: 201 })
      }),
    )

    await categoriesWriteApi.createCategory('tok-123', receita)

    expect(receivedBody).toEqual(receita)
  })

  it('em caso de 400, lança ValidationError', async () => {
    server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 400 })))

    await expect(categoriesWriteApi.createCategory('tok-123', payload)).rejects.toBeInstanceOf(
      ValidationError,
    )
  })

  it('em caso de 422 name-conflict, lança NameConflictError', async () => {
    server.use(http.post(CATEGORIES_URL, () => problem('name-conflict')))

    await expect(categoriesWriteApi.createCategory('tok-123', payload)).rejects.toBeInstanceOf(
      NameConflictError,
    )
  })

  it('em caso de 401, lança SessionExpiredError', async () => {
    server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 401 })))

    await expect(categoriesWriteApi.createCategory('tok-123', payload)).rejects.toBeInstanceOf(
      SessionExpiredError,
    )
  })

  it('em caso de falha de rede, lança NetworkError', async () => {
    server.use(http.post(CATEGORIES_URL, () => HttpResponse.error()))

    await expect(categoriesWriteApi.createCategory('tok-123', payload)).rejects.toBeInstanceOf(
      NetworkError,
    )
  })

  it('em caso de 403, lança ForbiddenError', async () => {
    server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 403 })))

    await expect(categoriesWriteApi.createCategory('tok-123', payload)).rejects.toBeInstanceOf(
      ForbiddenError,
    )
  })
})

describe('categoriesWriteApi.updateCategory', () => {
  it('retorna a categoria atualizada em caso de sucesso', async () => {
    server.use(http.put(CATEGORY_URL, () => HttpResponse.json(category)))

    const result = await categoriesWriteApi.updateCategory('tok-123', 'cat-1', payload)

    expect(result).toEqual(category)
  })

  it('em caso de 404, lança NotFoundError', async () => {
    server.use(http.put(CATEGORY_URL, () => new HttpResponse(null, { status: 404 })))

    await expect(
      categoriesWriteApi.updateCategory('tok-123', 'cat-1', payload),
    ).rejects.toBeInstanceOf(NotFoundError)
  })

  it('em caso de 422 name-conflict, lança NameConflictError', async () => {
    server.use(http.put(CATEGORY_URL, () => problem('name-conflict')))

    await expect(
      categoriesWriteApi.updateCategory('tok-123', 'cat-1', payload),
    ).rejects.toBeInstanceOf(NameConflictError)
  })

  it('em caso de 403, lança ForbiddenError', async () => {
    server.use(http.put(CATEGORY_URL, () => new HttpResponse(null, { status: 403 })))

    await expect(
      categoriesWriteApi.updateCategory('tok-123', 'cat-1', payload),
    ).rejects.toBeInstanceOf(ForbiddenError)
  })
})

describe('categoriesWriteApi.deleteCategory', () => {
  it('em caso de sucesso, resolve sem erro', async () => {
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 204 })))

    await expect(categoriesWriteApi.deleteCategory('tok-123', 'cat-1')).resolves.toBeUndefined()
  })

  it('em caso de 404, lança NotFoundError', async () => {
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 404 })))

    await expect(categoriesWriteApi.deleteCategory('tok-123', 'cat-1')).rejects.toBeInstanceOf(
      NotFoundError,
    )
  })

  it('em caso de 422 category-in-use, lança CategoryInUseError', async () => {
    server.use(http.delete(CATEGORY_URL, () => problem('category-in-use')))

    await expect(categoriesWriteApi.deleteCategory('tok-123', 'cat-1')).rejects.toBeInstanceOf(
      CategoryInUseError,
    )
  })

  it('em caso de 422 com type desconhecido, lança UnknownCategoryError', async () => {
    server.use(http.delete(CATEGORY_URL, () => problem('outro-erro')))

    await expect(categoriesWriteApi.deleteCategory('tok-123', 'cat-1')).rejects.toBeInstanceOf(
      UnknownCategoryError,
    )
  })

  it('em caso de 403, lança ForbiddenError', async () => {
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 403 })))

    await expect(categoriesWriteApi.deleteCategory('tok-123', 'cat-1')).rejects.toBeInstanceOf(
      ForbiddenError,
    )
  })
})
