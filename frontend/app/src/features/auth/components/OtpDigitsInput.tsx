import type { KeyboardEvent, MutableRefObject } from 'react'

const DIGIT_COUNT = 6

interface OtpDigitsInputProps {
  digits: string[]
  disabled: boolean
  inputRefs: MutableRefObject<Array<HTMLInputElement | null>>
  onChange: (index: number, value: string) => void
}

// Grid puramente visual dos 6 dígitos do código OTP: avanço automático
// de foco ao digitar, volta de foco com Backspace em campo vazio. Não
// sabe nada sobre submit, cooldown ou qual API está por trás — extraído
// de `ConfirmationForm` (FEAT-31) pra ser reaproveitado pelo passo de
// código da recuperação de senha (FEAT-32) sem duplicar a parte mais
// delicada de acertar (gestão de foco). `inputRefs` é dono do
// componente pai (não um `ref` encaminhado) porque quem decide "focar o
// primeiro input ao reabilitar" é o pai — cada fluxo tem sua própria
// transição de `disabled`.
export function OtpDigitsInput({ digits, disabled, inputRefs, onChange }: OtpDigitsInputProps) {
  function handleKeyDown(index: number, event: KeyboardEvent<HTMLInputElement>) {
    if (event.key === 'Backspace' && !digits[index] && index > 0) {
      inputRefs.current[index - 1]?.focus()
    }
  }

  function handleChange(index: number, rawValue: string) {
    const value = rawValue.replace(/\D/g, '').slice(-1)
    onChange(index, value)
    if (value && index < DIGIT_COUNT - 1) {
      inputRefs.current[index + 1]?.focus()
    }
  }

  return (
    <div style={{ display: 'grid', gridTemplateColumns: `repeat(${DIGIT_COUNT}, 1fr)`, gap: 'var(--space-2)' }}>
      {digits.map((digit, index) => (
        <input
          // eslint-disable-next-line react/no-array-index-key
          key={index}
          ref={(el) => {
            inputRefs.current[index] = el
          }}
          className="input"
          type="text"
          inputMode="numeric"
          maxLength={1}
          aria-label={`Dígito ${index + 1} do código`}
          disabled={disabled}
          value={digit}
          onChange={(event) => handleChange(index, event.target.value)}
          onKeyDown={(event) => handleKeyDown(index, event)}
          style={{ textAlign: 'center', font: '700 20px var(--font-heading)', padding: 0, height: '52px' }}
        />
      ))}
    </div>
  )
}
