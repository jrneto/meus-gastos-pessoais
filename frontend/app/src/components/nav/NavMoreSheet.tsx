import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { NAV_TREE } from './navConfig'
import { NavItemRow } from './NavItemRow'

interface NavMoreSheetProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const MORE_ITEMS = NAV_TREE.filter((item) => !item.mobilePrimary)

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist) no lugar do
// `Sheet` do shadcn/ui — ver plan.md FEAT-15 para o racional.
export function NavMoreSheet({ open, onOpenChange }: NavMoreSheetProps) {
  const { pathname } = useLocation()

  useEffect(() => {
    if (!open) return undefined

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onOpenChange(false)
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onOpenChange])

  if (!open) {
    return null
  }

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="nav-more-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="nav-more-title">
          Mais
        </div>
        <div
          style={{ display: 'flex', flexDirection: 'column', gap: '4px' }}
          onClick={() => onOpenChange(false)}
        >
          {MORE_ITEMS.map((item) => (
            <NavItemRow key={item.id} item={item} isActive={pathname === item.to} />
          ))}
        </div>
      </div>
    </div>
  )
}
