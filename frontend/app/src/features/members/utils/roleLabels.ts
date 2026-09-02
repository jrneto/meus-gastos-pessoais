import type { MemberRole } from '../api/membersApi'

export const ROLE_LABEL: Record<MemberRole, string> = {
  Leitura: 'Leitura',
  Lancar: 'Lançar',
  Total: 'Total',
  Titular: 'Titular',
}

export const ROLE_DESCRIPTION: Record<Exclude<MemberRole, 'Titular'>, string> = {
  Leitura: 'Pode visualizar despesas e relatórios, sem editar nada.',
  Lancar: 'Pode visualizar e lançar novas despesas.',
  Total: 'Pode visualizar, lançar despesas e criar categorias e orçamentos. Não pode gerenciar outros membros.',
}
