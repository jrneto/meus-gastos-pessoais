import { httpClient } from '@/lib/httpClient'
import { NetworkError, SessionExpiredError, UnknownPermissionError } from './permissionErrors'
import type { MemberRole } from './types'

export interface MembershipItem {
  email: string
  role: MemberRole
}

export interface GetMembersResponse {
  items: MembershipItem[]
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

function assertListOk(response: Response): void {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownPermissionError()
  }
}

async function getMembers(token: string): Promise<GetMembersResponse> {
  const response = await safeFetch(() =>
    httpClient.get('/members', { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertListOk(response)
  return response.json() as Promise<GetMembersResponse>
}

export const membershipReadApi = { getMembers }
