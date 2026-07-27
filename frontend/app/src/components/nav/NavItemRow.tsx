import { Link } from 'react-router-dom'
import { cn } from '@/lib/utils'
import type { NavItem } from './navConfig'

interface NavItemRowProps {
  item: NavItem
  isActive: boolean
  collapsed?: boolean
}

export function NavItemRow({ item, isActive, collapsed = false }: NavItemRowProps) {
  const Icon = item.icon

  if (item.status === 'disabled' || !item.to) {
    return (
      <div
        role="button"
        aria-disabled="true"
        tabIndex={-1}
        title={item.label}
        className="flex cursor-not-allowed items-center gap-2 rounded-lg px-2.5 py-2 text-sm text-muted-foreground/50"
      >
        <Icon className="size-4 shrink-0" />
        {!collapsed && <span>{item.label}</span>}
      </div>
    )
  }

  return (
    <Link
      to={item.to}
      title={item.label}
      aria-current={isActive ? 'page' : undefined}
      className={cn(
        'flex items-center gap-2 rounded-lg px-2.5 py-2 text-sm hover:bg-muted',
        isActive && 'bg-muted font-medium text-foreground',
      )}
    >
      <Icon className="size-4 shrink-0" />
      {!collapsed && <span>{item.label}</span>}
    </Link>
  )
}
