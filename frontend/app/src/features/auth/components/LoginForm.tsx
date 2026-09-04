import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { AccountNotConfirmedError } from '../errors/authErrors'
import { useLogin } from '../hooks/useLogin'
import { useRegister } from '../hooks/useRegister'
import { loginSchema, type LoginCredentials } from '../schemas/loginSchema'
import { registerSchema, type RegisterFormData } from '../schemas/registerSchema'
import { extractDigits, maskCpf } from '../utils/cpf'
import { maskPhone } from '../utils/phoneMask'
import { ConfirmationForm } from './ConfirmationForm'

type Screen = 'login' | 'signup' | 'confirmation'

export function LoginForm() {
  const [screen, setScreen] = useState<Screen>('login')
  const [confirmationEmail, setConfirmationEmail] = useState('')
  const [autoResendOnEnter, setAutoResendOnEnter] = useState(false)
  const [justConfirmedEmail, setJustConfirmedEmail] = useState<string | null>(null)

  function goToConfirmation(email: string, autoResend: boolean) {
    setConfirmationEmail(email)
    setAutoResendOnEnter(autoResend)
    setScreen('confirmation')
  }

  // A tela de confirmação ocupa o card inteiro, sem o seletor de modo
  // "Entrar"/"Criar conta" (FEAT-31).
  if (screen === 'confirmation') {
    return (
      <ConfirmationForm
        email={confirmationEmail}
        autoResendOnEnter={autoResendOnEnter}
        onConfirmed={(email) => {
          setJustConfirmedEmail(email)
          setScreen('login')
        }}
        onBack={() => setScreen('login')}
      />
    )
  }

  // O escopo `.ds-modernist` (tokens + reset) é aplicado uma vez no
  // wrapper de `LoginPage` — este componente só depende dele estar
  // presente em algum ancestral.
  return (
    <div>
      <div className="seg" style={{ alignSelf: 'flex-start', marginBottom: 'var(--space-4)' }}>
        <label className="seg-opt">
          <input type="radio" name="authmode" checked={screen === 'login'} onChange={() => setScreen('login')} />
          Entrar
        </label>
        <label className="seg-opt">
          <input type="radio" name="authmode" checked={screen === 'signup'} onChange={() => setScreen('signup')} />
          Criar conta
        </label>
      </div>

      {screen === 'signup' ? (
        <SignupForm onRegistered={(email) => goToConfirmation(email, false)} />
      ) : (
        <LoginModeForm
          justConfirmedEmail={justConfirmedEmail}
          onNeedsConfirmation={(email) => goToConfirmation(email, true)}
        />
      )}
    </div>
  )
}

function LoginModeForm({
  justConfirmedEmail,
  onNeedsConfirmation,
}: {
  justConfirmedEmail: string | null
  onNeedsConfirmation: (email: string) => void
}) {
  const { login, isLoading, error } = useLogin()
  const {
    register,
    handleSubmit,
    watch,
    formState: { errors },
  } = useForm<LoginCredentials>({
    resolver: zodResolver(loginSchema),
    defaultValues: { email: justConfirmedEmail ?? '' },
  })

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={handleSubmit((data) => login(data))}
    >
      {justConfirmedEmail && !error && (
        <div
          role="status"
          style={{
            border: '2px solid var(--color-accent)',
            background: 'var(--color-accent-100)',
            padding: '12px 14px',
            display: 'flex',
            gap: '10px',
            alignItems: 'flex-start',
          }}
        >
          <svg width="18" height="18" viewBox="0 0 24 24" fill="none" style={{ flex: 'none', marginTop: '1px' }}>
            <polyline
              points="20 6 9 17 4 12"
              stroke="var(--color-accent-700)"
              strokeWidth="2.5"
              fill="none"
              strokeLinecap="round"
              strokeLinejoin="round"
            />
          </svg>
          <span style={{ fontSize: '12.5px', lineHeight: 1.45, color: 'var(--color-accent-700)' }}>
            Email confirmado. Sua conta está ativa — entre com seus dados.
          </span>
        </div>
      )}

      {error && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
          <p style={{ color: 'var(--color-accent-700)', fontSize: '13px' }} role="alert">
            {error instanceof AccountNotConfirmedError ? error.message : 'Email ou senha inválidos.'}
          </p>
          {error instanceof AccountNotConfirmedError && (
            <button
              type="button"
              className="btn btn-ghost"
              style={{ alignSelf: 'flex-start', padding: 0, fontSize: '12.5px' }}
              onClick={() => onNeedsConfirmation(watch('email'))}
            >
              Confirmar cadastro
            </button>
          )}
        </div>
      )}

      <label className="field">
        <span>Email</span>
        <input
          className="input"
          id="email"
          type="email"
          autoComplete="email"
          aria-invalid={!!errors.email}
          {...register('email')}
        />
      </label>
      {errors.email && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.email.message}
        </p>
      )}

      <label className="field">
        <span>Senha</span>
        <input
          className="input"
          id="password"
          type="password"
          autoComplete="current-password"
          aria-invalid={!!errors.password}
          {...register('password')}
        />
      </label>
      {errors.password && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.password.message}
        </p>
      )}

      <button type="submit" className="btn btn-primary btn-block" disabled={isLoading}>
        {isLoading ? 'Entrando...' : 'Entrar'}
      </button>
    </form>
  )
}

