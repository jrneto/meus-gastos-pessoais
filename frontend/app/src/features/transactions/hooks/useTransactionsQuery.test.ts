import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { InvalidFilterError, SessionExpiredError } from '../errors/transactionErrors'
import type { TransactionFilterOutput } from '../schemas/transactionFilterSchema'
import { useTransactionsQuery } from './useTransactionsQuery'

const TRANSACTIONS_URL = 'http://localhost:5049/transactions'

function item(id: string) {
  return {
    id,
    description: `Despesa ${id}`,
    amountInCents: 1000,
    categoryId: 'cat-1',
    tipo: 'despesa',
    date: '2025-06-15',
    createdByUserId: 'user-1',
    createdByLabel: 'Você',
    createdAt: '2025-06-15T12:00:00Z',
  }
}

describe('useTransactionsQuery', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega a primeira página sem filtros ao montar', async () => {
    server.use(
      http.get(TRANSACTIONS_URL, () =>
        HttpResponse.json({ items: [item('1'), item('2')], nextCursor: null }),
      ),
    )

    const { result } = renderHook(() => useTransactionsQuery())

    await waitFor(() => expect(result.current.isLoading).toBe(false))

    expect(result.current.items).toHaveLength(2)
    expect(result.current.hasMore).toBe(false)
  })

  it('applyFilters reinicia items/cursor antes de buscar a nova página', async () => {
    server.use(
      http.get(TRANSACTIONS_URL, () =>
        HttpResponse.json({ items: [item('1')], nextCursor: 'cursor-1' }),
      ),
    )

    const { result } = renderHook(() => useTransactionsQuery())
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.hasMore).toBe(true)

    server.use(
      http.get(TRANSACTIONS_URL, () =>
        HttpResponse.json({ items: [item('2')], nextCursor: null }),
      ),
    )

    const filters: TransactionFilterOutput = {
      yearMonth: undefined,
      categoryId: 'cat-2',
      dateFrom: undefined,
      dateTo: undefined,
      minAmountInCents: undefined,
      maxAmountInCents: undefined,
    }
    act(() => {
      result.current.applyFilters(filters)
    })

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.items).toEqual([item('2')])
    expect(result.current.hasMore).toBe(false)
  })

  it('loadMore anexa a próxima página usando nextCursor', async () => {
    server.use(
      http.get(TRANSACTIONS_URL, ({ request }) => {
        const cursor = new URL(request.url).searchParams.get('cursor')
        if (!cursor) {
          return HttpResponse.json({ items: [item('1')], nextCursor: 'cursor-1' })
        }
        return HttpResponse.json({ items: [item('2')], nextCursor: null })
      }),
    )

    const { result } = renderHook(() => useTransactionsQuery())
    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.items).toEqual([item('1')])

    act(() => {
      result.current.loadMore()
    })

    await waitFor(() => expect(result.current.isLoadingMore).toBe(false))
    expect(result.current.items).toEqual([item('1'), item('2')])
    expect(result.current.hasMore).toBe(false)
  })

  it('em caso de 400, expõe InvalidFilterError', async () => {
    server.use(http.get(TRANSACTIONS_URL, () => new HttpResponse(null, { status: 400 })))

    const { result } = renderHook(() => useTransactionsQuery())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(InvalidFilterError))
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(TRANSACTIONS_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useTransactionsQuery())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('lista vazia quando nenhum item corresponde aos filtros', async () => {
    server.use(http.get(TRANSACTIONS_URL, () => HttpResponse.json({ items: [], nextCursor: null })))

    const { result } = renderHook(() => useTransactionsQuery())

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.items).toEqual([])
    expect(result.current.error).toBeNull()
  })

  it('refetch reexecuta a busca com os filtros atuais, voltando para a primeira página', async () => {
    server.use(
      http.get(TRANSACTIONS_URL, () =>
        HttpResponse.json({ items: [item('1')], nextCursor: 'cursor-1' }),
      ),
    )

    const { result } = renderHook(() => useTransactionsQuery())
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    const filters: TransactionFilterOutput = {
      yearMonth: undefined,
      categoryId: 'cat-2',
      dateFrom: undefined,
      dateTo: undefined,
      minAmountInCents: undefined,
      maxAmountInCents: undefined,
    }
    act(() => {
      result.current.applyFilters(filters)
    })
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    let requestedCategoryId: string | null = null
    server.use(
      http.get(TRANSACTIONS_URL, ({ request }) => {
        requestedCategoryId = new URL(request.url).searchParams.get('categoryId')
        return HttpResponse.json({ items: [item('3')], nextCursor: null })
      }),
    )

    act(() => {
      result.current.refetch()
    })

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.items).toEqual([item('3')])
    expect(result.current.hasMore).toBe(false)
    expect(requestedCategoryId).toBe('cat-2')
  })

  it('removeItem remove só o item do id informado, mantendo os demais', async () => {
    server.use(
      http.get(TRANSACTIONS_URL, () =>
        HttpResponse.json({ items: [item('1'), item('2')], nextCursor: null }),
      ),
    )

    const { result } = renderHook(() => useTransactionsQuery())
    await waitFor(() => expect(result.current.isLoading).toBe(false))

    act(() => {
      result.current.removeItem('1')
    })

    expect(result.current.items).toEqual([item('2')])
  })
})
