import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, ValidationError } from '../errors/transactionErrors'
import type { TransactionFormOutput } from '../schemas/transactionSchema'
import { useRegisterTransaction } from './useRegisterTransaction'

const TRANSACTIONS_URL = 'http://localhost:5049/transactions'

const validTransaction: TransactionFormOutput = {
  description: 'Almoço no restaurante',
  amount: 4590,
  categoryId: 'cat-1',
  date: '2025-06-15',
}

describe('useRegisterTransaction', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e não deixa erro', async () => {
    server.use(
      http.post(TRANSACTIONS_URL, () =>
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

    const { result } = renderHook(() => useRegisterTransaction())

    await act(async () => {
      await result.current.registerTransaction(validTransaction)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('envia tipo "despesa" fixo no payload', async () => {
    let receivedBody: unknown = null
    server.use(
      http.post(TRANSACTIONS_URL, async ({ request }) => {
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

    const { result } = renderHook(() => useRegisterTransaction())

    await act(async () => {
      await result.current.registerTransaction(validTransaction)
    })

    expect(receivedBody).toMatchObject({ tipo: 'despesa', date: validTransaction.date })
  })

  it('em caso de 400, expõe ValidationError e success permanece false', async () => {
    server.use(http.post(TRANSACTIONS_URL, () => new HttpResponse(null, { status: 400 })))

    const { result } = renderHook(() => useRegisterTransaction())

    await act(async () => {
      await result.current.registerTransaction(validTransaction)
    })

    expect(result.current.error).toBeInstanceOf(ValidationError)
    expect(result.current.success).toBe(false)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.post(TRANSACTIONS_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useRegisterTransaction())

    expect(useAuthStore.getState().token).toBe('tok-123')

    await act(async () => {
      await result.current.registerTransaction(validTransaction)
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.post(TRANSACTIONS_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useRegisterTransaction())

    await act(async () => {
      await result.current.registerTransaction(validTransaction)
    })

    expect(result.current.error).toBeInstanceOf(NetworkError)
  })
})
