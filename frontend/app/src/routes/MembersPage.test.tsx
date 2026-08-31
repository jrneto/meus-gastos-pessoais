import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { MembersPage } from './MembersPage'

const MEMBERS_URL = 'http://localhost:5049/members'
const ME_URL = 'http://localhost:5049/auth/me'

const titular = {
  id: 'mem-1',
  email: 'titular@email.com',
  role: 'Titular',
  status: 'Ativo',
  createdAt: '2025-06-15T12:34:56Z',
}

const member = {
  id: 'mem-2',
  email: 'convidado@email.com',
  role: 'Leitura',
  status: 'ConvitePendente',
  createdAt: '2025-06-16T09:00:00Z',
}

function mockMembersAndMe(currentUserEmail: string, items: unknown[] = [titular, member]) {
  server.use(
    http.get(MEMBERS_URL, () => HttpResponse.json({ items })),
    http.get(ME_URL, () =>
      HttpResponse.json({ userId: 'user-1', email: currentUserEmail, name: 'Fulano da Silva' }),
    ),
  )
}

describe('MembersPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('Titular vê a lista completa com ações e botão de convidar', async () => {
    mockMembersAndMe('titular@email.com')

    render(<MembersPage />)

    expect(await screen.findByText('Você (titular)')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: /convidar pessoa/i })).toBeInTheDocument()
    // `others` (via localOthers) só chega um tick depois de `items`
    // carregar (sincronizado por useEffect, ver plan.md) — `findBy*`
    // aguarda esse tick extra, diferente da linha do Titular acima,
    // que já vem pronta na primeira renderização pós-loading.
    expect(await screen.findByLabelText('Leitura')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Remover membro' })).toBeInTheDocument()
  })

  it('não-Titular vê a lista completa sem ações e sem botão de convidar', async () => {
    mockMembersAndMe('convidado@email.com')

    render(<MembersPage />)

    expect(await screen.findByText('titular@email.com')).toBeInTheDocument()
    expect(await screen.findByText('convidado@email.com (você)')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /convidar pessoa/i })).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Leitura')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Remover membro' })).not.toBeInTheDocument()
  })

  it('convidar com sucesso mostra o toast e atualiza a lista sem novo GET /members', async () => {
    const user = userEvent.setup()
    let getMembersCount = 0
    server.use(
      http.get(MEMBERS_URL, () => {
        getMembersCount += 1
        return HttpResponse.json({ items: [titular] })
      }),
      http.get(ME_URL, () => HttpResponse.json({ userId: 'user-1', email: 'titular@email.com', name: 'Titular' })),
      http.post(MEMBERS_URL, () =>
        HttpResponse.json({
          id: 'mem-3',
          email: 'nova@email.com',
          role: 'Lancar',
          status: 'ConvitePendente',
          createdAt: '2025-06-17T09:00:00Z',
        }),
      ),
    )

    render(<MembersPage />)
    await screen.findByText('Você (titular)')
    expect(getMembersCount).toBe(1)

    await user.click(screen.getByRole('button', { name: /convidar pessoa/i }))
    await user.type(screen.getByLabelText('E-mail'), 'nova@email.com')
    await user.click(screen.getByRole('button', { name: /enviar convite/i }))

    expect(await screen.findByText('Convite enviado para nova@email.com.')).toBeInTheDocument()
    expect(screen.getByText('nova@email.com')).toBeInTheDocument()
    expect(getMembersCount).toBe(1)
  })

  it('Titular troca o papel de um membro', async () => {
    const user = userEvent.setup()
    mockMembersAndMe('titular@email.com')
    server.use(
      http.put(`${MEMBERS_URL}/mem-2`, () => HttpResponse.json({ ...member, role: 'Total' })),
    )

    render(<MembersPage />)
    await screen.findByLabelText('Leitura')

    await user.click(screen.getByLabelText('Total'))

    await waitFor(() => expect(screen.getByLabelText('Total')).toBeChecked())
  })

  it('Titular remove um membro com confirmação', async () => {
    const user = userEvent.setup()
    mockMembersAndMe('titular@email.com')
    server.use(http.delete(`${MEMBERS_URL}/mem-2`, () => new HttpResponse(null, { status: 204 })))

    render(<MembersPage />)
    await screen.findByText('convidado@email.com')

    await user.click(screen.getByRole('button', { name: 'Remover membro' }))
    expect(await screen.findByText('Remover membro')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: /^remover$/i }))

    await waitFor(() => expect(screen.queryByText('convidado@email.com')).not.toBeInTheDocument())
  })

  it('erro de sessão expirada limpa a sessão e exibe mensagem', async () => {
    server.use(
      http.get(MEMBERS_URL, () => new HttpResponse(null, { status: 401 })),
      http.get(ME_URL, () => HttpResponse.json({ userId: 'user-1', email: 'titular@email.com', name: 'Titular' })),
    )

    render(<MembersPage />)

    expect(await screen.findByText('Não foi possível carregar os membros')).toBeInTheDocument()
    expect(screen.getByText('Sua sessão expirou. Faça login novamente.')).toBeInTheDocument()
    expect(useAuthStore.getState().token).toBeNull()
  })
})
