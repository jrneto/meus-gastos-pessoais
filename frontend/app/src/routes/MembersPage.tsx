import { useEffect, useState } from 'react'
import '@/styles/modernist/modernist.css'
import { Toast } from '@/components/Toast'
import { InviteMemberDialog } from '@/features/members/components/InviteMemberDialog'
import { MemberList } from '@/features/members/components/MemberList'
import { MemberRemoveDialog } from '@/features/members/components/MemberRemoveDialog'
import { useMembers } from '@/features/members/hooks/useMembers'
import type { MemberItem } from '@/features/members/api/membersApi'
import { useCurrentUser } from '@/lib/auth/useCurrentUser'

// Tela "Membros da conta" (FEAT-28). Busca `GET /members` + `GET
// /auth/me` em paralelo; deriva quem é o Titular e se o usuário logado
// é ele (decisão 3 da spec). Mutações atualizam o estado local
// (`localOthers`) sem recarregar a lista inteira (ver plan.md).
export function MembersPage() {
  const { items, isLoading: membersLoading, error: membersError } = useMembers()
  const { data: currentUser, isLoading: userLoading, error: userError } = useCurrentUser()
  const isLoading = membersLoading || userLoading
  const error = membersError ?? userError

  const titular = items.find((m) => m.role === 'Titular') ?? null
  const others = items.filter((m) => m.role !== 'Titular')
  const isViewerTitular = !!currentUser && !!titular && currentUser.email === titular.email

  const [localOthers, setLocalOthers] = useState<MemberItem[]>([])
  const [inviteOpen, setInviteOpen] = useState(false)
  const [removeTarget, setRemoveTarget] = useState<MemberItem | null>(null)
  const [toastMessage, setToastMessage] = useState<string | null>(null)

  useEffect(() => {
    setLocalOthers(others)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [items])

  function handleInvited(member: MemberItem) {
    setLocalOthers((prev) => [...prev, member])
    setInviteOpen(false)
    setToastMessage(`Convite enviado para ${member.email}.`)
  }

  function handleRoleChanged(updated: MemberItem) {
    setLocalOthers((prev) => prev.map((item) => (item.id === updated.id ? updated : item)))
  }

  function handleRemoved(id: string) {
    setLocalOthers((prev) => prev.filter((item) => item.id !== id))
    setRemoveTarget(null)
  }

  return (
    <div
      className="ds-modernist"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-6)',
        maxWidth: '640px',
        margin: '0 auto',
        padding: '40px 40px 60px',
        boxSizing: 'border-box',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end', gap: '20px' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Membros da conta</h1>
        {isViewerTitular && (
          <button type="button" className="btn btn-primary" style={{ flex: 'none' }} onClick={() => setInviteOpen(true)}>
            + Convidar pessoa
          </button>
        )}
      </div>

      {isLoading && <p style={{ opacity: 0.7, fontSize: '14px' }}>Carregando...</p>}

      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível carregar os membros</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      {!isLoading && !error && (
        <MemberList
          titular={titular}
          others={localOthers}
          isViewerTitular={isViewerTitular}
          currentUserEmail={currentUser?.email ?? null}
          onRoleChanged={handleRoleChanged}
          onRemoveRequested={setRemoveTarget}
        />
      )}

      <InviteMemberDialog open={inviteOpen} onOpenChange={setInviteOpen} onInvited={handleInvited} />
      <MemberRemoveDialog member={removeTarget} onOpenChange={(open) => !open && setRemoveTarget(null)} onRemoved={handleRemoved} />
      <Toast message={toastMessage} onDismiss={() => setToastMessage(null)} />
    </div>
  )
}
