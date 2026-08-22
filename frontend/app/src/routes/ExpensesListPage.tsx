import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseFormDialog } from '@/features/expenses/components/ExpenseFormDialog'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

type ExpenseDialogTarget = { mode: 'create' } | { mode: 'edit'; id: string } | null

export function ExpensesListPage() {
  const query = useExpensesQuery()
  const [dialogTarget, setDialogTarget] = useState<ExpenseDialogTarget>(null)

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Transações</h1>
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setDialogTarget({ mode: 'create' })}
        >
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
        onEdit={(item) => setDialogTarget({ mode: 'edit', id: item.id })}
      />
      <ExpenseFormDialog
        key={dialogTarget ? (dialogTarget.mode === 'edit' ? dialogTarget.id : 'create') : 'closed'}
        open={dialogTarget !== null}
        expenseId={dialogTarget?.mode === 'edit' ? dialogTarget.id : undefined}
        onOpenChange={(open) => !open && setDialogTarget(null)}
        onSaved={query.refetch}
      />
    </div>
  )
}
