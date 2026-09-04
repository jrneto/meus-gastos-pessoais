import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { server } from '@/test/msw/server'
import { ForgotPasswordFlow } from './ForgotPasswordFlow'

const FORGOT_PASSWORD_URL = 'http://localhost:5049/auth/forgot-password'
const RESET_PASSWORD_URL = 'http://localhost:5049/auth/reset-password'

// Um pequeno delay artificial nos mocks é necessário aqui: `authApi.forgotPassword`/
// `authApi.resetPassword` são consumidos via um `useEffect` que detecta sucesso
// pela transição `isLoading: true → false` (mesmo idioma de `ConfirmationForm`,
// FEAT-31). Sem nenhum delay, o MSW resolve rápido demais e o React 18 agrupa os
// dois `setState` (true, depois false) no mesmo lote — como o valor final é igual
// ao inicial, o React nunca chega a comitar o estado intermediário `true`, e o
// efeito nunca observa a transição. Em produção isso não acontece (rede real
// sempre cruza uma fronteira de macrotask antes da resposta chegar); aqui é só
// pra simular esse timing mínimo — não testa nenhum comportamento de latência
// em si (ver `frontend/docs/backlog.md`, débito técnico registrado durante a
// implementação desta feature).
const TICK = 10

function forgotPasswordOk() {
  return http.post(FORGOT_PASSWORD_URL, async () => {
    await delay(TICK)
    return new HttpResponse(null, { status: 200 })
  })
}

function resetPasswordOk() {
  return http.post(RESET_PASSWORD_URL, async () => {
    await delay(TICK)
    return new HttpResponse(null, { status: 200 })
  })
}

function resetPasswordProblem(status: number, type: string) {
  return http.post(RESET_PASSWORD_URL, async () => {
    await delay(TICK)
    return HttpResponse.json({ status, title: '...', detail: '...', type: `https://gastosapp.dev/errors/${type}` }, { status })
  })
}

interface RenderOverrides {
  onDone?: (email: string) => void
  onBack?: () => void
}

function renderFlow(overrides: RenderOverrides = {}) {
  const props = { onDone: vi.fn(), onBack: vi.fn(), ...overrides }
  render(<ForgotPasswordFlow {...props} />)
  return props
}

async function goToEmailStep(user: ReturnType<typeof userEvent.setup>, email = 'fulano@email.com') {
  await user.type(screen.getByLabelText('E-mail'), email)
  await user.click(screen.getByRole('button', { name: 'Enviar código' }))
  await screen.findByLabelText('Dígito 1 do código')
}

async function fillCode(user: ReturnType<typeof userEvent.setup>, code: string) {
  for (const [index, digit] of code.split('').entries()) {
    await user.type(screen.getByLabelText(`Dígito ${index + 1} do código`), digit)
  }
}

// Drena a fila de microtasks (encadeamento de promises da validação
// assíncrona do RHF + `fetch`), sem depender de nenhum timer — usado só
// no teste com fake timers abaixo, onde `waitFor` (baseado em
// `setTimeout`) não dispara sozinho.
async function flushMicrotasks(times = 10) {
  for (let i = 0; i < times; i++) {
    // eslint-disable-next-line no-await-in-loop
    await act(async () => {
      await Promise.resolve()
    })
  }
}

async function goToNewPasswordStep(user: ReturnType<typeof userEvent.setup>) {
  await goToEmailStep(user)
  await fillCode(user, '123456')
  await user.click(screen.getByRole('button', { name: 'Confirmar código' }))
  await screen.findByLabelText('Nova senha')
}