function SignupForm({ onRegistered }: { onRegistered: (email: string) => void }) {
  const { register: doRegister, isLoading, error, success } = useRegister()
  const {
    register,
    handleSubmit,
    setValue,
    watch,
    formState: { errors },
  } = useForm<RegisterFormData>({ resolver: zodResolver(registerSchema) })

  const phoneDigits = watch('phoneDigits') ?? ''
  const cpfDigits = watch('cpfDigits') ?? ''
  // Guarda o email submetido (não o `watch()` no momento do sucesso,
  // que poderia já ter mudado) pra entregar pro `onRegistered`.
  const submittedEmailRef = useRef('')

  // Cadastro bem-sucedido navega direto pra tela de confirmação (FEAT-31)
  // — não existe mais uma tela intermediária de "aguarde aprovação".
  useEffect(() => {
    if (success) {
      onRegistered(submittedEmailRef.current)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [success])

  async function onSubmit(data: RegisterFormData) {
    submittedEmailRef.current = data.email
    await doRegister({
      email: data.email,
      password: data.password,
      name: data.name,
      phoneNumber: data.phoneDigits,
      cpf: data.cpfDigits,
    })
  }

  return (
    <form className="flex w-full max-w-sm flex-col gap-4" noValidate onSubmit={handleSubmit(onSubmit)}>
      {error && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '13px' }} role="alert">
          {error.message}
        </p>
      )}

      <label className="field">
        <span>Nome</span>
        <input className="input" id="name" type="text" autoComplete="name" {...register('name')} />
      </label>
      {errors.name && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.name.message}
        </p>
      )}

      <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: 'var(--space-4)' }}>
        <label className="field">
          <span>CPF</span>
          <input
            className="input"
            id="cpf"
            type="text"
            inputMode="numeric"
            autoComplete="off"
            value={maskCpf(cpfDigits)}
            onChange={(e) => setValue('cpfDigits', extractDigits(e.target.value, 11), { shouldValidate: true })}
          />
        </label>

        <label className="field">
          <span>Telefone</span>
          <input
            className="input"
            id="phone"
            type="text"
            inputMode="numeric"
            autoComplete="tel"
            value={maskPhone(phoneDigits)}
            onChange={(e) => setValue('phoneDigits', extractDigits(e.target.value, 11), { shouldValidate: true })}
          />
        </label>
      </div>
      {errors.cpfDigits && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.cpfDigits.message}
        </p>
      )}
      {errors.phoneDigits && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.phoneDigits.message}
        </p>
      )}

      <label className="field">
        <span>Email</span>
        <input className="input" id="signup-email" type="email" autoComplete="email" {...register('email')} />
      </label>
      {errors.email && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.email.message}
        </p>
      )}

      <label className="field">
        <span>Senha</span>
        <input
          className="input"
          id="signup-password"
          type="password"
          autoComplete="new-password"
          {...register('password')}
        />
      </label>
      {errors.password && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.password.message}
        </p>
      )}

      <button type="submit" className="btn btn-primary btn-block" disabled={isLoading}>
        {isLoading ? 'Criando conta...' : 'Criar conta'}
      </button>
    </form>
  )
}
