import { useEffect, useState } from 'react'
import '@/styles/modernist/modernist.css'
import { Trash2 } from 'lucide-react'
import type { MemberItem, MemberRole } from '../api/membersApi'
import { useUpdateMemberRole } from '../hooks/useUpdateMemberRole'
import { ROLE_LABEL } from '../utils/roleLabels'
import { MEMBER_STATUS_LABEL } from '../utils/statusLabels'

interface MemberRowProps {
  member: MemberItem
  readOnly: boolean
  isMe: boolean
  onRoleChanged: (updated: MemberItem) => void
  onRemoveRequested: (member: MemberItem) => void
}

const ASSIGNABLE_ROLES: Exclude<MemberRole, 'Titular'>[] = ['Leitura', 'Lancar', 'Total']

// Uma linha da lista de membros (FEAT-28), exceto a do Titular
// (tratada à parte em `MemberList`). Dona da própria instância de
// `useUpdateMemberRole` — necessário pra cada linha ter seu próprio
// estado de loading/erro/rollback (Regras de Hooks). Seletor de papel
// otimista: reflete a troca na hora, reverte só se a chamada falhar
// (decisão 5/US6 da spec).
export function MemberRow({ member, readOnly, isMe, onRoleChanged, onRemoveRequested }: MemberRowProps) {
  const { updateRole, error, success, data } = useUpdateMemberRole(member.id)
  const [optimisticRole, setOptimisticRole] = useState<MemberRole>(member.role)

  useEffect(() => {
    setOptimisticRole(member.role)
  }, [member.role])

  useEffect(() => {
    if (success && data) {
      onRoleChanged(data)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [success, data])

  useEffect(() => {
    if (error) {
      setOptimisticRole(member.role)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [error])

  function handleRoleChange(role: Exclude<MemberRole, 'Titular'>) {
    setOptimisticRole(role)
    updateRole(role)
  }

  // Um membro Inativo nunca tem ação de escrita disponível, mesmo pra
  // quem é Titular — nenhuma delas tem efeito útil sobre ele (a API
  // responde 422 pros dois casos), então a UI não oferece o que já se
  // sabe que vai falhar (FEAT-35, ver plan.md).
  const isEffectivelyReadOnly = readOnly || member.status === 'Inativo'

  return (
    <div
      style={{
        borderBottom: '1px solid var(--color-divider)',
        paddingBottom: '16px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: '16px',
      }}
    >
      <div style={{ minWidth: 0 }}>
        <div style={{ fontSize: '14px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
          {member.email}
          {isMe && ' (você)'}
        </div>
        <div style={{ fontSize: '12px', opacity: 0.55 }}>{MEMBER_STATUS_LABEL[member.status]}</div>
        {error && (
          <div style={{ fontSize: '12px', color: 'var(--color-accent-700)', marginTop: '4px' }}>{error.message}</div>
        )}
      </div>

      {isEffectivelyReadOnly ? (
        <span style={{ fontSize: '13px', flex: 'none' }}>{ROLE_LABEL[member.role]}</span>
      ) : (
        <div className="seg" style={{ flex: 'none' }}>
          {ASSIGNABLE_ROLES.map((role) => (
            <label key={role} className="seg-opt">
              <input
                type="radio"
                checked={optimisticRole === role}
                onChange={() => handleRoleChange(role)}
                style={{ display: 'none' }}
              />
              {ROLE_LABEL[role]}
            </label>
          ))}
        </div>
      )}

      {!isEffectivelyReadOnly && (
        <button
          type="button"
          className="btn"
          aria-label="Remover membro"
          style={{ color: 'var(--color-accent-700)', flex: 'none' }}
          onClick={() => onRemoveRequested(member)}
        >
          <Trash2 size={16} />
        </button>
      )}
    </div>
  )
}
