import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import type { MemberItem } from '../api/membersApi'
import { MemberList } from './MemberList'

const titular: MemberItem = {
  id: 'mem-1',
  email: 'titular@email.com',
  role: 'Titular',
  status: 'Ativo',
  createdAt: '2025-06-15T12:34:56Z',
}

const member: MemberItem = {
  id: 'mem-2',
  email: 'convidado@email.com',
  role: 'Leitura',
  status: 'ConvitePendente',
  createdAt: '2025-06-16T09:00:00Z',
}

describe('MemberList', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('destaca a linha do Titular como "Você (titular)" quando o viewer é o Titular', () => {
    render(
      <MemberList
        titular={titular}
        others={[member]}
        isViewerTitular={true}
        currentUserEmail="titular@email.com"
        onRoleChanged={() => {}}
        onRemoveRequested={() => {}}
      />,
    )

    expect(screen.getByText('Você (titular)')).toBeInTheDocument()
    expect(screen.getByText('Titular')).toBeInTheDocument()
    expect(screen.getByText('Acesso total · gerencia membros')).toBeInTheDocument()
  })

  it('mostra o e-mail do Titular quando o viewer não é ele', () => {
    render(
      <MemberList
        titular={titular}
        others={[member]}
        isViewerTitular={false}
        currentUserEmail="convidado@email.com"
        onRoleChanged={() => {}}
        onRemoveRequested={() => {}}
      />,
    )

    expect(screen.getByText('titular@email.com')).toBeInTheDocument()
    expect(screen.queryByText('Você (titular)')).not.toBeInTheDocument()
  })

  it('Titular vê a linha do membro com ações (seletor e remover)', () => {
    render(
      <MemberList
        titular={titular}
        others={[member]}
        isViewerTitular={true}
        currentUserEmail="titular@email.com"
        onRoleChanged={() => {}}
        onRemoveRequested={() => {}}
      />,
    )

    expect(screen.getByLabelText('Leitura')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Remover membro' })).toBeInTheDocument()
  })

  it('não-Titular vê a linha do membro sem ações', () => {
    render(
      <MemberList
        titular={titular}
        others={[member]}
        isViewerTitular={false}
        currentUserEmail="convidado@email.com"
        onRoleChanged={() => {}}
        onRemoveRequested={() => {}}
      />,
    )

    expect(screen.queryByLabelText('Leitura')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remover membro' })).not.toBeInTheDocument()
    expect(screen.getByText('convidado@email.com (você)')).toBeInTheDocument()
  })
})
