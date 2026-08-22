import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import { NewExpenseDialog } from '@/features/expenses/components/NewExpenseDialog'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

export function ExpensesListPage() {
  const query = useExpensesQuery()
  const [isAddOpen, setIsAddOpen] = useState(false)

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Transações</h1>
        <button type="button" className="btn btn-primary" onClick={() => setIsAddOpen(true)}>
          + Nova despesa
        </button>
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
      <NewExpenseDialog open={isAddOpen} onOpenChange={setIsAddOpen} onCreated={query.refetch} />
    </div>
  )
}
