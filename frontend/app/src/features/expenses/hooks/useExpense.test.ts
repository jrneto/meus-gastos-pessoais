import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, NotFoundError, SessionExpiredError } from '../errors/expenseErrors'
import { useExpense } from './useExpense'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

const expenseDetail = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  category: 'Alimentacao',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('useExpense', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega a despesa com sucesso', async () => {
    server.use(http.get(EXPENSE_URL, () => HttpResponse.json(expenseDetail)))

    const { result } = renderHook(() => useExpense('exp-1'))

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.data).toEqual(expenseDetail)
    expect(result.current.error).toBeNull()
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.get(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useExpense('exp-1'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NotFoundError))
    expect(result.current.data).toBeNull()
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(EXPENSE_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useExpense('exp-1'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(EXPENSE_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useExpense('exp-1'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })
})
