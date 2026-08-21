import { httpClient } from '@/lib/httpClient'
import type { CategoryItem } from '@/lib/categories/types'
import {
  CategoryInUseError,
  NameConflictError,
  NetworkError,
  NotFoundError,
  SessionExpiredError,
  UnknownCategoryError,
  ValidationError,
} from '../errors/categoryErrors'

export interface CategoryPayload {
  nome: string
  cor: string
  icone: string
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

async function extractErrorCode(response: Response): Promise<string | null> {
  try {
    const body = (await response.json()) as { type?: string }
    return body.type?.split('/').pop() ?? null
  } catch {
    return null
  }
}

async function assertWriteOk(response: Response): Promise<void> {
  if (response.status === 400) {
    throw new ValidationError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 422) {
    const code = await extractErrorCode(response)
    throw code === 'name-conflict' ? new NameConflictError() : new UnknownCategoryError()
  }
  if (!response.ok) {
    throw new UnknownCategoryError()
  }
}

async function assertDeleteOk(response: Response): Promise<void> {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 422) {
    const code = await extractErrorCode(response)
    throw code === 'category-in-use' ? new CategoryInUseError() : new UnknownCategoryError()
  }
  if (!response.ok) {
    throw new UnknownCategoryError()
  }
}

async function createCategory(token: string, payload: CategoryPayload): Promise<CategoryItem> {
  const response = await safeFetch(() =>
    httpClient.post('/categories', payload, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  await assertWriteOk(response)
  return response.json() as Promise<CategoryItem>
}

async function updateCategory(
  token: string,
  id: string,
  payload: CategoryPayload,
): Promise<CategoryItem> {
  const response = await safeFetch(() =>
    httpClient.put(`/categories/${id}`, payload, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  await assertWriteOk(response)
  return response.json() as Promise<CategoryItem>
}

async function deleteCategory(token: string, id: string): Promise<void> {
  const response = await safeFetch(() =>
    httpClient.delete(`/categories/${id}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  await assertDeleteOk(response)
}

export const categoriesWriteApi = { createCategory, updateCategory, deleteCategory }
