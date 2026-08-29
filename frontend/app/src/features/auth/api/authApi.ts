import { httpClient } from '@/lib/httpClient'
import {
  AccountPendingApprovalError,
  CpfAlreadyExistsError,
  EmailAlreadyExistsError,
  InvalidCredentialsError,
  NetworkError,
  RefreshFailedError,
  RegisterValidationError,
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

export interface RegisterPayload {
  email: string
  password: string
  name: string
  phoneNumber: string
  cpf: string
}

export interface RegisterResponse {
  userId: string
  email: string
  name: string
  phoneNumber: string
  cpf: string
}

interface ProblemDetails {
  type?: string
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

async function readProblemType(response: Response): Promise<string | undefined> {
  const problem = (await response.json().catch(() => null)) as ProblemDetails | null
  return problem?.type
}

async function login(credentials: LoginCredentials): Promise<LoginResponse> {
  const response = await safeFetch(() => httpClient.post('/auth/login', credentials))

  if (response.status === 401) {
    const type = await readProblemType(response)
    if (type?.endsWith('user-not-confirmed')) {
      throw new AccountPendingApprovalError()
    }
    throw new InvalidCredentialsError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
  return response.json() as Promise<LoginResponse>
}

async function register(payload: RegisterPayload): Promise<RegisterResponse> {
  const response = await safeFetch(() => httpClient.post('/auth/register', payload))

  if (response.status === 409) {
    const type = await readProblemType(response)
    if (type?.endsWith('email-already-exists')) {
      throw new EmailAlreadyExistsError()
    }
    if (type?.endsWith('cpf-already-exists')) {
      throw new CpfAlreadyExistsError()
    }
    throw new UnknownAuthError()
  }
  if (response.status === 400) {
    throw new RegisterValidationError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
  return response.json() as Promise<RegisterResponse>
}

async function me(token: string): Promise<MeResponse> {
  const response = await safeFetch(() =>
    httpClient.get('/auth/me', { headers: { Authorization: `Bearer ${token}` } }),
  )
  if (response.status === 401) {
    throw new InvalidCredentialsError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
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

export const authApi = { login, register, me, refresh, logout }
