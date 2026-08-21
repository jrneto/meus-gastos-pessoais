import { Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { CategoryBadge } from '@/lib/categories/CategoryBadge'
import type { CategoryItem } from '@/lib/categories/types'
import { CategoryDeleteDialog } from './CategoryDeleteDialog'

interface CategoryListProps {
  items: CategoryItem[]
  isLoading: boolean
  error: Error | null
  onDeleted: (id: string) => void
}

export function CategoryList({ items, isLoading, error, onDeleted }: CategoryListProps) {
  const [deleteTarget, setDeleteTarget] = useState<CategoryItem | null>(null)

  return (
    <div className="flex w-full max-w-sm flex-col gap-4">
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível buscar as categorias</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {!isLoading && items.length === 0 && !error && (
        <div className="flex flex-col items-center gap-4 py-8 text-center">
          <p className="text-sm text-muted-foreground">
            Você ainda não tem nenhuma categoria cadastrada.
          </p>
          <Link to="/categories/new" className={cn(buttonVariants({}))}>
            Criar categoria
          </Link>
        </div>
      )}

      <ul className="flex flex-col gap-2">
        {items.map((item) => (
          <li
            key={item.id}
            className="flex items-center justify-between rounded-lg border border-border px-2.5 py-2 text-sm"
          >
            <CategoryBadge category={item} />
            <div className="flex items-center gap-2">
              <Link
                to={`/categories/${item.id}/edit`}
                aria-label="Editar categoria"
                className={cn(buttonVariants({ variant: 'ghost', size: 'icon-sm' }))}
              >
                <Pencil className="size-4" />
              </Link>
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Excluir categoria"
                onClick={() => setDeleteTarget(item)}
              >
                <Trash2 className="size-4" />
              </Button>
            </div>
          </li>
        ))}
      </ul>

      <CategoryDeleteDialog
        key={deleteTarget?.id ?? 'closed'}
        category={deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onDeleted={(id) => {
          onDeleted(id)
          setDeleteTarget(null)
        }}
      />
    </div>
  )
}
