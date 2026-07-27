import { PanelLeft, PanelLeftClose } from 'lucide-react'
import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { flattenNavItems, NAV_TREE } from './navConfig'
import { NavItemRow } from './NavItemRow'

export function DesktopSidebar() {
  const [collapsed, setCollapsed] = useState(false)
  const { pathname } = useLocation()

  return (
    <nav
      aria-label="Navegação principal"
      className={cn(
        'hidden shrink-0 flex-col gap-1 border-r border-border p-2 md:flex',
        collapsed ? 'w-14' : 'w-56',
      )}
    >
      <Button
        variant="ghost"
        size="icon-sm"
        className="mb-2 self-end"
        onClick={() => setCollapsed((value) => !value)}
        aria-label={collapsed ? 'Expandir menu' : 'Colapsar menu'}
      >
        {collapsed ? <PanelLeft className="size-4" /> : <PanelLeftClose className="size-4" />}
      </Button>

      {collapsed
        ? flattenNavItems(NAV_TREE).map((item) => (
            <NavItemRow key={item.id} item={item} isActive={pathname === item.to} collapsed />
          ))
        : NAV_TREE.map((item) =>
            item.children ? (
              <div key={item.id} className="flex flex-col gap-1">
                <div className="flex items-center gap-2 px-2.5 py-1 text-sm font-medium text-muted-foreground">
                  <item.icon className="size-4 shrink-0" />
                  <span>{item.label}</span>
                </div>
                <div className="flex flex-col gap-1 pl-4">
                  {item.children.map((child) => (
                    <NavItemRow key={child.id} item={child} isActive={pathname === child.to} />
                  ))}
                </div>
              </div>
            ) : (
              <NavItemRow key={item.id} item={item} isActive={pathname === item.to} />
            ),
          )}
    </nav>
  )
}
