import { renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NetworkError, NotFoundError, SessionExpiredError } from '../errors/transactionErrors'
import { useTransaction } from './useTransaction'

const TRANSACTION_URL = 'http://localhost:5049/transactions/tx-1'

const transactionDetail = {
  id: 'tx-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  tipo: 'despesa',
  date: '2025-06-15',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('useTransaction', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega a transação com sucesso', async () => {
    server.use(http.get(TRANSACTION_URL, () => HttpResponse.json(transactionDetail)))

    const { result } = renderHook(() => useTransaction('tx-1'))

    await waitFor(() => expect(result.current.isLoading).toBe(false))
    expect(result.current.data).toEqual(transactionDetail)
    expect(result.current.error).toBeNull()
  })

  it('em caso de 404, expõe NotFoundError', async () => {
    server.use(http.get(TRANSACTION_URL, () => new HttpResponse(null, { status: 404 })))

    const { result } = renderHook(() => useTransaction('tx-1'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NotFoundError))
    expect(result.current.data).toBeNull()
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.get(TRANSACTION_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useTransaction('tx-1'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
  })

  it('em caso de falha de rede, expõe NetworkError', async () => {
    server.use(http.get(TRANSACTION_URL, () => HttpResponse.error()))

    const { result } = renderHook(() => useTransaction('tx-1'))

    await waitFor(() => expect(result.current.error).toBeInstanceOf(NetworkError))
  })

  it('id vazio não chama a API e não fica em carregamento (FEAT-18)', async () => {
    let apiCalled = false
    server.use(
      http.get(TRANSACTION_URL, () => {
        apiCalled = true
        return HttpResponse.json(transactionDetail)
      }),
    )

    const { result } = renderHook(() => useTransaction(''))

    expect(result.current.isLoading).toBe(false)
    expect(result.current.data).toBeNull()
    expect(result.current.error).toBeNull()
    expect(apiCalled).toBe(false)
  })
})
