import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { server } from '@/test/msw/server'
import { ConfirmationForm } from './ConfirmationForm'

const CONFIRM_URL = 'http://localhost:5049/auth/confirm'
const RESEND_URL = 'http://localhost:5049/auth/resend-confirmation'

interface RenderOverrides {
  email?: string
  autoResendOnEnter?: boolean
  onConfirmed?: (email: string) => void
  onBack?: () => void
}

function renderForm(overrides: RenderOverrides = {}) {
  const props = {
    email: 'fulano@email.com',
    autoResendOnEnter: false,
    onConfirmed: vi.fn(),
    onBack: vi.fn(),
    ...overrides,
  }
  render(<ConfirmationForm {...props} />)
  return props
}

async function fillCode(user: ReturnType<typeof userEvent.setup>, code: string) {
  for (const [index, digit] of code.split('').entries()) {
    await user.type(screen.getByLabelText(`Dígito ${index + 1} do código`), digit)
  }
}

describe('ConfirmationForm', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('avança o foco automaticamente ao digitar e volta com Backspace em campo vazio', async () => {
    const user = userEvent.setup()
    renderForm()

    const d1 = screen.getByLabelText('Dígito 1 do código')
    const d2 = screen.getByLabelText('Dígito 2 do código')

    await user.click(d1)
    await user.keyboard('1')
    expect(d2).toHaveFocus()

    await user.keyboard('{Backspace}')
    expect(d1).toHaveFocus()
  })

  it('bloqueia submit com código incompleto, sem chamar a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(CONFIRM_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    renderForm()

    await user.type(screen.getByLabelText('Dígito 1 do código'), '1')
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    expect(await screen.findByText('Digite os 6 dígitos do código.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('código correto chama confirm e dispara onConfirmed', async () => {
    const user = userEvent.setup()
    server.use(http.post(CONFIRM_URL, () => new HttpResponse(null, { status: 200 })))
    const { onConfirmed } = renderForm()

    await fillCode(user, '123456')
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    await waitFor(() => expect(onConfirmed).toHaveBeenCalledWith('fulano@email.com'))
  })

  it('código incorreto mostra erro inline sem limpar os dígitos preenchidos', async () => {
    const user = userEvent.setup()
    server.use(
      http.post(CONFIRM_URL, () =>
        HttpResponse.json(
          { status: 400, title: '...', detail: '...', type: 'https://gastosapp.dev/errors/invalid-confirmation-code' },
          { status: 400 },
        ),
      ),
    )
    renderForm()

    await fillCode(user, '123456')
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    expect(
      await screen.findByText('Código inválido ou expirado. Confira o email ou solicite um novo código.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Dígito 1 do código')).toHaveValue('1')
    expect(screen.getByLabelText('Dígito 6 do código')).toHaveValue('6')
  })

  it('contador chegando a zero desabilita os inputs e mostra "Reenviar e-mail"', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    renderForm()

    act(() => {
      vi.advanceTimersByTime(60_000)
    })

    expect(await screen.findByRole('button', { name: 'Reenviar e-mail' })).toBeInTheDocument()
    expect(screen.getByLabelText('Dígito 1 do código')).toBeDisabled()
  })

  it('reenviar código limpa e reabilita os campos e reinicia o contador em 60s', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true })
    server.use(http.post(RESEND_URL, () => new HttpResponse(null, { status: 200 })))
    renderForm()

    act(() => {
      vi.advanceTimersByTime(60_000)
    })
    const resendButton = await screen.findByRole('button', { name: 'Reenviar e-mail' })

    fireEvent.click(resendButton)

    await waitFor(() => expect(screen.getByLabelText('Dígito 1 do código')).not.toBeDisabled())
    expect(screen.getByLabelText('Dígito 1 do código')).toHaveValue('')
    expect(screen.getByText('1:00')).toBeInTheDocument()
  })

  it('autoResendOnEnter dispara resendConfirmation no mount, sem interação do usuário', async () => {
    let resendCalled = false
    server.use(
      http.post(RESEND_URL, () => {
        resendCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    renderForm({ autoResendOnEnter: true })

    await waitFor(() => expect(resendCalled).toBe(true))
  })

  it('voltar chama onBack sem nenhuma chamada de API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(CONFIRM_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
      http.post(RESEND_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    const { onBack } = renderForm()

    await user.click(screen.getByRole('button', { name: '← Voltar' }))

    expect(onBack).toHaveBeenCalled()
    expect(apiCalled).toBe(false)
  })

  it('erro de rede em confirm mostra a mensagem de NetworkError', async () => {
    const user = userEvent.setup()
    server.use(http.post(CONFIRM_URL, () => HttpResponse.error()))
    renderForm()

    await fillCode(user, '123456')
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    expect(await screen.findByText('Não foi possível conectar à API. Verifique sua conexão.')).toBeInTheDocument()
  })

  it('erro de rede em resendConfirmation (via autoResendOnEnter) mostra a mensagem de NetworkError', async () => {
    server.use(http.post(RESEND_URL, () => HttpResponse.error()))
    renderForm({ autoResendOnEnter: true })

    expect(await screen.findByText('Não foi possível conectar à API. Verifique sua conexão.')).toBeInTheDocument()
  })
})
