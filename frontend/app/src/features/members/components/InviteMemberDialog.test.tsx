import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { InviteMemberDialog } from './InviteMemberDialog'

const MEMBERS_URL = 'http://localhost:5049/members'

describe('InviteMemberDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('fica fechado quando open é false', () => {
    render(<InviteMemberDialog open={false} onOpenChange={vi.fn()} onInvited={vi.fn()} />)

    expect(screen.queryByText('Convidar pessoa')).not.toBeInTheDocument()
  })

  it('papel inicial é "Lançar", com a descrição correspondente', () => {
    render(<InviteMemberDialog open={true} onOpenChange={vi.fn()} onInvited={vi.fn()} />)

    expect(screen.getByLabelText('Lançar')).toBeChecked()
    expect(screen.getByText('Pode visualizar e lançar novas despesas.')).toBeInTheDocument()
  })

  it('trocar o papel atualiza a descrição exibida', async () => {
    const user = userEvent.setup()
    render(<InviteMemberDialog open={true} onOpenChange={vi.fn()} onInvited={vi.fn()} />)

    await user.click(screen.getByLabelText('Total'))

    expect(
      screen.getByText(
        'Pode visualizar, lançar despesas e criar categorias e orçamentos. Não pode gerenciar outros membros.',
      ),
    ).toBeInTheDocument()
  })

  it('enviar mostra o overlay de processamento e desabilita os botões', async () => {
    // Resolução controlada manualmente pelo teste (em vez de um delay
    // fixo): sob carga da suíte completa, um delay real de tempo fixo
    // pode não ser suficiente pra manter a janela de loading visível
    // até a asserção rodar (achado ao investigar flakiness da FEAT-32
    // — ver `frontend/docs/backlog.md`). Resolver só quando o teste
    // decidir elimina essa corrida por completo.
    const user = userEvent.setup()
    let resolveInvite: (() => void) | null = null
    server.use(
      http.post(MEMBERS_URL, () => {
        return new Promise<Response>((resolve) => {
          resolveInvite = () =>
            resolve(
              HttpResponse.json({
                id: 'mem-2',
                email: 'convidado@email.com',
                role: 'Lancar',
                status: 'ConvitePendente',
                createdAt: '2025-06-16T09:00:00Z',
              }),
            )
        })
      }),
    )

    render(<InviteMemberDialog open={true} onOpenChange={vi.fn()} onInvited={vi.fn()} />)
    await user.type(screen.getByLabelText('E-mail'), 'convidado@email.com')
    await user.click(screen.getByRole('button', { name: /enviar convite/i }))

    await waitFor(() => expect(resolveInvite).not.toBeNull())
    expect(screen.getByText('Enviando convite')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /cancelar/i })).toBeDisabled()
    expect(screen.getByRole('button', { name: /enviar convite/i })).toBeDisabled()

    // Resolve e drena antes do fim do teste — evita "setState após
    // desmontar" quando o RTL desmonta automaticamente no cleanup.
    await act(async () => {
      resolveInvite?.()
    })
  })

  it('sucesso chama onInvited e fecha o popup', async () => {
    const user = userEvent.setup()
    const invited = {
      id: 'mem-2',
      email: 'convidado@email.com',
      role: 'Lancar',
      status: 'ConvitePendente',
      createdAt: '2025-06-16T09:00:00Z',
    }
    server.use(http.post(MEMBERS_URL, () => HttpResponse.json(invited)))
    const onInvited = vi.fn()

    render(<InviteMemberDialog open={true} onOpenChange={vi.fn()} onInvited={onInvited} />)
    await user.type(screen.getByLabelText('E-mail'), 'convidado@email.com')
    await user.click(screen.getByRole('button', { name: /enviar convite/i }))

    await waitFor(() => expect(onInvited).toHaveBeenCalledWith(invited))
  })

  it('e-mail já membro (409) mostra erro inline sem fechar o popup', async () => {
    const user = userEvent.setup()
    server.use(http.post(MEMBERS_URL, () => new HttpResponse(null, { status: 409 })))
    const onInvited = vi.fn()

    render(<InviteMemberDialog open={true} onOpenChange={vi.fn()} onInvited={onInvited} />)
    await user.type(screen.getByLabelText('E-mail'), 'convidado@email.com')
    await user.click(screen.getByRole('button', { name: /enviar convite/i }))

    expect(await screen.findByText('Este e-mail já é membro desta conta.')).toBeInTheDocument()
    expect(screen.getByText('Convidar pessoa')).toBeInTheDocument()
    expect(onInvited).not.toHaveBeenCalled()
  })

  it('cancelar chama onOpenChange(false)', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(<InviteMemberDialog open={true} onOpenChange={onOpenChange} onInvited={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
