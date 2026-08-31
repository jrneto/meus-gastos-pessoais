import '@/styles/modernist/modernist.css'
import type { MemberItem } from '../api/membersApi'
import { MemberRow } from './MemberRow'

interface MemberListProps {
  titular: MemberItem | null
  others: MemberItem[]
  isViewerTitular: boolean
  currentUserEmail: string | null
  onRoleChanged: (updated: MemberItem) => void
  onRemoveRequested: (member: MemberItem) => void
}

// Lista de "Membros da conta" (FEAT-28) — a linha do Titular é sempre
// destacada à parte (tag "Titular", descrição fixa, sem seletor de
// papel nem remover, já que essas ações nunca se aplicam a ele); as
// demais linhas usam `MemberRow`, que já trata `readOnly` (decisão 1
// da spec, quem não é Titular não vê ações de escrita).
export function MemberList({
  titular,
  others,
  isViewerTitular,
  currentUserEmail,
  onRoleChanged,
  onRemoveRequested,
}: MemberListProps) {
  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
      {titular && (
        <div
          style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            paddingBottom: '14px',
            borderBottom: '1px solid var(--color-divider)',
          }}
        >
          <div>
            <div style={{ fontSize: '14px', fontWeight: 600 }}>
              {titular.email === currentUserEmail ? 'Você (titular)' : titular.email}
            </div>
            <div style={{ fontSize: '12px', opacity: 0.55 }}>Acesso total · gerencia membros</div>
          </div>
          <span className="tag tag-neutral">Titular</span>
        </div>
      )}

      {others.map((member) => (
        <MemberRow
          key={member.id}
          member={member}
          readOnly={!isViewerTitular}
          isMe={member.email === currentUserEmail}
          onRoleChanged={onRoleChanged}
          onRemoveRequested={onRemoveRequested}
        />
      ))}
    </div>
  )
}
