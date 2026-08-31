import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { MemberItem } from '../api/membersApi'
import { MemberRemoveDialog } from './MemberRemoveDialog'

const MEMBER_URL = 'http://localhost:5049/members/mem-2'

const member: MemberItem = {
  id: 'mem-2',
  email: 'convidado@email.com',
  role: 'Leitura',
  status: 'ConvitePendente',
  createdAt: '2025-06-16T09:00:00Z',
}

describe('MemberRemoveDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('fica fechado quando member é null', () => {
    render(<MemberRemoveDialog member={null} onOpenChange={vi.fn()} onRemoved={vi.fn()} />)

    expect(screen.queryByText('Remover membro')).not.toBeInTheDocument()
  })

  it('aberto exibe o e-mail do membro', () => {
    render(<MemberRemoveDialog member={member} onOpenChange={vi.fn()} onRemoved={vi.fn()} />)

    expect(screen.getByText('Remover membro')).toBeInTheDocument()
    expect(screen.getByText(/convidado@email\.com/)).toBeInTheDocument()
  })

  it('cancelar não chama a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.delete(MEMBER_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const onOpenChange = vi.fn()

    render(<MemberRemoveDialog member={member} onOpenChange={onOpenChange} onRemoved={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(apiCalled).toBe(false)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('confirmar com sucesso chama a API e onRemoved', async () => {
    const user = userEvent.setup()
    server.use(http.delete(MEMBER_URL, () => new HttpResponse(null, { status: 204 })))
    const onRemoved = vi.fn()

    render(<MemberRemoveDialog member={member} onOpenChange={vi.fn()} onRemoved={onRemoved} />)
    await user.click(screen.getByRole('button', { name: /^remover$/i }))

    await waitFor(() => expect(onRemoved).toHaveBeenCalledWith('mem-2'))
  })

  it('confirmar com 404 chama onRemoved (membro já não existia)', async () => {
    const user = userEvent.setup()
    server.use(http.delete(MEMBER_URL, () => new HttpResponse(null, { status: 404 })))
    const onRemoved = vi.fn()

    render(<MemberRemoveDialog member={member} onOpenChange={vi.fn()} onRemoved={onRemoved} />)
    await user.click(screen.getByRole('button', { name: /^remover$/i }))

    await waitFor(() => expect(onRemoved).toHaveBeenCalledWith('mem-2'))
  })

  it('confirmar com erro inesperado mantém o dialog aberto com alerta, sem chamar onRemoved', async () => {
    const user = userEvent.setup()
    server.use(http.delete(MEMBER_URL, () => new HttpResponse(null, { status: 500 })))
    const onRemoved = vi.fn()

    render(<MemberRemoveDialog member={member} onOpenChange={vi.fn()} onRemoved={onRemoved} />)
    await user.click(screen.getByRole('button', { name: /^remover$/i }))

    expect(await screen.findByText('Não foi possível remover')).toBeInTheDocument()
    expect(screen.getByText('Remover membro')).toBeInTheDocument()
    expect(onRemoved).not.toHaveBeenCalled()
  })
})
