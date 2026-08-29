import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { AccountPendingApprovalError } from '../errors/authErrors'
import { useLogin } from '../hooks/useLogin'
import { useRegister } from '../hooks/useRegister'
import { loginSchema, type LoginCredentials } from '../schemas/loginSchema'
import { registerSchema, type RegisterFormData } from '../schemas/registerSchema'
import { extractDigits, maskCpf } from '../utils/cpf'
import { maskPhone } from '../utils/phoneMask'

type AuthMode = 'login' | 'signup'

export function LoginForm() {
  const [authMode, setAuthMode] = useState<AuthMode>('login')
  const isSignupMode = authMode === 'signup'

  // O escopo `.ds-modernist` (tokens + reset) é aplicado uma vez no
  // wrapper de `LoginPage` — este componente só depende dele estar
  // presente em algum ancestral.
  return (
    <div>
      <div className="seg" style={{ alignSelf: 'flex-start', marginBottom: 'var(--space-4)' }}>
        <label className="seg-opt">
          <input
            type="radio"
            name="authmode"
            checked={authMode === 'login'}
            onChange={() => setAuthMode('login')}
          />
          Entrar
        </label>
        <label className="seg-opt">
          <input
            type="radio"
            name="authmode"
            checked={authMode === 'signup'}
            onChange={() => setAuthMode('signup')}
          />
          Criar conta
        </label>
      </div>

      {isSignupMode ? <SignupForm onDone={() => setAuthMode('login')} /> : <LoginModeForm />}
    </div>
  )
}

function LoginModeForm() {
  const { login, isLoading, error } = useLogin()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<LoginCredentials>({ resolver: zodResolver(loginSchema) })

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={handleSubmit((data) => login(data))}
    >
      {error && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '13px' }} role="alert">
          {error instanceof AccountPendingApprovalError
            ? error.message
            : 'Email ou senha inválidos.'}
        </p>
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

function SignupForm({ onDone }: { onDone: () => void }) {
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

  if (success) {
    return (
      <div className="flex w-full max-w-sm flex-col gap-4">
        <p style={{ color: 'var(--color-success-700, #15803d)', fontSize: '13px' }} role="status">
          Conta criada! Aguarde a aprovação do administrador para poder entrar.
        </p>
        <button type="button" className="btn btn-primary btn-block" onClick={onDone}>
          Voltar para o login
        </button>
      </div>
    )
  }

  async function onSubmit(data: RegisterFormData) {
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
