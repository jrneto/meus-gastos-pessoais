import type { MemberStatus } from '../api/membersApi'

export const MEMBER_STATUS_LABEL: Record<MemberStatus, string> = {
  ConvitePendente: 'Convite pendente',
  Ativo: 'Ativo',
  Inativo: 'Inativo',
}
