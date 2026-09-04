import { useEffect, useRef, useState } from 'react'
import { useConfirmAccount } from '../hooks/useConfirmAccount'
import { useResendConfirmation } from '../hooks/useResendConfirmation'
import { useResendCooldown } from '../hooks/useResendCooldown'
import { confirmationCodeSchema } from '../schemas/confirmationSchema'
import { OtpDigitsInput } from './OtpDigitsInput'

const DIGIT_COUNT = 6
const COOLDOWN_SECONDS = 60

interface ConfirmationFormProps {
  email: string
  // true quando a tela foi aberta pelo CTA "Confirmar cadastro" do
  // login (401 user-not-confirmed) — dispara um reenvio automático ao
  // montar, já que nesse caminho não houve nenhum `register` recente
  // enviando um código novo (spec.md, US8).
  autoResendOnEnter: boolean
  onConfirmed: (email: string) => void
  onBack: () => void
}

function formatTimer(seconds: number): string {
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return `${minutes}:${String(rest).padStart(2, '0')}`
}

export function ConfirmationForm({ email, autoResendOnEnter, onConfirmed, onBack }: ConfirmationFormProps) {
  const [digits, setDigits] = useState<string[]>(() => Array(DIGIT_COUNT).fill(''))
  const [clientError, setClientError] = useState<string | null>(null)
  const [apiError, setApiError] = useState<string | null>(null)
  const inputRefs = useRef<Array<HTMLInputElement | null>>([])
  const wasResendingRef = useRef(false)
  const wasExpiredRef = useRef(false)

  const { confirm, isLoading: isConfirming, error: confirmError, success: isConfirmed } = useConfirmAccount()
  const { resend, isLoading: isResending, error: resendError } = useResendConfirmation()
  const { secondsLeft, isExpired, restart } = useResendCooldown(COOLDOWN_SECONDS)

  // Entrada pelo CTA do login: dispara o primeiro reenvio sem
  // interação do usuário, uma única vez.
  useEffect(() => {
    if (autoResendOnEnter) {
      void resend({ email })
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Reflete o erro de `confirm` (400 — código inválido ou expirado) no
  // slot de erro exibido, sem limpar os dígitos nem o cooldown.
  useEffect(() => {
    if (confirmError) {
      setApiError(confirmError.message)
    }
  }, [confirmError])

  // Detecta a transição isLoading=true → false do reenvio pra saber
  // quando ele terminou: sucesso limpa os campos e reinicia o
  // cooldown; falha só exibe o erro (useResendConfirmation nunca
  // relança, sempre resolve). Não foca o primeiro input aqui — nesse
  // exato instante os inputs ainda estão `disabled` no DOM committado
  // (isExpired só vira `false` depois do re-render disparado por
  // `restart()`), então `.focus()` seria um no-op silencioso. Quem
  // cuida do foco é o efeito de `isExpired` logo abaixo.
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

  // Foca o primeiro input quando os campos voltam a ficar habilitados
  // (transição true → false de `isExpired`, disparada pelo `restart()`
  // acima) — só então o DOM já reflete `disabled=false`.
  useEffect(() => {
    if (wasExpiredRef.current && !isExpired) {
      inputRefs.current[0]?.focus()
    }
    wasExpiredRef.current = isExpired
  }, [isExpired])

  useEffect(() => {
    if (isConfirmed) {
      onConfirmed(email)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isConfirmed])

  function handleDigitChange(index: number, value: string) {
    setDigits((current) => {
      const next = [...current]
      next[index] = value
      return next
    })
    setClientError(null)
    setApiError(null)
  }

  async function handleSubmit() {
    const code = digits.join('')
    const result = confirmationCodeSchema.safeParse(code)
    if (!result.success) {
      setClientError('Digite os 6 dígitos do código.')
      return
    }
    setClientError(null)
    await confirm({ email, code })
  }

  const displayedError = clientError ?? apiError

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        void handleSubmit()
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
        <h1 style={{ font: '800 24px var(--font-heading)', margin: '0 0 var(--space-2)' }}>Confirme seu email</h1>
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
          <button type="button" className="btn btn-primary btn-block" disabled={isResending} onClick={() => void resend({ email })}>
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
          <button type="submit" className="btn btn-primary btn-block" disabled={isConfirming}>
            {isConfirming ? 'Confirmando…' : 'Confirmar código'}
          </button>
        </>
      )}
    </form>
  )
}
