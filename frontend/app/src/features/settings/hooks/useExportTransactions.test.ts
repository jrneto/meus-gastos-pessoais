import { act, renderHook, waitFor } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { EXPORT_FILENAME } from '../api/settingsApi'
import { NetworkError, SessionExpiredError } from '../errors/settingsErrors'
import { useExportTransactions } from './useExportTransactions'

const EXPORT_URL = 'http://localhost:5049/transactions/export'

const downloadBlobMock = vi.hoisted(() => vi.fn())
vi.mock('@/lib/downloadFile', () => ({ downloadBlob: downloadBlobMock }))

describe('useExportTransactions', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    downloadBlobMock.mockClear()
  })

  it('sucesso aciona downloadBlob com o filename fixo e marca success', async () => {
    server.use(http.get(EXPORT_URL, () => HttpResponse.text('data;descricao\r\n')))
    const { result } = renderHook(() => useExportTransactions())

    await act(() => result.current.exportCsv())

    expect(downloadBlobMock).toHaveBeenCalledTimes(1)
    expect(downloadBlobMock.mock.calls[0][1]).toBe(EXPORT_FILENAME)
    // Não usa `toBeInstanceOf(Blob)` — ver settingsApi.test.ts.
    expect(typeof downloadBlobMock.mock.calls[0][0].text).toBe('function')
    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
  })

  it('fica ocupado (isExporting) durante a chamada e volta a false ao final', async () => {
    server.use(http.get(EXPORT_URL, () => HttpResponse.text('data;descricao\r\n')))
    const { result } = renderHook(() => useExportTransactions())

    expect(result.current.isExporting).toBe(false)
    let pending: Promise<void>
    act(() => {
      pending = result.current.exportCsv()
    })
    expect(result.current.isExporting).toBe(true)
    await act(() => pending)
    expect(result.current.isExporting).toBe(false)
  })

  it('em caso de 401, expõe SessionExpiredError, limpa a authStore e não baixa nada', async () => {
    server.use(http.get(EXPORT_URL, () => new HttpResponse(null, { status: 401 })))
    const { result } = renderHook(() => useExportTransactions())

    await act(() => result.current.exportCsv())

    await waitFor(() => expect(result.current.error).toBeInstanceOf(SessionExpiredError))
    expect(useAuthStore.getState().token).toBeNull()
    expect(downloadBlobMock).not.toHaveBeenCalled()
    expect(result.current.success).toBe(false)
  })

  it('em caso de falha de rede, expõe NetworkError sem mexer na authStore', async () => {
    server.use(http.get(EXPORT_URL, () => HttpResponse.error()))
    const { result } = renderHook(() => useExportTransactions())

    await act(() => result.current.exportCsv())

    expect(result.current.error).toBeInstanceOf(NetworkError)
    expect(useAuthStore.getState().token).toBe('tok-123')
    expect(downloadBlobMock).not.toHaveBeenCalled()
  })
})
