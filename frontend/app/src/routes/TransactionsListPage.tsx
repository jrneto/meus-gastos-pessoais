import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import { TransactionDeleteDialog } from '@/features/transactions/components/TransactionDeleteDialog'
import { TransactionDetailDialog } from '@/features/transactions/components/TransactionDetailDialog'
import { TransactionFilters } from '@/features/transactions/components/TransactionFilters'
import { TransactionFormDialog } from '@/features/transactions/components/TransactionFormDialog'
import { TransactionList } from '@/features/transactions/components/TransactionList'
import type { TransactionQueryItem } from '@/features/transactions/api/transactionsApi'
import { useTransactionsQuery } from '@/features/transactions/hooks/useTransactionsQuery'

type TransactionFormTarget =
  | { mode: 'create'; tipo: 'despesa' | 'receita' }
  | { mode: 'edit'; id: string }
  | null

export function TransactionsListPage() {
  const query = useTransactionsQuery()
  const [formTarget, setFormTarget] = useState<TransactionFormTarget>(null)
  const [detailTarget, setDetailTarget] = useState<TransactionQueryItem | null>(null)
  const [deleteTarget, setDeleteTarget] = useState<TransactionQueryItem | null>(null)

  function handleEditFromDetail(item: TransactionQueryItem) {
    setDetailTarget(null)
    setFormTarget({ mode: 'edit', id: item.id })
  }

  function handleDeleteFromDetail(item: TransactionQueryItem) {
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
        <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setFormTarget({ mode: 'create', tipo: 'receita' })}
          >
            + Nova receita
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => setFormTarget({ mode: 'create', tipo: 'despesa' })}
          >
            + Nova despesa
          </button>
        </div>
      </div>
      <TransactionFilters onApply={query.applyFilters} />
      <TransactionList
        items={query.items}
        isLoading={query.isLoading}
        isLoadingMore={query.isLoadingMore}
        error={query.error}
        hasMore={query.hasMore}
        onLoadMore={query.loadMore}
        onRowClick={setDetailTarget}
      />
      <TransactionFormDialog
        key={formTarget ? (formTarget.mode === 'edit' ? formTarget.id : `create-${formTarget.tipo}`) : 'closed'}
        open={formTarget !== null}
        transactionId={formTarget?.mode === 'edit' ? formTarget.id : undefined}
        tipo={formTarget?.mode === 'create' ? formTarget.tipo : undefined}
        onOpenChange={(open) => !open && setFormTarget(null)}
        onSaved={query.refetch}
      />
      <TransactionDetailDialog
        transaction={detailTarget}
        onOpenChange={(open) => !open && setDetailTarget(null)}
        onEdit={handleEditFromDetail}
        onDelete={handleDeleteFromDetail}
      />
      <TransactionDeleteDialog
        key={deleteTarget?.id ?? 'closed'}
        transaction={deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onDeleted={(id) => {
          query.removeItem(id)
          setDeleteTarget(null)
        }}
      />
    </div>
  )
}
