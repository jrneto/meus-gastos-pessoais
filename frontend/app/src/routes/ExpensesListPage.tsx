import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

export function ExpensesListPage() {
  const query = useExpensesQuery()

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <div className="flex w-full max-w-sm items-center justify-between">
        <h1 className="text-2xl font-semibold">Minhas despesas</h1>
        <Link to="/expenses/new" className={buttonVariants({ size: 'sm' })}>
          + Nova despesa
        </Link>
      </div>
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
