import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import {
  CategoryInUseError,
  NotFoundError,
  SessionExpiredError,
} from '../errors/categoryErrors'
import { useDeleteCategory } from './useDeleteCategory'

const CATEGORY_URL = 'http://localhost:5049/categories/cat-1'

function problem(type: string) {
  return HttpResponse.json(
    { status: 422, title: 'Regra de negócio violada', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 422 },
  )
}

describe('useDeleteCategory', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true', async () => {
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 204 })))

    const { result } = renderHook(() => useDeleteCategory())

    await act(async () => {
      await result.current.deleteCategory('cat-1')
    })

    expect(result.current.success).toBe(true)
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useDeleteCategory())

    await act(async () => {
      await result.current.deleteCategory('cat-1')
    })

    expect(result.current.error).toBeInstanceOf(NotFoundError)
  })

  it('em caso de 422 category-in-use, expõe CategoryInUseError', async () => {
    server.use(http.delete(CATEGORY_URL, () => problem('category-in-use')))

    const { result } = renderHook(() => useDeleteCategory())

    await act(async () => {
      await result.current.deleteCategory('cat-1')
    })

    expect(result.current.error).toBeInstanceOf(CategoryInUseError)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useDeleteCategory())

    await act(async () => {
      await result.current.deleteCategory('cat-1')
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })
})
