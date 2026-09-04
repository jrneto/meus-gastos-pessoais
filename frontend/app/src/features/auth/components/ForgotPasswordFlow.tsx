import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { InvalidResetCodeError } from '../errors/authErrors'
import { useForgotPassword } from '../hooks/useForgotPassword'
import { useResendCooldown } from '../hooks/useResendCooldown'
import { useResetPassword } from '../hooks/useResetPassword'
import { confirmationCodeSchema } from '../schemas/confirmationSchema'
import { forgotPasswordEmailSchema, newPasswordSchema, type ForgotPasswordEmailData, type NewPasswordFormData } from '../schemas/forgotPasswordSchema'
import { OtpDigitsInput } from './OtpDigitsInput'
import { PasswordField } from './PasswordField'

const DIGIT_COUNT = 6
const COOLDOWN_SECONDS = 60

type Step = 'email' | 'code' | 'new-password'

interface ForgotPasswordFlowProps {
  onDone: (email: string) => void
  onBack: () => void
}

// Orquestra os 3 passos do fluxo "Esqueci minha senha" (FEAT-32). Cada
// passo é uma função interna (mesmo padrão de `LoginModeForm`/
// `SignupForm` dentro de `LoginForm.tsx`) — nenhuma é reaproveitada fora
// deste fluxo.
export function ForgotPasswordFlow({ onDone, onBack }: ForgotPasswordFlowProps) {
  const [step, setStep] = useState<Step>('email')
  const [email, setEmail] = useState('')
  const [code, setCode] = useState('')

  if (step === 'email') {
    return (
      <ForgotPasswordEmailStep
        defaultEmail={email}
        onSent={(sentEmail) => {
          setEmail(sentEmail)
          setStep('code')
        }}
        onBack={onBack}
      />
    )
  }

  if (step === 'code') {
    return (
      <ForgotPasswordCodeStep
        email={email}
        onConfirmed={(confirmedCode) => {
          setCode(confirmedCode)
          setStep('new-password')
        }}
        onBack={() => setStep('email')}
      />
    )
  }

  return (
    <NewPasswordStep
      email={email}
      code={code}
      onSuccess={() => onDone(email)}
      onBackToCode={() => setStep('code')}
    />
  )
}

function ForgotPasswordEmailStep({
  defaultEmail,
  onSent,
  onBack,
}: {
  defaultEmail: string
  onSent: (email: string) => void
  onBack: () => void
}) {
  const { forgotPassword, isLoading, error } = useForgotPassword()
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ForgotPasswordEmailData>({
    resolver: zodResolver(forgotPasswordEmailSchema),
    // "← Voltar" do Passo 2/3 remonta este passo (spec.md, US6) — o email
    // já digitado precisa continuar visível, não voltar em branco.
    defaultValues: { email: defaultEmail },
  })
  // Guarda o email submetido (não o `watch()` no momento do sucesso, que
  // poderia já ter mudado) — mesmo padrão de `SignupForm` em `LoginForm.tsx`.
  const submittedEmailRef = useRef('')
  // `useForgotPassword` não expõe `success` (mesmo formato de
  // `useResendConfirmation`, que também não expõe) — detecta o fim de
  // uma submissão sem erro pela transição `isLoading: true → false`,
  // mesmo idioma já usado no reenvio de `ForgotPasswordCodeStep` abaixo.
  const wasSubmittingRef = useRef(false)

  useEffect(() => {
    if (wasSubmittingRef.current && !isLoading && !error) {
      onSent(submittedEmailRef.current)
    }
    wasSubmittingRef.current = isLoading
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isLoading, error])

  async function onSubmit(data: ForgotPasswordEmailData) {
    submittedEmailRef.current = data.email
    await forgotPassword({ email: data.email })
  }

  return (
    <form className="flex w-full max-w-sm flex-col gap-4" noValidate onSubmit={handleSubmit(onSubmit)}>
      <button
        type="button"
        onClick={onBack}
        className="btn btn-ghost"
        style={{ alignSelf: 'flex-start', paddingLeft: 0, fontSize: '12px' }}
      >
        ← Voltar ao login
      </button>

      <div>
        <p className="card-kicker" style={{ margin: '0 0 var(--space-2)' }}>PASSO 1 DE 3 · RECUPERAÇÃO</p>
        <h1 style={{ font: '800 24px var(--font-heading)', margin: '0 0 var(--space-2)' }}>Recuperar senha</h1>
        <p style={{ fontSize: '13.5px', lineHeight: 1.55, opacity: 0.7, margin: 0 }}>
          Informe o e-mail da sua conta. Enviaremos um código de 6 dígitos para confirmar que é você.
        </p>
      </div>

      {error && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '13px' }} role="alert">
          {error.message}
        </p>
      )}

      <label className="field">
        <span>E-mail</span>
        <input
          className="input"
          id="forgot-password-email"
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

      <button type="submit" className="btn btn-primary btn-block" disabled={isLoading}>
        {isLoading ? 'Enviando…' : 'Enviar código'}
      </button>
    </form>
  )
}

