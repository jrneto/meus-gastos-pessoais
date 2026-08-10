import { httpClient } from '@/lib/httpClient'
import {
  InvalidCredentialsError,
  NetworkError,
  RefreshFailedError,
  UnknownAuthError,
} from '../errors/authErrors'
import type { LoginCredentials } from '../schemas/loginSchema'

interface LoginResponse {
  accessToken: string
  expiresIn: number
  userId: string
}

interface MeResponse {
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
    throw new InvalidCredentialsError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
}

async function login(credentials: LoginCredentials): Promise<LoginResponse> {
  const response = await safeFetch(() => httpClient.post('/auth/login', credentials))
  assertOk(response)
  return response.json() as Promise<LoginResponse>
}

async function me(token: string): Promise<MeResponse> {
  const response = await safeFetch(() =>
    httpClient.get('/auth/me', { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertOk(response)
  return response.json() as Promise<MeResponse>
}

// Sem request body — o refreshToken é lido pelo backend a partir do
// cookie httpOnly (Path=/auth), nunca do corpo da requisição.
async function refresh(): Promise<LoginResponse> {
  const response = await safeFetch(() => httpClient.post('/auth/refresh'))
  if (response.status === 401) {
    throw new RefreshFailedError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
  return response.json() as Promise<LoginResponse>
}

// Idempotente no backend (200 mesmo sem cookie presente) — erros são
// tratados pelo chamador (ver features/auth/hooks/useLogout.ts).
async function logout(): Promise<void> {
  const response = await safeFetch(() => httpClient.post('/auth/logout'))
  if (!response.ok) {
    throw new UnknownAuthError()
  }
}

export const authApi = { login, me, refresh, logout }
