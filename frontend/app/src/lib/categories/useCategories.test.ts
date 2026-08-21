import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError } from './categoryErrors'
import { useCategories } from './useCategories'

const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('useCategories', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega as categorias com sucesso', async () => {
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))

    const { result } = renderHook(() => useCategories())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.items).toEqual([category])
    expect(result.current.error).toBeNull()
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(CATEGORIES_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useCategories())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useCategories())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })
})