function ForgotPasswordCodeStep({
  email,
  onConfirmed,
  onBack,
}: {
  email: string
  onConfirmed: (code: string) => void
  onBack: () => void
}) {
  const [digits, setDigits] = useState<string[]>(() => Array(DIGIT_COUNT).fill(''))
  const [clientError, setClientError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const inputRefs = useRef<Array<HTMLInputElement | null>>([])
  const wasResendingRef = useRef(false)
  const wasExpiredRef = useRef(false)

  const { forgotPassword, isLoading: isResending, error: resendError } = useForgotPassword()
  const { secondsLeft, isExpired, restart } = useResendCooldown(COOLDOWN_SECONDS)

  // Detecta a transição isLoading=true → false do reenvio (mesmo padrão
  // de `ConfirmationForm`, FEAT-31): sucesso limpa os campos e reinicia
  // o cooldown; falha só exibe o erro.
  useEffect(() => {
    if (wasResendingRef.current && !isResending) {
      if (resendError) {
        setApiError(resendError.message)
      } else {
        setDigits(Array(DIGIT_COUNT).fill(''))
        setClientError(null)
        setApiError(null)
        restart()
      }
    }
    wasResendingRef.current = isResending
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isResending, resendError])

  useEffect(() => {
    if (wasExpiredRef.current && !isExpired) {
      inputRefs.current[0]?.focus()
    }
    wasExpiredRef.current = isExpired
  }, [isExpired])

  function handleDigitChange(index: number, value: string) {
    setDigits((current) => {
      const next = [...current]
      next[index] = value
      return next
    })
    setClientError(null)
    setApiError(null)
  }

  // Só validação local — não existe endpoint pra verificar o código
  // isoladamente (spec.md, decisão 3). `POST /auth/reset-password` só é
  // chamado no Passo 3/3, junto com a senha nova.
  function handleSubmit() {
    const code = digits.join('')
    const result = confirmationCodeSchema.safeParse(code)
    if (!result.success) {
      setClientError('Digite os 6 dígitos do código.')
      return
    }
    setClientError(null)
    onConfirmed(code)
  }

  const displayedError = clientError ?? apiError

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        handleSubmit()
      }}
    >
      <button
        type="button"
        onClick={onBack}
        className="btn btn-ghost"
        style={{ alignSelf: 'flex-start', paddingLeft: 0, fontSize: '12px' }}
      >
        ← Voltar
      </button>

      <div>
        <p className="card-kicker" style={{ margin: '0 0 var(--space-2)' }}>PASSO 2 DE 3 · RECUPERAÇÃO</p>
        <h1 style={{ font: '800 24px var(--font-heading)', margin: '0 0 var(--space-2)' }}>Verifique o código</h1>
        <p style={{ fontSize: '13.5px', lineHeight: 1.55, opacity: 0.7, margin: 0 }}>
          Enviamos um código de 6 dígitos para <strong>{email}</strong>.
        </p>
      </div>

      <OtpDigitsInput digits={digits} disabled={isExpired} inputRefs={inputRefs} onChange={handleDigitChange} />

      {displayedError && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12.5px' }} role="alert">
          {displayedError}
        </p>
      )}

      {isExpired ? (
        <div
          style={{
            borderTop: '2px solid var(--color-divider)',
            paddingTop: 'var(--space-3)',
            display: 'flex',
            flexDirection: 'column',
            gap: 'var(--space-3)',
          }}
        >
          <span style={{ fontSize: '12.5px', opacity: 0.7 }}>Não recebeu o código? Você pode solicitar um novo agora.</span>
          <button
            type="button"
            className="btn btn-primary btn-block"
            disabled={isResending}
            onClick={() => void forgotPassword({ email })}
          >
            {isResending ? 'Reenviando…' : 'Reenviar e-mail'}
          </button>
        </div>
      ) : (
        <>
          <div
            style={{
              borderTop: '2px solid var(--color-divider)',
              paddingTop: 'var(--space-3)',
              display: 'flex',
              alignItems: 'baseline',
              gap: 'var(--space-2)',
            }}
          >
            <span style={{ font: '800 20px var(--font-heading)', color: 'var(--color-accent)', fontVariantNumeric: 'tabular-nums' }}>
              {formatTimer(secondsLeft)}
            </span>
            <span style={{ fontSize: '12px', opacity: 0.6 }}>aguarde para poder reenviar o código</span>
          </div>
          <button type="submit" className="btn btn-primary btn-block">
            Confirmar código
          </button>
        </>
      )}
    </form>
  )
}

