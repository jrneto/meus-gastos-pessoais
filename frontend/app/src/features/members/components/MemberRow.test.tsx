import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { MemberItem } from '../api/membersApi'
import { MemberRow } from './MemberRow'

const MEMBER_URL = 'http://localhost:5049/members/mem-2'

const member: MemberItem = {
  id: 'mem-2',
  email: 'convidado@email.com',
  role: 'Leitura',
  status: 'ConvitePendente',
  createdAt: '2025-06-16T09:00:00Z',
}

describe('MemberRow', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('modo Titular mostra seletor de papel e ícone de remover', () => {
    render(
      <MemberRow member={member} readOnly={false} isMe={false} onRoleChanged={() => {}} onRemoveRequested={() => {}} />,
    )

    expect(screen.getByLabelText('Leitura')).toBeChecked()
    expect(screen.getByLabelText('Lançar')).not.toBeChecked()
    expect(screen.getByRole('button', { name: 'Remover membro' })).toBeInTheDocument()
  })

  it('trocar o seletor reflete imediatamente e chama PUT', async () => {
    const user = userEvent.setup()
    server.use(http.put(MEMBER_URL, () => HttpResponse.json({ ...member, role: 'Total' })))
    const onRoleChanged = vi.fn()

    render(
      <MemberRow
        member={member}
        readOnly={false}
        isMe={false}
        onRoleChanged={onRoleChanged}
        onRemoveRequested={() => {}}
      />,
    )

    await user.click(screen.getByLabelText('Total'))

    expect(screen.getByLabelText('Total')).toBeChecked()
    expect(await screen.findByLabelText('Total')).toBeChecked()
  })

  it('falha ao trocar o papel reverte o seletor e mostra erro inline', async () => {
    const user = userEvent.setup()
    server.use(http.put(MEMBER_URL, () => new HttpResponse(null, { status: 500 })))

    render(
      <MemberRow member={member} readOnly={false} isMe={false} onRoleChanged={() => {}} onRemoveRequested={() => {}} />,
    )

    await user.click(screen.getByLabelText('Total'))

    expect(await screen.findByText('Ocorreu um erro inesperado. Tente novamente.')).toBeInTheDocument()
    expect(screen.getByLabelText('Leitura')).toBeChecked()
    expect(screen.getByLabelText('Total')).not.toBeChecked()
  })

  it('clicar em remover chama onRemoveRequested com o membro', async () => {
    const user = userEvent.setup()
    const onRemoveRequested = vi.fn()

    render(
      <MemberRow
        member={member}
        readOnly={false}
        isMe={false}
        onRoleChanged={() => {}}
        onRemoveRequested={onRemoveRequested}
      />,
    )

    await user.click(screen.getByRole('button', { name: 'Remover membro' }))

    expect(onRemoveRequested).toHaveBeenCalledWith(member)
  })

  it('modo somente leitura mostra o papel como texto, sem seletor nem remover', () => {
    render(
      <MemberRow member={member} readOnly={true} isMe={false} onRoleChanged={() => {}} onRemoveRequested={() => {}} />,
    )

    expect(screen.getByText('Leitura')).toBeInTheDocument()
    expect(screen.queryByLabelText('Total')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remover membro' })).not.toBeInTheDocument()
  })

  it('isMe mostra o indicador "(você)"', () => {
    render(
      <MemberRow member={member} readOnly={true} isMe={true} onRoleChanged={() => {}} onRemoveRequested={() => {}} />,
    )

    expect(screen.getByText('convidado@email.com (você)')).toBeInTheDocument()
  })
})
