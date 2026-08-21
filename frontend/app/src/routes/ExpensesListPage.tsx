import { Link } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

export function ExpensesListPage() {
  const query = useExpensesQuery()

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Transações</h1>
        <Link to="/expenses/new" className="btn btn-primary">
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