function NewPasswordStep({
  email,
  code,
  onSuccess,
  onBackToCode,
}: {
  email: string
  code: string
  onSuccess: () => void
  onBackToCode: () => void
}) {
  const { resetPassword, isLoading, error, success: isReset } = useResetPassword()
  const [showPassword, setShowPassword] = useState(false)
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<NewPasswordFormData>({ resolver: zodResolver(newPasswordSchema) })

  useEffect(() => {
    if (isReset) {
      onSuccess()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isReset])

  async function onSubmit(data: NewPasswordFormData) {
    await resetPassword({ email, code, newPassword: data.newPassword })
  }

  return (
    <form className="flex w-full max-w-sm flex-col gap-4" noValidate onSubmit={handleSubmit(onSubmit)}>
      <div>
        <p className="card-kicker" style={{ margin: '0 0 var(--space-2)' }}>PASSO 3 DE 3 · RECUPERAÇÃO</p>
        <h1 style={{ font: '800 24px var(--font-heading)', margin: '0 0 var(--space-2)' }}>Nova senha</h1>
        <p style={{ fontSize: '13.5px', lineHeight: 1.55, opacity: 0.7, margin: 0 }}>
          Escolha uma senha com pelo menos 8 caracteres, com letra maiúscula, minúscula, número e símbolo.
        </p>
      </div>

      {error && (
        <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)' }}>
          <p style={{ color: 'var(--color-accent-700)', fontSize: '13px' }} role="alert">
            {error.message}
          </p>
          {error instanceof InvalidResetCodeError && (
            <button
              type="button"
              className="btn btn-ghost"
              style={{ alignSelf: 'flex-start', padding: 0, fontSize: '12.5px' }}
              onClick={onBackToCode}
            >
              Voltar e conferir o código
            </button>
          )}
        </div>
      )}

      <PasswordField
        id="new-password"
        label="Nova senha"
        autoComplete="new-password"
        ariaInvalid={!!errors.newPassword}
        registration={register('newPassword')}
        isVisible={showPassword}
        onToggleVisibility={() => setShowPassword((visible) => !visible)}
      />
      {errors.newPassword && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.newPassword.message}
        </p>
      )}

      <PasswordField
        id="confirm-new-password"
        label="Confirmar nova senha"
        autoComplete="new-password"
        ariaInvalid={!!errors.confirmNewPassword}
        registration={register('confirmNewPassword')}
        isVisible={showPassword}
      />
      {errors.confirmNewPassword && (
        <p style={{ color: 'var(--color-accent-700)', fontSize: '12px' }} role="alert">
          {errors.confirmNewPassword.message}
        </p>
      )}

      <button type="submit" className="btn btn-primary btn-block" disabled={isLoading}>
        {isLoading ? 'Salvando…' : 'Salvar nova senha'}
      </button>
    </form>
  )
}

function formatTimer(seconds: number): string {
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return `${minutes}:${String(rest).padStart(2, '0')}`
}
