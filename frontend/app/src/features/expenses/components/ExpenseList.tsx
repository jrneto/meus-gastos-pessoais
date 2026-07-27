import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { EXPENSE_CATEGORIES } from '../constants/expenseCategories'
import { formatCentsToCurrency } from '../utils/currency'

interface ExpenseListProps {
  items: ExpenseQueryItem[]
  isLoading: boolean
  isLoadingMore: boolean
  error: Error | null
  hasMore: boolean
  onLoadMore: () => void
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
}: ExpenseListProps) {
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
            <div className="flex flex-col">
              <span className="font-medium">{item.description}</span>
              <span className="text-muted-foreground">
                {categoryLabel(item.category)} · {item.expenseDate}
              </span>
            </div>
            <span className="font-medium">{formatCentsToCurrency(item.amountInCents)}</span>
          </li>
        ))}
      </ul>

      {hasMore && (
        <Button variant="outline" onClick={onLoadMore} disabled={isLoadingMore}>
          {isLoadingMore ? 'Carregando...' : 'Carregar mais'}
        </Button>
      )}
    </div>
  )
}
