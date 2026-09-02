import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import type { MemberItem } from '../api/membersApi'
import { NotFoundError } from '../errors/memberErrors'
import { useRemoveMember } from '../hooks/useRemoveMember'

interface MemberRemoveDialogProps {
  member: MemberItem | null
  onOpenChange: (open: boolean) => void
  onRemoved: (id: string) => void
}

// Confirmação de remoção (decisão 4 da spec) — mesmo padrão de
// `CategoryDeleteDialog`/`TransactionDeleteDialog`.
export function MemberRemoveDialog({ member, onOpenChange, onRemoved }: MemberRemoveDialogProps) {
  const { removeMember, isLoading, error, success } = useRemoveMember()
  const open = member !== null

  useEffect(() => {
    if (success && member) {
      onRemoved(member.id)
    }
  }, [success, member, onRemoved])

  useEffect(() => {
    if (error instanceof NotFoundError && member) {
      onRemoved(member.id)
    }
  }, [error, member, onRemoved])

  useEffect(() => {
    if (!open) return undefined

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onOpenChange(false)
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onOpenChange])

  if (!open) {
    return null
  }

  const otherError = error && !(error instanceof NotFoundError) ? error : null

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="remove-member-title"
        aria-describedby="remove-member-description"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="remove-member-title">
          Remover membro
        </div>
        <p className="dialog-body" id="remove-member-description">
          Tem certeza que deseja remover "{member?.email}" da conta? Essa ação não pode ser desfeita.
        </p>

        {otherError && (
          <div style={{ color: 'var(--color-accent-700)' }}>
            <div style={{ fontWeight: 700 }}>Não foi possível remover</div>
            <div style={{ fontSize: '13px' }}>{otherError.message}</div>
          </div>
        )}

        <div className="dialog-actions">
          <button type="button" className="btn btn-secondary" onClick={() => onOpenChange(false)}>
            Cancelar
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={isLoading}
            onClick={() => member && removeMember(member.id)}
          >
            {isLoading ? 'Removendo...' : 'Remover'}
          </button>
        </div>
      </div>
    </div>
  )
}
