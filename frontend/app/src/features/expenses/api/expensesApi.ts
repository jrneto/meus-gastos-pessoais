import { httpClient } from '@/lib/httpClient'
import {
  InvalidFilterError,
  NetworkError,
  SessionExpiredError,
  UnknownExpenseError,
  UnknownExpenseQueryError,
  ValidationError,
} from '../errors/expenseErrors'

interface RegisterExpensePayload {
  description: string
  amountInCents: number
  category: string
  expenseDate: string
}

interface RegisterExpenseResponse {
  id: string
  description: string
  amountInCents: number
  category: string
  expenseDate: string
  createdAt: string
}

export interface GetExpensesParams {
  yearMonth?: string
  category?: string
  dateFrom?: string
  dateTo?: string
  minAmountInCents?: number
  maxAmountInCents?: number
  cursor?: string
}

export interface ExpenseQueryItem {
  id: string
  description: string
  amountInCents: number
  category: string
  expenseDate: string
  createdAt: string
}

export interface GetExpensesResponse {
  items: ExpenseQueryItem[]
  nextCursor: string | null
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
  if (!response.ok) {
    throw new UnknownExpenseError()
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
    throw new UnknownExpenseQueryError()
  }
}

function toQueryString(params: GetExpensesParams): string {
  const entries = Object.entries(params).filter(([, value]) => value !== undefined && value !== '')
  const search = new URLSearchParams(entries.map(([key, value]) => [key, String(value)]))
  const query = search.toString()
  return query ? `?${query}` : ''
}

async function registerExpense(
  token: string,
  payload: RegisterExpensePayload,
): Promise<RegisterExpenseResponse> {
  const response = await safeFetch(() =>
    httpClient.post('/expenses', payload, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response)
  return response.json() as Promise<RegisterExpenseResponse>
}

async function getExpenses(
  token: string,
  params: GetExpensesParams,
): Promise<GetExpensesResponse> {
  const response = await safeFetch(() =>
    httpClient.get(`/expenses${toQueryString(params)}`, {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertQueryOk(response)
  return response.json() as Promise<GetExpensesResponse>
}

export const expensesApi = { registerExpense, getExpenses }
