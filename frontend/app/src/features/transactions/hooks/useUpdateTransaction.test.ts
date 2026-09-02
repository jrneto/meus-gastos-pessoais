import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NotFoundError, SessionExpiredError, UpdateValidationError } from '../errors/transactionErrors'
import type { TransactionFormOutput } from '../schemas/transactionSchema'
import { useUpdateTransaction } from './useUpdateTransaction'

const TRANSACTION_URL = 'http://localhost:5049/transactions/tx-1'

const validTransaction: TransactionFormOutput = {
  description: 'Almoço no restaurante',
  amount: 5290,
  categoryId: 'cat-1',
  date: '2025-06-16',
}

describe('useUpdateTransaction', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e não deixa erro', async () => {
    server.use(
      http.put(TRANSACTION_URL, () =>
        HttpResponse.json({
          id: 'tx-1',
          description: validTransaction.description,
          amountInCents: validTransaction.amount,
          categoryId: validTransaction.categoryId,
          tipo: 'despesa',
          date: validTransaction.date,
          createdByUserId: 'user-1',
          createdByLabel: 'Você',
          createdAt: '2025-06-15T12:00:00Z',
        }),
      ),
    )

    const { result } = renderHook(() => useUpdateTransaction('tx-1', 'despesa'))

    await act(async () => {
      await result.current.updateTransaction(validTransaction)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('envia o tipo recebido pelo hook (despesa) no payload', async () => {
    let receivedBody: unknown = null
    server.use(
      http.put(TRANSACTION_URL, async ({ request }) => {
        receivedBody = await request.json()
        return HttpResponse.json({
          id: 'tx-1',
          description: validTransaction.description,
          amountInCents: validTransaction.amount,
          categoryId: validTransaction.categoryId,
          tipo: 'despesa',
          date: validTransaction.date,
          createdByUserId: 'user-1',
          createdByLabel: 'Você',
          createdAt: '2025-06-15T12:00:00Z',
        })
      }),
    )

    const { result } = renderHook(() => useUpdateTransaction('tx-1', 'despesa'))

    await act(async () => {
      await result.current.updateTransaction(validTransaction)
    })

    expect(receivedBody).toMatchObject({ tipo: 'despesa', date: validTransaction.date })
  })

  it('envia o tipo recebido pelo hook (receita) no payload', async () => {
    let receivedBody: unknown = null
    server.use(
      http.put(TRANSACTION_URL, async ({ request }) => {
        receivedBody = await request.json()
        return HttpResponse.json({
          id: 'tx-1',
          description: validTransaction.description,
          amountInCents: validTransaction.amount,
          categoryId: validTransaction.categoryId,
          tipo: 'receita',
          date: validTransaction.date,
          createdByUserId: 'user-1',
          createdByLabel: 'Você',
          createdAt: '2025-06-15T12:00:00Z',
        })
      }),
    )

    const { result } = renderHook(() => useUpdateTransaction('tx-1', 'receita'))

    await act(async () => {
      await result.current.updateTransaction(validTransaction)
    })

    expect(receivedBody).toMatchObject({ tipo: 'receita', date: validTransaction.date })
  })

  it('em caso de 400, expõe UpdateValidationError', async () => {
    server.use(http.put(TRANSACTION_URL, () => new HttpResponse(null, { status: 400 })))

    const { result } = renderHook(() => useUpdateTransaction('tx-1', 'despesa'))

    await act(async () => {
      await result.current.updateTransaction(validTransaction)
    })

    expect(result.current.error).toBeInstanceOf(UpdateValidationError)
    expect(result.current.success).toBe(false)
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.put(TRANSACTION_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useUpdateTransaction('tx-1', 'despesa'))

    await act(async () => {
      await result.current.updateTransaction(validTransaction)
    })

    expect(result.current.error).toBeInstanceOf(NotFoundError)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.put(TRANSACTION_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useUpdateTransaction('tx-1', 'despesa'))

    await act(async () => {
      await result.current.updateTransaction(validTransaction)
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })
})
