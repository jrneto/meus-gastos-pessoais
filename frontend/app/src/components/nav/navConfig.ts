import type { LucideIcon } from 'lucide-react'
import { BarChart3, Home, ListFilter, Settings, Tag, Users } from 'lucide-react'

export type NavItemStatus = 'active' | 'placeholder'

export interface NavItem {
  id: string
  label: string
  icon: LucideIcon
  to?: string
  status: NavItemStatus
  mobilePrimary?: boolean
  children?: NavItem[]
}

// FEAT-15: o menu recriado no Modernist não tem mais grupos com
// subitens (o design de referência trata cada área de negócio como um
// único item; "Nova despesa" virou um botão dentro da tela de
// listagem) nem itens desabilitados/sem rota — "Relatórios" ainda não
// existe de verdade, mas agora navega para uma página fake em vez de
// ficar cinza e sem ação.
export const NAV_TREE: NavItem[] = [
  { id: 'home', label: 'Início', icon: Home, to: '/', status: 'active', mobilePrimary: true },
  {
    id: 'transactions',
    label: 'Transações',
    icon: ListFilter,
    to: '/transactions',
    status: 'active',
    mobilePrimary: true,
  },
  { id: 'reports', label: 'Relatórios', icon: BarChart3, to: '/reports', status: 'active' },
  { id: 'categories', label: 'Categorias', icon: Tag, to: '/categories', status: 'active' },
  { id: 'members', label: 'Membros', icon: Users, to: '/members', status: 'active' },
  {
    // FEAT-30: rótulo alinhado ao protótipo (`.dc.html`, bloco `isSet`
    // rotula o próprio item de menu como "Ajustes", igual ao título da
    // página) — antes "Configurações", inconsistente com o título
    // "Ajustes" da página por trás. `status: 'active'`: era o único
    // item ainda `placeholder` (comentário acima já dizia que nenhum
    // deveria ficar assim desde a FEAT-15); sem efeito visual em
    // `NavItemRow` hoje, só metadado.
    id: 'settings',
    label: 'Ajustes',
    icon: Settings,
    to: '/settings',
    status: 'active',
    mobilePrimary: true,
  },
]

export function flattenNavItems(items: NavItem[] = NAV_TREE): NavItem[] {
  return items.flatMap((item) => (item.children ? flattenNavItems(item.children) : [item]))
}
