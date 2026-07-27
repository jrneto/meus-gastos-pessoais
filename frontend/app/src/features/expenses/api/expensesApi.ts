import { httpClient } from '@/lib/httpClient'
import {
  NetworkError,
  SessionExpiredError,
  UnknownExpenseError,
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

export const expensesApi = { registerExpense }
