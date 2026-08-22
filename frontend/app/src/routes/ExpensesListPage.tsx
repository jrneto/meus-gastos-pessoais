import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import { ExpenseDeleteDialog } from '@/features/expenses/components/ExpenseDeleteDialog'
import { ExpenseDetailDialog } from '@/features/expenses/components/ExpenseDetailDialog'
import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseFormDialog } from '@/features/expenses/components/ExpenseFormDialog'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import type { ExpenseQueryItem } from '@/features/expenses/api/expensesApi'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

type ExpenseFormTarget = { mode: 'create' } | { mode: 'edit'; id: string } | null

export function ExpensesListPage() {
  const query = useExpensesQuery()
  const [formTarget, setFormTarget] = useState<ExpenseFormTarget>(null)
  const [detailTarget, setDetailTarget] = useState<ExpenseQueryItem | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<ExpenseQueryItem | null>(null)

  function handleEditFromDetail(item: ExpenseQueryItem) {
    setDetailTarget(null)
    setFormTarget({ mode: 'edit', id: item.id })
  }

  function handleDeleteFromDetail(item: ExpenseQueryItem) {
    setDetailTarget(null)
    setDeleteTarget(item)
  }

  return (
    <div
      className="ds-modernist"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-6)',
        maxWidth: '920px',
        margin: '0 auto',
        padding: '40px 40px 60px',
        boxSizing: 'border-box',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Transações</h1>
        <button type="button" className="btn btn-primary" onClick={() => setFormTarget({ mode: 'create' })}>
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
        onRowClick={setDetailTarget}
      />
      <ExpenseFormDialog
        key={formTarget ? (formTarget.mode === 'edit' ? formTarget.id : 'create') : 'closed'}
        open={formTarget !== null}
        expenseId={formTarget?.mode === 'edit' ? formTarget.id : undefined}
        onOpenChange={(open) => !open && setFormTarget(null)}
        onSaved={query.refetch}
      />
      <ExpenseDetailDialog
        expense={detailTarget}
        onOpenChange={(open) => !open && setDetailTarget(null)}
        onEdit={handleEditFromDetail}
        onDelete={handleDeleteFromDetail}
      />
      <ExpenseDeleteDialog
        key={deleteTarget?.id ?? 'closed'}
        expense={deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onDeleted={(id) => {
          query.removeItem(id)
          setDeleteTarget(null)
        }}
      />
    </div>
  )
}
