import { MoreHorizontal } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { cn } from '@/lib/utils'
import { flattenNavItems, NAV_TREE } from './navConfig'
import { NavMoreSheet } from './NavMoreSheet'

const PRIMARY_ITEMS = flattenNavItems(NAV_TREE).filter((item) => item.mobilePrimary)

export function MobileBottomNav() {
  const [moreOpen, setMoreOpen] = useState(false)
  const { pathname } = useLocation()

  return (
    <>
      <nav
        aria-label="Navegação principal"
        className="fixed inset-x-0 bottom-0 flex h-16 items-center justify-around border-t border-border bg-background md:hidden"
      >
        {PRIMARY_ITEMS.map((item) => {
          const isActive = pathname === item.to
          const Icon = item.icon
          return (
            <Link
              key={item.id}
              to={item.to ?? '#'}
              aria-current={isActive ? 'page' : undefined}
              className={cn(
                'flex flex-1 flex-col items-center gap-0.5 py-2 text-xs text-muted-foreground',
                isActive && 'font-medium text-foreground',
              )}
            >
              <Icon className="size-5" />
              <span>{item.label}</span>
            </Link>
          )
        })}
        <button
          type="button"
          onClick={() => setMoreOpen(true)}
          className="flex flex-1 flex-col items-center gap-0.5 py-2 text-xs text-muted-foreground"
        >
          <MoreHorizontal className="size-5" />
          <span>Mais</span>
        </button>
      </nav>

      <NavMoreSheet open={moreOpen} onOpenChange={setMoreOpen} />
    </>
  )
}
