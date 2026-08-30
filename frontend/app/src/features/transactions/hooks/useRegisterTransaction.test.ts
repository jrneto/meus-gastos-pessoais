import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, ValidationError } from '../errors/transactionErrors'
import type { ExpenseFormOutput } from '../schemas/transactionSchema'
import { useRegisterExpense } from './useRegisterTransaction'

const EXPENSES_URL = 'http://localhost:5049/expenses'

const validExpense: ExpenseFormOutput = {
  description: 'Almoço no restaurante',
  amount: 4590,
  categoryId: 'cat-1',
  expenseDate: '2025-06-15',
}

describe('useRegisterExpense', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e não deixa erro', async () => {
    server.use(
      http.post(EXPENSES_URL, () =>
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

    const { result } = renderHook(() => useRegisterExpense())

    await act(async () => {
      await result.current.registerExpense(validExpense)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('em caso de 400, expõe ValidationError e success permanece false', async () => {
    server.use(http.post(EXPENSES_URL, () => new HttpResponse(null, { status: 400 })))

    const { result } = renderHook(() => useRegisterExpense())

    await act(async () => {
      await result.current.registerExpense(validExpense)
    })

    expect(result.current.error).toBeInstanceOf(ValidationError)
    expect(result.current.success).toBe(false)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.post(EXPENSES_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useRegisterExpense())

    expect(useAuthStore.getState().token).toBe('tok-123')

    await act(async () => {
      await result.current.registerExpense(validExpense)
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.post(EXPENSES_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useRegisterExpense())

    await act(async () => {
      await result.current.registerExpense(validExpense)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
  })
})
