import { httpClient } from '@/lib/httpClient'
import {
  ForbiddenError,
  InvalidFilterError,
  NetworkError,
  NotFoundError,
  SessionExpiredError,
  UnknownTransactionError,
  UnknownTransactionQueryError,
  UpdateValidationError,
  ValidationError,
} from '../errors/transactionErrors'

interface RegisterTransactionPayload {
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
}

interface RegisterTransactionResponse {
  id: string
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
  createdByUserId: string
  createdByLabel: string
  createdAt: string
}

export interface GetTransactionsParams {
  tipo?: 'despesa' | 'receita'
  yearMonth?: string
  categoryId?: string
  dateFrom?: string
  dateTo?: string
  minAmountInCents?: number
  maxAmountInCents?: number
  cursor?: string
}

export interface TransactionQueryItem {
  id: string
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
  createdByUserId: string
  createdByLabel: string
  createdAt: string
}

export interface GetTransactionsResponse {
  items: TransactionQueryItem[]
  nextCursor: string | null
}

interface UpdateTransactionPayload {
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
}

export interface TransactionDetail {
  id: string
  description: string
  amountInCents: number
  categoryId: string
  tipo: 'despesa' | 'receita'
  date: string
  createdByUserId: string
  createdByLabel: string
  createdAt: string
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

function assertOk(response: Response): void {
  if (response.status === 400) {
    throw new ValidationError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 403) {
    throw new ForbiddenError()
  }
  if (!response.ok) {
    throw new UnknownTransactionError()
  }
}

function assertQueryOk(response: Response): void {
  if (response.status === 400) {
    throw new InvalidFilterError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownTransactionQueryError()
  }
}

function assertDetailOk(response: Response): void {
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownTransactionError()
  }
}

function assertUpdateOk(response: Response): void {
  if (response.status === 400) {
    throw new UpdateValidationError()
  }
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 403) {
    throw new ForbiddenError()
  }
  if (!response.ok) {
    throw new UnknownTransactionError()
  }
}

function assertDeleteOk(response: Response): void {
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 403) {
    throw new ForbiddenError()
  }
  if (!response.ok) {
    throw new UnknownTransactionError()
  }
}

function toQueryString(params: GetTransactionsParams): string {
  const entries = Object.entries(params).filter(([, value]) => value !== undefined && value !== '')
  const search = new URLSearchParams(entries.map(([key, value]) => [key, String(value)]))
  const query = search.toString()
  return query ? `?${query}` : ''
}

async function registerTransaction(
  token: string,
  payload: RegisterTransactionPayload,
): Promise<RegisterTransactionResponse> {
  const response = await safeFetch(() =>
    httpClient.post('/transactions', payload, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response)
  return response.json() as Promise<RegisterTransactionResponse>
}

async function getTransactions(
  token: string,
  params: GetTransactionsParams,
): Promise<GetTransactionsResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/transactions${toQueryString(params)}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertQueryOk(response)
  return response.json() as Promise<GetTransactionsResponse>
}

async function getTransactionById(token: string, id: string): Promise<TransactionDetail> {
  const response = await safeFetch(() =>
    httpClient.get(`/transactions/${id}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertDetailOk(response)
  return response.json() as Promise<TransactionDetail>
}

async function updateTransaction(
  token: string,
  id: string,
  payload: UpdateTransactionPayload,
): Promise<TransactionDetail> {
  const response = await safeFetch(() =>
    httpClient.put(`/transactions/${id}`, payload, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertUpdateOk(response)
  return response.json() as Promise<TransactionDetail>
}

async function deleteTransaction(token: string, id: string): Promise<void> {
  const response = await safeFetch(() =>
    httpClient.delete(`/transactions/${id}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertDeleteOk(response)
}

export const transactionsApi = {
  registerTransaction,
  getTransactions,
  getTransactionById,
  updateTransaction,
  deleteTransaction,
}
