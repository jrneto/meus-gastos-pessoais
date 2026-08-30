import { act, renderHook } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NameConflictError, SessionExpiredError, ValidationError } from '../errors/categoryErrors'
import { useRegisterCategory } from './useRegisterCategory'

const CATEGORIES_URL = 'http://localhost:5049/categories'
const payload = { nome: 'Viagem', tipo: 'despesa' as const, orcamentoMensalCents: 50000 }

function problem(type: string) {
  return HttpResponse.json(
    { status: 422, title: 'Regra de negócio violada', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 422 },
  )
}

describe('useRegisterCategory', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('em caso de sucesso, seta success = true e expõe a categoria criada', async () => {
    server.use(http.post(CATEGORIES_URL, () => HttpResponse.json({ id: 'cat-1', ...payload })))

    const { result } = renderHook(() => useRegisterCategory())

    await act(async () => {
      await result.current.registerCategory(payload)
    })

    expect(result.current.success).toBe(true)
    expect(result.current.error).toBeNull()
    expect(result.current.data).toEqual({ id: 'cat-1', ...payload })
  })

  it('em caso de 400, expõe ValidationError', async () => {
    server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 400 })))

    const { result } = renderHook(() => useRegisterCategory())

    await act(async () => {
      await result.current.registerCategory(payload)
    })

    expect(result.current.error).toBeInstanceOf(ValidationError)
  })

  it('em caso de 422 name-conflict, expõe NameConflictError', async () => {
    server.use(http.post(CATEGORIES_URL, () => problem('name-conflict')))

    const { result } = renderHook(() => useRegisterCategory())

    await act(async () => {
      await result.current.registerCategory(payload)
    })

    expect(result.current.error).toBeInstanceOf(NameConflictError)
  })

  it('em caso de 401, expõe SessionExpiredError e limpa a authStore', async () => {
    server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 401 })))

    const { result } = renderHook(() => useRegisterCategory())

    await act(async () => {
      await result.current.registerCategory(payload)
    })

    expect(result.current.error).toBeInstanceOf(SessionExpiredError)
    expect(useAuthStore.getState().token).toBeNull()
  })
})
