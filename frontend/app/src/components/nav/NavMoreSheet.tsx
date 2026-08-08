import { useLocation } from 'react-router-dom'
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet'
import { NAV_TREE } from './navConfig'
import { NavItemRow } from './NavItemRow'

interface NavMoreSheetProps {
  open: boolean
  onOpenChange: (open: boolean) => void
}

const MORE_ITEMS = NAV_TREE.filter((item) => !item.children && !item.mobilePrimary)

export function NavMoreSheet({ open, onOpenChange }: NavMoreSheetProps) {
  const { pathname } = useLocation()

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="bottom">
        <SheetHeader>
          <SheetTitle>Mais</SheetTitle>
        </SheetHeader>
        <div className="flex flex-col gap-1 p-4 pt-0">
          {MORE_ITEMS.map((item) => (
            <NavItemRow key={item.id} item={item} isActive={pathname === item.to} />
          ))}
        </div>
      </SheetContent>
    </Sheet>
  )
}
