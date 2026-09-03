import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import { server } from '@/test/msw/server'
import { NetworkError, SessionExpiredError, UnknownPermissionError } from './permissionErrors'
import { membershipReadApi } from './membershipReadApi'

const MEMBERS_URL = 'http://localhost:5049/members'

const member = {
  email: 'titular@email.com',
  role: 'Titular',
}

describe('membershipReadApi.getMembers', () => {
  it('retorna a lista de membros em caso de sucesso', async () => {
    server.use(http.get(MEMBERS_URL, () => HttpResponse.json({ items: [member] })))

    const result = await membershipReadApi.getMembers('tok-123')

    expect(result).toEqual({ items: [member] })
  })

  it('em caso de 401, lança SessionExpiredError', async () => {
    server.use(http.get(MEMBERS_URL, () => new HttpResponse(null, { status: 401 })))

    await expect(membershipReadApi.getMembers('tok-123')).rejects.toBeInstanceOf(
      SessionExpiredError,
    )
  })

  it('em caso de erro inesperado (5xx), lança UnknownPermissionError', async () => {
    server.use(http.get(MEMBERS_URL, () => new HttpResponse(null, { status: 500 })))

    await expect(membershipReadApi.getMembers('tok-123')).rejects.toBeInstanceOf(
      UnknownPermissionError,
    )
  })

  it('em caso de falha de rede, lança NetworkError', async () => {
    server.use(http.get(MEMBERS_URL, () => HttpResponse.error()))

    await expect(membershipReadApi.getMembers('tok-123')).rejects.toBeInstanceOf(NetworkError)
  })
})
