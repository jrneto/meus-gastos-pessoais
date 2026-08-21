import type { LucideIcon } from 'lucide-react'
import {
  Book,
  Car,
  Coffee,
  Dumbbell,
  Gamepad2,
  Gift,
  GraduationCap,
  HeartPulse,
  Home,
  PawPrint,
  Plane,
  Shirt,
  ShoppingBag,
  Smartphone,
  Utensils,
  Wallet,
} from 'lucide-react'

export interface CategoryIconOption {
  value: string
  label: string
  Icon: LucideIcon
}

// Curadoria fixa de ícones (FEAT-13). O backend só valida
// presença/tamanho de `icone` (até 50 caracteres, sem catálogo
// fechado) — esta lista é só uma conveniência de UI do frontend.
export const CATEGORY_ICONS: CategoryIconOption[] = [
  { value: 'utensils', label: 'Alimentação', Icon: Utensils },
  { value: 'car', label: 'Transporte', Icon: Car },
  { value: 'home', label: 'Moradia', Icon: Home },
  { value: 'heart-pulse', label: 'Saúde', Icon: HeartPulse },
  { value: 'graduation-cap', label: 'Educação', Icon: GraduationCap },
  { value: 'gamepad-2', label: 'Lazer', Icon: Gamepad2 },
  { value: 'shopping-bag', label: 'Compras', Icon: ShoppingBag },
  { value: 'plane', label: 'Viagem', Icon: Plane },
  { value: 'wallet', label: 'Finanças', Icon: Wallet },
  { value: 'coffee', label: 'Café', Icon: Coffee },
  { value: 'gift', label: 'Presente', Icon: Gift },
  { value: 'dumbbell', label: 'Academia', Icon: Dumbbell },
  { value: 'paw-print', label: 'Pet', Icon: PawPrint },
  { value: 'book', label: 'Livros', Icon: Book },
  { value: 'smartphone', label: 'Assinaturas', Icon: Smartphone },
  { value: 'shirt', label: 'Vestuário', Icon: Shirt },
]

export function findCategoryIcon(value: string): LucideIcon | null {
  return CATEGORY_ICONS.find((icon) => icon.value === value)?.Icon ?? null
}