describe('ForgotPasswordFlow', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('Passo 1/3: email de conta existente avança pro Passo 2/3', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk())
    renderFlow()

    await goToEmailStep(user)

    expect(screen.getByText(/Enviamos um código de 6 dígitos para/)).toBeInTheDocument()
    expect(screen.getByText('fulano@email.com')).toBeInTheDocument()
  })

  it('Passo 1/3: email inexistente avança igual, sem diferença observável (US2)', async () => {
    const user = userEvent.setup()
    // Backend sempre retorna 200, mesmo pra email inexistente (FEAT-36).
    server.use(forgotPasswordOk())
    renderFlow()

    await goToEmailStep(user, 'nao-existe@email.com')

    expect(screen.getByLabelText('Dígito 1 do código')).toBeInTheDocument()
  })

  it('"← Voltar ao login" no Passo 1/3 chama onBack sem chamar API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(FORGOT_PASSWORD_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    const { onBack } = renderFlow()

    await user.click(screen.getByRole('button', { name: '← Voltar ao login' }))

    expect(onBack).toHaveBeenCalled()
    expect(apiCalled).toBe(false)
  })

  it('Passo 2/3: código completo avança pro Passo 3/3 sem chamar nenhuma API', async () => {
    const user = userEvent.setup()
    let resetCalled = false
    server.use(
      forgotPasswordOk(),
      http.post(RESET_PASSWORD_URL, () => {
        resetCalled = true
        return new HttpResponse(null, { status: 200 })
      }),
    )
    renderFlow()

    await goToEmailStep(user)
    await fillCode(user, '123456')
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    expect(await screen.findByLabelText('Nova senha')).toBeInTheDocument()
    expect(resetCalled).toBe(false)
  })

  it('Passo 2/3: código incompleto bloqueia o submit no client', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk())
    renderFlow()

    await goToEmailStep(user)
    await user.type(screen.getByLabelText('Dígito 1 do código'), '1')
    await user.click(screen.getByRole('button', { name: 'Confirmar código' }))

    expect(await screen.findByText('Digite os 6 dígitos do código.')).toBeInTheDocument()
  })

  it('Passo 2/3: contador chegando a zero desabilita os campos e reenvio chama forgot-password de novo', async () => {
    // Fake timers desde o início — o `setInterval` do cooldown
    // (`useResendCooldown`) precisa nascer sob o relógio falso pra
    // `advanceTimersByTime` conseguir adiantá-lo depois. A resposta de
    // `forgot-password`, porém, é resolvida manualmente (sem `delay()`
    // do msw, que usa `setTimeout` real e não é interceptado pelo
    // relógio falso — confirmado empiricamente) via uma Promise
    // controlada por este teste: resolver numa chamada de `act()`
    // separada do clique garante que o React comita o estado
    // intermediário `isLoading: true` antes de `isLoading` voltar a
    // `false`, sem depender de nenhum timer (real ou falso) pra isso —
    // só da fila de microtasks, imune ao relógio falso.
    vi.useFakeTimers()
    let forgotPasswordCalls = 0
    let resolveForgotPassword: (() => void) | null = null
    server.use(
      http.post(FORGOT_PASSWORD_URL, () => {
        forgotPasswordCalls += 1
        return new Promise<HttpResponse<null>>((resolve) => {
          resolveForgotPassword = () => resolve(new HttpResponse(null, { status: 200 }))
        })
      }),
    )
    renderFlow()

    fireEvent.change(screen.getByLabelText('E-mail'), { target: { value: 'fulano@email.com' } })
    fireEvent.click(screen.getByRole('button', { name: 'Enviar código' }))
    await flushMicrotasks()
    expect(resolveForgotPassword).not.toBeNull()
    await act(async () => {
      resolveForgotPassword?.()
    })
    await flushMicrotasks()
    expect(screen.getByLabelText('Dígito 1 do código')).toBeInTheDocument()
    expect(forgotPasswordCalls).toBe(1)

    act(() => {
      vi.advanceTimersByTime(60_000)
    })
    const resendButton = screen.getByRole('button', { name: 'Reenviar e-mail' })
    expect(screen.getByLabelText('Dígito 1 do código')).toBeDisabled()

    resolveForgotPassword = null
    fireEvent.click(resendButton)
    await flushMicrotasks()
    expect(resolveForgotPassword).not.toBeNull()
    await act(async () => {
      resolveForgotPassword?.()
    })
    await flushMicrotasks()

    expect(forgotPasswordCalls).toBe(2)
    expect(screen.getByLabelText('Dígito 1 do código')).not.toBeDisabled()
    expect(screen.getByText('1:00')).toBeInTheDocument()
  })

  it('"← Voltar" do Passo 2/3 volta ao Passo 1/3 com o email preservado, sem chamar API', async () => {
    const user = userEvent.setup()
    let apiCalls = 0
    server.use(
      http.post(FORGOT_PASSWORD_URL, async () => {
        apiCalls += 1
        await delay(TICK)
        return new HttpResponse(null, { status: 200 })
      }),
    )
    renderFlow()

    await goToEmailStep(user)
    expect(apiCalls).toBe(1)

    await user.click(screen.getByRole('button', { name: '← Voltar' }))

    expect(screen.getByLabelText('E-mail')).toHaveValue('fulano@email.com')
    expect(apiCalls).toBe(1)
  })

  it('Passo 3/3: botão "Mostrar/Ocultar" da nova senha também alterna a confirmação, sem botão próprio nela', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk())
    renderFlow()

    await goToNewPasswordStep(user)

    const newPasswordInput = screen.getByLabelText('Nova senha')
    const confirmInput = screen.getByLabelText('Confirmar nova senha')
    expect(newPasswordInput).toHaveAttribute('type', 'password')
    expect(confirmInput).toHaveAttribute('type', 'password')
    expect(screen.getAllByRole('button', { name: 'Mostrar' })).toHaveLength(1)

    await user.click(screen.getByRole('button', { name: 'Mostrar' }))

    expect(newPasswordInput).toHaveAttribute('type', 'text')
    expect(confirmInput).toHaveAttribute('type', 'text')
  })

  it('Passo 3/3: senhas diferentes bloqueiam o submit no client', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk())
    renderFlow()

    await goToNewPasswordStep(user)
    await user.type(screen.getByLabelText('Nova senha'), 'Senha123@')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'OutraSenha1@')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))

    expect(await screen.findByText('As senhas não coincidem.')).toBeInTheDocument()
  })

  it('Passo 3/3: sucesso chama onDone com o email', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk(), resetPasswordOk())
    const { onDone } = renderFlow()

    await goToNewPasswordStep(user)
    await user.type(screen.getByLabelText('Nova senha'), 'Senha123@')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'Senha123@')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))

    await waitFor(() => expect(onDone).toHaveBeenCalledWith('fulano@email.com'))
  })

  it('Passo 3/3: erro de código inválido mostra link de volta, que retorna ao Passo 2/3', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk(), resetPasswordProblem(400, 'invalid-reset-code'))
    renderFlow()

    await goToNewPasswordStep(user)
    await user.type(screen.getByLabelText('Nova senha'), 'Senha123@')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'Senha123@')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))

    expect(await screen.findByText('Código inválido ou expirado.')).toBeInTheDocument()
    await user.click(screen.getByRole('button', { name: 'Voltar e conferir o código' }))

    expect(screen.getByLabelText('Dígito 1 do código')).toBeInTheDocument()
  })

  it('Passo 3/3: senha fora da política mantém o usuário no passo, mostrando o erro', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk(), resetPasswordProblem(400, 'bad-request'))
    renderFlow()

    await goToNewPasswordStep(user)
    await user.type(screen.getByLabelText('Nova senha'), 'Senha123@')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'Senha123@')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))

    expect(
      await screen.findByText('A senha deve ter no mínimo 8 caracteres, com letra maiúscula, minúscula, número e símbolo.'),
    ).toBeInTheDocument()
    expect(screen.getByLabelText('Nova senha')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Voltar e conferir o código' })).not.toBeInTheDocument()
  })

  it('erro de rede no Passo 1/3 (forgot-password) mostra a mensagem de rede', async () => {
    const user = userEvent.setup()
    server.use(http.post(FORGOT_PASSWORD_URL, () => HttpResponse.error()))
    renderFlow()

    await user.type(screen.getByLabelText('E-mail'), 'fulano@email.com')
    await user.click(screen.getByRole('button', { name: 'Enviar código' }))

    expect(await screen.findByText('Não foi possível conectar à API. Verifique sua conexão.')).toBeInTheDocument()
  })

  it('erro de rede no Passo 3/3 (reset-password) mostra a mensagem de rede', async () => {
    const user = userEvent.setup()
    server.use(forgotPasswordOk(), http.post(RESET_PASSWORD_URL, () => HttpResponse.error()))
    renderFlow()

    await goToNewPasswordStep(user)
    await user.type(screen.getByLabelText('Nova senha'), 'Senha123@')
    await user.type(screen.getByLabelText('Confirmar nova senha'), 'Senha123@')
    await user.click(screen.getByRole('button', { name: 'Salvar nova senha' }))

    expect(await screen.findByText('Não foi possível conectar à API. Verifique sua conexão.')).toBeInTheDocument()
  })
})
