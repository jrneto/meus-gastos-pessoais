import type { MemberRole } from './types'

// Matriz de autorização já aplicada pelo backend desde a FEAT-20
// (atualizada pela FEAT-22 para a regra de autoria de transações) —
// funções puras, só espelham no frontend uma decisão que a API já toma
// (ver frontend/specs/FEAT-29-permissoes-por-role/spec.md).

export function canCreateTransaction(role: MemberRole | null): boolean {
  return role === 'Lancar' || role === 'Total' || role === 'Titular'
}

// Editar e excluir uma transação seguem sempre a mesma regra — um
// único flag em vez de dois booleanos idênticos.
export function canManageTransaction(role: MemberRole | null, isOwn: boolean): boolean {
  if (role === 'Total' || role === 'Titular') return true
  if (role === 'Lancar') return isOwn
  return false
}

export function canWriteCategories(role: MemberRole | null): boolean {
  return role === 'Total' || role === 'Titular'
}
