import { httpClient } from '@/lib/httpClient'
import { NetworkError, SessionExpiredError, UnknownCategoryError } from './categoryErrors'
import type { CategoryItem } from './types'

export interface GetCategoriesResponse {
  items: CategoryItem[]
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

function assertListOk(response: Response): void {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownCategoryError()
  }
}

async function getCategories(token: string): Promise<GetCategoriesResponse> {
  const response = await safeFetch(() =>
    httpClient.get('/categories', {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertListOk(response)
  return response.json() as Promise<GetCategoriesResponse>
}

export const categoriesReadApi = { getCategories }
