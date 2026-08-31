import { httpClient } from '@/lib/httpClient'
import {
  CannotModifyTitularError,
  CannotRemoveTitularError,
  ConflictError,
  ForbiddenError,
  NetworkError,
  NotFoundError,
  SessionExpiredError,
  UnknownMemberError,
  ValidationError,
} from '../errors/memberErrors'

export type MemberRole = 'Leitura' | 'Lancar' | 'Total' | 'Titular'
export type MemberStatus = 'ConvitePendente' | 'Ativo'

export interface MemberItem {
  id: string
  email: string
  role: MemberRole
  status: MemberStatus
  createdAt: string
}

export interface InviteMemberPayload {
  email: string
  role: Exclude<MemberRole, 'Titular'>
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> {
  try {
    return await fn()
  } catch {
    throw new NetworkError()
  }
}

async function extractErrorCode(response: Response): Promise<string | null> {
  try {
    const body = (await response.json()) as { type?: string }
    return body.type?.split('/').pop() ?? null
  } catch {
    return null
  }
}

function assertListOk(response: Response): void {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (!response.ok) {
    throw new UnknownMemberError()
  }
}

async function assertInviteOk(response: Response): Promise<void> {
  if (response.status === 400) {
    throw new ValidationError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 403) {
    throw new ForbiddenError()
  }
  if (response.status === 409) {
    throw new ConflictError()
  }
  if (!response.ok) {
    throw new UnknownMemberError()
  }
}

async function assertUpdateOk(response: Response): Promise<void> {
  if (response.status === 400) {
    throw new ValidationError()
  }
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 403) {
    throw new ForbiddenError()
  }
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 422) {
    const code = await extractErrorCode(response)
    throw code === 'cannot-modify-titular' ? new CannotModifyTitularError() : new UnknownMemberError()
  }
  if (!response.ok) {
    throw new UnknownMemberError()
  }
}

async function assertRemoveOk(response: Response): Promise<void> {
  if (response.status === 401) {
    throw new SessionExpiredError()
  }
  if (response.status === 403) {
    throw new ForbiddenError()
  }
  if (response.status === 404) {
    throw new NotFoundError()
  }
  if (response.status === 422) {
    const code = await extractErrorCode(response)
    throw code === 'cannot-remove-titular' ? new CannotRemoveTitularError() : new UnknownMemberError()
  }
  if (!response.ok) {
    throw new UnknownMemberError()
  }
}

async function getMembers(token: string): Promise<{ items: MemberItem[] }> {
  const response = await safeFetch(() =>
    httpClient.get('/members', { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertListOk(response)
  return response.json() as Promise<{ items: MemberItem[] }>
}

async function inviteMember(token: string, payload: InviteMemberPayload): Promise<MemberItem> {
  const response = await safeFetch(() =>
    httpClient.post('/members', payload, { headers: { Authorization: `Bearer ${token}` } }),
  )
  await assertInviteOk(response)
  return response.json() as Promise<MemberItem>
}

async function updateMemberRole(
  token: string,
  id: string,
  role: Exclude<MemberRole, 'Titular'>,
): Promise<MemberItem> {
  const response = await safeFetch(() =>
    httpClient.put(`/members/${id}`, { role }, { headers: { Authorization: `Bearer ${token}` } }),
  )
  await assertUpdateOk(response)
  return response.json() as Promise<MemberItem>
}

async function removeMember(token: string, id: string): Promise<void> {
  const response = await safeFetch(() =>
    httpClient.delete(`/members/${id}`, { headers: { Authorization: `Bearer ${token}` } }),
  )
  await assertRemoveOk(response)
}

export const membersApi = { getMembers, inviteMember, updateMemberRole, removeMember }
