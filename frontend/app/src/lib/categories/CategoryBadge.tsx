import { Tag } from 'lucide-react'
import { findCategoryIcon } from './categoryIcons'
import type { CategoryItem } from './types'

interface CategoryBadgeProps {
  category: Pick<CategoryItem, 'nome' | 'cor' | 'icone'> | undefined
}

export function CategoryBadge({ category }: CategoryBadgeProps) {
  if (!category) {
    return <span className="text-muted-foreground">Categoria não encontrada</span>
  }

  const Icon = findCategoryIcon(category.icone) ?? Tag

  return (
    <span className="inline-flex items-center gap-1.5" style={{ color: category.cor }}>
      <Icon className="size-4 shrink-0" />
      <span>{category.nome}</span>
    </span>
  )
}
