import { Link } from 'react-router-dom'
import type { NavItem } from './navConfig'

interface NavItemRowProps {
  item: NavItem
  isActive: boolean
  collapsed?: boolean
}

export function NavItemRow({ item, isActive, collapsed = false }: NavItemRowProps) {
  const Icon = item.icon

  // FEAT-15: todo item de NAV_TREE tem `to` (nenhum fica desabilitado
  // mais) — o guard abaixo é só defensivo, caso um item futuro seja
  // adicionado sem rota.
  if (!item.to) {
    return null
  }

  return (
    <Link
      to={item.to}
      title={item.label}
      aria-current={isActive ? 'page' : undefined}
      style={{
        display: 'flex',
        alignItems: 'center',
        gap: '10px',
        padding: '11px 12px',
        borderLeft: `2px solid ${isActive ? 'var(--color-accent)' : 'transparent'}`,
        background: isActive ? 'var(--color-neutral-200)' : 'transparent',
        color: isActive ? 'var(--color-text)' : 'var(--color-neutral-700)',
        fontWeight: isActive ? 600 : 400,
        fontSize: '13.5px',
        textDecoration: 'none',
      }}
    >
      <Icon size={18} style={{ flexShrink: 0 }} />
      {!collapsed && <span>{item.label}</span>}
    </Link>
  )
}
