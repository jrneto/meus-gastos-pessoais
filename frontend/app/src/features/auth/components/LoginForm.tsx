import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { useNavigate } from 'react-router-dom'
import { useLogin } from '../hooks/useLogin'
import { loginSchema, type LoginCredentials } from '../schemas/loginSchema'
import { signupSchema, type SignupFormData } from '../schemas/signupSchema'

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

      {isSignupMode ? <SignupForm /> : <LoginModeForm />}
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
          {error.message}
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

function SignupForm() {
  const navigate = useNavigate()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<SignupFormData>({ resolver: zodResolver(signupSchema) })

  // Não há endpoint de cadastro no backend hoje: este submit nunca chama
  // API — apenas navega para a página fake que sinaliza que o cadastro
  // ainda não está disponível (ver spec.md, "Fora do escopo").
  function onSubmit() {
    navigate('/cadastro-em-breve')
  }

  return (
    <form className="flex w-full max-w-sm flex-col gap-4" noValidate onSubmit={handleSubmit(onSubmit)}>
      <label className="field">
        <span>Nome</span>
        <input className="input" id="name" type="text" autoComplete="name" {...register('name')} />
      </label>
      {errors.name && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.name.message}
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

      <button type="submit" className="btn btn-primary btn-block">
        Criar conta
      </button>
    </form>
  )
}
