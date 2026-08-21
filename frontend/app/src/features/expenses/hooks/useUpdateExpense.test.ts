import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NotFoundError, SessionExpiredError, UpdateValidationError } from '../errors/expenseErrors'
import type { ExpenseFormOutput } from '../schemas/expenseSchema'
import { useUpdateExpense } from './useUpdateExpense'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

const validExpense: ExpenseFormOutput = {
  description: 'Almoço no restaurante',
  amount: 5290,
  categoryId: 'cat-1',
  expenseDate: '2025-06-16',
}

describe('useUpdateExpense', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e não deixa erro', async () => {
    server.use(
      http.put(EXPENSE_URL, () =>
        HttpResponse.json({
          id: 'exp-1',
          description: validExpense.description,
          amountInCents: validExpense.amount,
          categoryId: validExpense.categoryId,
          expenseDate: validExpense.expenseDate,
          createdAt: '2025-06-15T12:00:00Z',
        }),
      ),
    )

    const { result } = renderHook(() => useUpdateExpense('exp-1'))

    await act(async () => {
      await result.current.updateExpense(validExpense)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('em caso de 400, expõe UpdateValidationError', async () => {
    server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 400 })))

    const { result } = renderHook(() => useUpdateExpense('exp-1'))

    await act(async () => {
      await result.current.updateExpense(validExpense)
    })

    expect(result.current.error).toBeInstanceOf(UpdateValidationError)
    expect(result.current.success).toBe(false)
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useUpdateExpense('exp-1'))

    await act(async () => {
      await result.current.updateExpense(validExpense)
    })

    expect(result.current.error).toBeInstanceOf(NotFoundError)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useUpdateExpense('exp-1'))

    await act(async () => {
      await result.current.updateExpense(validExpense)
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })
})
