import { PanelLeft, PanelLeftClose } from 'lucide-react'
import { useState } from 'react'
import { useLocation } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { AccountFooter } from './AccountFooter'
import { NAV_TREE } from './navConfig'
import { NavItemRow } from './NavItemRow'

export function DesktopSidebar() {
  const [collapsed, setCollapsed] = useState(false)
  const { pathname } = useLocation()

  return (
    <nav
      aria-label="Navegação principal"
      className="ds-modernist ds-modernist-sidebar"
      style={{
        flexShrink: 0,
        flexDirection: 'column',
        gap: '4px',
        borderRight: '2px solid var(--color-divider)',
        padding: '8px',
        width: collapsed ? '56px' : '224px',
      }}
    >
      <button
        type="button"
        className="btn btn-ghost"
        style={{ marginBottom: '8px', alignSelf: 'flex-end' }}
        onClick={() => setCollapsed((value) => !value)}
        aria-label={collapsed ? 'Expandir menu' : 'Colapsar menu'}
      >
        {collapsed ? <PanelLeft size={16} /> : <PanelLeftClose size={16} />}
      </button>

      {NAV_TREE.map((item) => (
        <NavItemRow key={item.id} item={item} isActive={pathname === item.to} collapsed={collapsed} />
      ))}

      <AccountFooter collapsed={collapsed} />
    </nav>
  )
}
