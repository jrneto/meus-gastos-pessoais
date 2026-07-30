import { Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { EXPENSE_CATEGORIES } from '../constants/expenseCategories'
import { formatCentsToCurrency } from '../utils/currency'
import { ExpenseDeleteDialog } from './ExpenseDeleteDialog'

interface ExpenseListProps {
  items: ExpenseQueryItem[]
  isLoading: boolean
  isLoadingMore: boolean
  error: Error | null
  hasMore: boolean
  onLoadMore: () => void
  onDeleted: (id: string) => void
}

function categoryLabel(value: string): string {
  return EXPENSE_CATEGORIES.find((category) => category.value === value)?.label ?? value
}

export function ExpenseList({
  items,
  isLoading,
  isLoadingMore,
  error,
  hasMore,
  onLoadMore,
  onDeleted,
}: ExpenseListProps) {
  const [deleteTarget, setDeleteTarget] = useState<ExpenseQueryItem | null>(null)

  return (
    <div className="flex w-full max-w-sm flex-col gap-4">
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível buscar as despesas</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {!isLoading && items.length === 0 && !error && (
        <p className="text-sm text-muted-foreground">
          Nenhuma despesa encontrada para os filtros selecionados.
        </p>
      )}

      <ul className="flex flex-col gap-2">
        {items.map((item) => (
          <li
            key={item.id}
            className="flex items-center justify-between rounded-lg border border-border px-2.5 py-2 text-sm"
          >
            <Link to={`/expenses/${item.id}`} className="flex flex-col">
              <span className="font-medium hover:underline">{item.description}</span>
              <span className="text-muted-foreground">
                {categoryLabel(item.category)} · {item.expenseDate}
              </span>
            </Link>
            <div className="flex items-center gap-2">
              <span className="font-medium">{formatCentsToCurrency(item.amountInCents)}</span>
              <Link
                to={`/expenses/${item.id}/edit`}
                aria-label="Editar despesa"
                className={cn(buttonVariants({ variant: 'ghost', size: 'icon-sm' }))}
              >
                <Pencil className="size-4" />
              </Link>
              <Button
                variant="ghost"
                size="icon-sm"
                aria-label="Excluir despesa"
                onClick={() => setDeleteTarget(item)}
              >
                <Trash2 className="size-4" />
              </Button>
            </div>
          </li>
        ))}
      </ul>

      {hasMore && (
        <Button variant="outline" onClick={onLoadMore} disabled={isLoadingMore}>
          {isLoadingMore ? 'Carregando...' : 'Carregar mais'}
        </Button>
      )}

      <ExpenseDeleteDialog
        key={deleteTarget?.id ?? 'closed'}
        expense={deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onDeleted={(id) => {
          onDeleted(id)
          setDeleteTarget(null)
        }}
      />
    </div>
  )
}
