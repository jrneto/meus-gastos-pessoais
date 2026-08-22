import { MoreHorizontal } from 'lucide-react'
import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { flattenNavItems, NAV_TREE } from './navConfig'
import { NavMoreSheet } from './NavMoreSheet'

const PRIMARY_ITEMS = flattenNavItems(NAV_TREE).filter((item) => item.mobilePrimary)

function tabItemStyle(isActive: boolean): React.CSSProperties {
  return {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    gap: '3px',
    padding: '8px 0',
    fontSize: '11px',
    color: isActive ? 'var(--color-text)' : 'var(--color-neutral-500)',
    fontWeight: isActive ? 600 : 400,
    textDecoration: 'none',
    background: 'none',
    border: 'none',
    cursor: 'pointer',
  }
}

export function MobileBottomNav() {
  const [moreOpen, setMoreOpen] = useState(false)
  const { pathname } = useLocation()

  return (
    <>
      <nav
        aria-label="Navegação principal"
        className="ds-modernist ds-modernist-bottom-nav"
        style={{
          position: 'fixed',
          insetInline: 0,
          bottom: 0,
          alignItems: 'center',
          height: '64px',
          borderTop: '2px solid var(--color-divider)',
          background: 'var(--color-bg)',
        }}
      >
        {PRIMARY_ITEMS.map((item) => {
          const isActive = pathname === item.to
          const Icon = item.icon
          return (
            <Link
              key={item.id}
              to={item.to ?? '#'}
              aria-current={isActive ? 'page' : undefined}
              style={tabItemStyle(isActive)}
            >
              <Icon size={20} />
              <span>{item.label}</span>
            </Link>
          )
        })}
        <button type="button" onClick={() => setMoreOpen(true)} style={tabItemStyle(false)}>
          <MoreHorizontal size={20} />
          <span>Mais</span>
        </button>
      </nav>

      <NavMoreSheet open={moreOpen} onOpenChange={setMoreOpen} />
    </>
  )
}
