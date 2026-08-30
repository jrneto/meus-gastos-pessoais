import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, NotFoundError, SessionExpiredError } from '../errors/transactionErrors'
import { useDeleteExpense } from './useDeleteTransaction'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

describe('useDeleteExpense', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e não deixa erro', async () => {
    server.use(http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 204 })))

    const { result } = renderHook(() => useDeleteExpense())

    await act(async () => {
      await result.current.deleteExpense('exp-1')
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useDeleteExpense())

    await act(async () => {
      await result.current.deleteExpense('exp-1')
    })

    expect(result.current.error).toBeInstanceOf(NotFoundError)
    expect(result.current.success).toBe(false)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.delete(EXPENSE_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useDeleteExpense())

    await act(async () => {
      await result.current.deleteExpense('exp-1')
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.delete(EXPENSE_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useDeleteExpense())

    await act(async () => {
      await result.current.deleteExpense('exp-1')
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
  })
})
