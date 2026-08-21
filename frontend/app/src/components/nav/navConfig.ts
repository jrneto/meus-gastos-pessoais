import type { LucideIcon } from 'lucide-react'
import { BarChart3, Home, ListFilter, PlusCircle, Settings, Tag } from 'lucide-react'

export type NavItemStatus = 'active' | 'disabled' | 'placeholder'

export interface NavItem {
  id: string
  label: string
  icon: LucideIcon
  to?: string
  status: NavItemStatus
  mobilePrimary?: boolean
  children?: NavItem[]
}

export const NAV_TREE: NavItem[] = [
  { id: 'home', label: 'Início', icon: Home, to: '/', status: 'placeholder', mobilePrimary: true },
  {
    id: 'expenses',
    label: 'Despesas',
    icon: PlusCircle,
    status: 'active',
    children: [
      {
        id: 'expenses-new',
        label: 'Nova despesa',
        icon: PlusCircle,
        to: '/expenses/new',
        status: 'active',
        mobilePrimary: true,
      },
      {
        id: 'expenses-list',
        label: 'Listagem / Filtros',
        icon: ListFilter,
        to: '/expenses',
        status: 'active',
        mobilePrimary: true,
      },
    ],
  },
  { id: 'reports', label: 'Relatórios', icon: BarChart3, status: 'disabled' },
  { id: 'categories', label: 'Categorias', icon: Tag, to: '/categories', status: 'active' },
  {
    id: 'settings',
    label: 'Configurações',
    icon: Settings,
    to: '/settings',
    status: 'placeholder',
    mobilePrimary: true,
  },
]

export function flattenNavItems(items: NavItem[] = NAV_TREE): NavItem[] {
  return items.flatMap((item) => (item.children ? flattenNavItems(item.children) : [item]))
}
