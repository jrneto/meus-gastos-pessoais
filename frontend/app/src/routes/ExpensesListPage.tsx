import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

export function ExpensesListPage() {
  const query = useExpensesQuery()

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Minhas despesas</h1>
      <ExpenseFilters onApply={query.applyFilters} />
      <ExpenseList
        items={query.items}
        isLoading={query.isLoading}
        isLoadingMore={query.isLoadingMore}
        error={query.error}
        hasMore={query.hasMore}
        onLoadMore={query.loadMore}
        onDeleted={query.removeItem}
      />
    </div>
  )
}
