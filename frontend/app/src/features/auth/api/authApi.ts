import { httpClient } from '@/lib/httpClient'
import {
  AccountNotConfirmedError,
  CpfAlreadyExistsError,
  EmailAlreadyExistsError,
  InvalidConfirmationCodeError,
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

export interface ConfirmPayload {
  email: string
  code: string
}

export interface ResendConfirmationPayload {
  email: string
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
      throw new AccountNotConfirmedError()
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

// Sem corpo em caso de sucesso (200), mesmo padrão de `logout` — o
// backend não devolve nenhum dado adicional (FEAT-35).
async function confirm(payload: ConfirmPayload): Promise<void> {
  const response = await safeFetch(() => httpClient.post('/auth/confirm', payload))

  if (response.status === 400) {
    // `invalid-confirmation-code` (código incorreto) e
    // `expired-confirmation-code` (código expirado ou email
    // inexistente, anti-enumeração) mapeiam pro mesmo erro — ver
    // plan.md "decisão 3": diferenciar na UI arriscaria abrir um canal
    // indireto de enumeração que o backend deliberadamente evitou.
    throw new InvalidConfirmationCodeError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
}

// O backend sempre retorna 200 pra esse endpoint (email inexistente ou
// já confirmado não é revelado, FEAT-35 decisão 3) — não há erro de
// negócio a mapear aqui, só falha técnica.
async function resendConfirmation(payload: ResendConfirmationPayload): Promise<void> {
  const response = await safeFetch(() => httpClient.post('/auth/resend-confirmation', payload))

  if (!response.ok) {
    throw new UnknownAuthError()
  }
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

export const authApi = { login, register, me, refresh, logout, confirm, resendConfirmation }
