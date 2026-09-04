import type { UseFormRegisterReturn } from 'react-hook-form'

interface PasswordFieldProps {
  id: string
  label: string
  autoComplete: string
  ariaInvalid?: boolean
  registration: UseFormRegisterReturn
  isVisible: boolean
  onToggleVisibility?: () => void
}

// Campo de senha com botão opcional "Mostrar/Ocultar" — toggle local de
// `type="password"`/`type="text"`, sem chamada de API nem novo estado de
// servidor (`frontend/design-system/README.md`, seção Autenticação).
// Extraído pra ser reaproveitado pelos 3 campos que o design system pede:
// login e cadastro (`LoginForm.tsx`) e nova senha (`ForgotPasswordFlow.tsx`)
// — mesmo padrão de extração de `OtpDigitsInput`.
// `onToggleVisibility` omitido renderiza sem botão próprio: é o caso do
// campo "Confirmar nova senha", que só espelha a visibilidade do campo
// "Nova senha" ao lado, sem seu próprio controle (mesmo comportamento do
// protótipo Modernist, `design-system/web/jrnexpenses-web.dc.html`).
export function PasswordField({
  id,
  label,
  autoComplete,
  ariaInvalid,
  registration,
  isVisible,
  onToggleVisibility,
}: PasswordFieldProps) {
  return (
    <label className="field">
      <span>{label}</span>
      <span style={{ position: 'relative', display: 'block' }}>
        <input
          className="input"
          id={id}
          type={isVisible ? 'text' : 'password'}
          autoComplete={autoComplete}
          aria-invalid={ariaInvalid}
          style={onToggleVisibility ? { width: '100%', boxSizing: 'border-box', paddingRight: '86px' } : undefined}
          {...registration}
        />
        {onToggleVisibility && (
          <button
            type="button"
            onClick={onToggleVisibility}
            className="btn btn-ghost"
            style={{
              position: 'absolute',
              right: 0,
              top: 0,
              height: '100%',
              padding: '0 12px',
              fontSize: '10.5px',
              letterSpacing: '.08em',
              textTransform: 'uppercase',
            }}
          >
            {isVisible ? 'Ocultar' : 'Mostrar'}
          </button>
        )}
      </span>
    </label>
  )
}
