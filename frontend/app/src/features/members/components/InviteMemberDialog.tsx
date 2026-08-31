import { useEffect, useState } from 'react'
import '@/styles/modernist/modernist.css'
import { ProcessingOverlay } from '@/components/ProcessingOverlay'
import type { MemberItem, MemberRole } from '../api/membersApi'
import { useInviteMember } from '../hooks/useInviteMember'
import { ROLE_DESCRIPTION, ROLE_LABEL } from '../utils/roleLabels'

interface InviteMemberDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onInvited: (member: MemberItem) => void
}

const ASSIGNABLE_ROLES: Exclude<MemberRole, 'Titular'>[] = ['Leitura', 'Lancar', 'Total']

// Popup "Convidar pessoa" (FEAT-28). Papel inicial "Lançar" (decisão 6
// da spec). Enquanto o `POST /members` está em andamento, mostra o
// `ProcessingOverlay` sobre o próprio conteúdo do dialog (que por isso
// ganha `position: relative` só aqui, diferente de outros dialogs sem
// overlay). Erro fica inline no popup — sem toast em erro, só em
// sucesso (ver `MembersPage`).
export function InviteMemberDialog({ open, onOpenChange, onInvited }: InviteMemberDialogProps) {
  const [email, setEmail] = useState('')
  const [role, setRole] = useState<Exclude<MemberRole, 'Titular'>>('Lancar')
  const { inviteMember, isLoading, error, success, data } = useInviteMember()

  useEffect(() => {
    if (success && data) {
      onInvited(data)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [success, data])

  useEffect(() => {
    if (!open) {
      setEmail('')
      setRole('Lancar')
    }
  }, [open])

  useEffect(() => {
    if (!open) return undefined

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape' && !isLoading) {
        onOpenChange(false)
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, isLoading, onOpenChange])

  if (!open) {
    return null
  }

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => !isLoading && onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="invite-member-title"
        style={{ position: 'relative' }}
        onClick={(event) => event.stopPropagation()}
      >
        {isLoading && <ProcessingOverlay label="Enviando convite" />}

        <div className="dialog-title" id="invite-member-title">
          Convidar pessoa
        </div>

        {error && (
          <div style={{ color: 'var(--color-accent-700)' }}>
            <div style={{ fontWeight: 700 }}>Não foi possível enviar o convite</div>
            <div style={{ fontSize: '13px' }}>{error.message}</div>
          </div>
        )}

        <label className="field">
          <span>E-mail</span>
          <input
            className="input"
            type="email"
            placeholder="pessoa@email.com"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
          />
        </label>

        <div>
          <div style={{ fontSize: '12px', opacity: 0.7, marginBottom: '8px' }}>Nível de acesso</div>
          <div className="seg" style={{ width: '100%', display: 'flex' }}>
            {ASSIGNABLE_ROLES.map((option) => (
              <label key={option} className="seg-opt" style={{ flex: 1, textAlign: 'center' }}>
                <input
                  type="radio"
                  checked={role === option}
                  onChange={() => setRole(option)}
                  style={{ display: 'none' }}
                />
                {ROLE_LABEL[option]}
              </label>
            ))}
          </div>
          <p style={{ fontSize: '12px', opacity: 0.6, margin: '8px 0 0' }}>{ROLE_DESCRIPTION[role]}</p>
        </div>

        <div className="dialog-actions">
          <button type="button" className="btn btn-secondary" disabled={isLoading} onClick={() => onOpenChange(false)}>
            Cancelar
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={isLoading}
            onClick={() => inviteMember({ email, role })}
          >
            Enviar convite
          </button>
        </div>
      </div>
    </div>
  )
}
