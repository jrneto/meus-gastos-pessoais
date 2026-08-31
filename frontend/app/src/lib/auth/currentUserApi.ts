import { httpClient } from '@/lib/httpClient'
import { NetworkError, SessionExpiredError, UnknownCurrentUserError } from './currentUserErrors'

export interface CurrentUser {
  userId: string
  email: string
  name: string
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

function assertOk(response: Response): void {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownCurrentUserError()
  }
}

async function getCurrentUser(token: string): Promise<CurrentUser> {
  const response = await safeFetch(() =>
    httpClient.get('/auth/me', {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response)
  return response.json() as Promise<CurrentUser>
}

export const currentUserApi = { getCurrentUser }
