import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import {
  NameConflictError,
  NotFoundError,
  SessionExpiredError,
} from '../errors/categoryErrors'
import { useUpdateCategory } from './useUpdateCategory'

const CATEGORY_URL = 'http://localhost:5049/categories/cat-1'
const payload = { nome: 'Viagem', tipo: 'despesa' as const, orcamentoMensalCents: 50000 }

function problem(type: string) {
  return HttpResponse.json(
    { status: 422, title: 'Regra de negócio violada', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 422 },
  )
}

describe('useUpdateCategory', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e expõe a categoria atualizada', async () => {
    server.use(http.put(CATEGORY_URL, () => HttpResponse.json({ id: 'cat-1', ...payload })))

    const { result } = renderHook(() => useUpdateCategory('cat-1'))

    await act(async () => {
      await result.current.updateCategory(payload)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.data).toEqual({ id: 'cat-1', ...payload })
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.put(CATEGORY_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useUpdateCategory('cat-1'))

    await act(async () => {
      await result.current.updateCategory(payload)
    })

    expect(result.current.error).toBeInstanceOf(NotFoundError)
  })

  it('em caso de 422 name-conflict, expõe NameConflictError', async () => {
    server.use(http.put(CATEGORY_URL, () => problem('name-conflict')))

    const { result } = renderHook(() => useUpdateCategory('cat-1'))

    await act(async () => {
      await result.current.updateCategory(payload)
    })

    expect(result.current.error).toBeInstanceOf(NameConflictError)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.put(CATEGORY_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useUpdateCategory('cat-1'))

    await act(async () => {
      await result.current.updateCategory(payload)
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })
})
