import { Pencil, Trash2 } from 'lucide-react'
import { useMemo, useState } from 'react'
import { useNavigate } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { useCategories } from '@/lib/categories/useCategories'
import type { ExpenseQueryItem } from '../api/expensesApi'
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
  onEdit: (item: ExpenseQueryItem) => void
}

export function ExpenseList({
  items,
  isLoading,
  isLoadingMore,
  error,
  hasMore,
  onLoadMore,
  onDeleted,
  onEdit,
}: ExpenseListProps) {
  const [deleteTarget, setDeleteTarget] = useState<ExpenseQueryItem | null>(null)
  const { items: categories } = useCategories()
  const navigate = useNavigate()
  const categoryById = useMemo(
    () => new Map(categories.map((category) => [category.id, category])),
    [categories],
  )

  return (
    <div className="ds-modernist" style={{ display: 'flex', width: '100%', flexDirection: 'column', gap: 'var(--space-4)' }}>
      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível buscar as despesas</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      {!isLoading && items.length === 0 && !error && (
        <p style={{ opacity: 0.55, fontSize: '13px' }}>
          Nenhuma despesa encontrada para os filtros selecionados.
        </p>
      )}

      {items.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Categoria</th>
              <th>Descrição</th>
              <th>Data</th>
              <th style={{ textAlign: 'right' }}>Valor</th>
              <th style={{ textAlign: 'right' }}>Ações</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const category = categoryById.get(item.categoryId)
              return (
                <tr
                  key={item.id}
                  onClick={() => navigate(`/expenses/${item.id}`)}
                  style={{ cursor: 'pointer' }}
                >
                  <td>
                    {category ? (
                      <span className="tag tag-neutral" style={{ color: category.cor }}>
                        {category.nome}
                      </span>
                    ) : (
                      <span style={{ opacity: 0.6 }}>Categoria não encontrada</span>
                    )}
                  </td>
                  <td>{item.description}</td>
                  <td style={{ opacity: 0.65 }}>{item.expenseDate}</td>
                  <td style={{ textAlign: 'right', fontWeight: 600, color: 'var(--color-accent-700)' }}>
                    {formatCentsToCurrency(item.amountInCents)}
                  </td>
                  <td style={{ textAlign: 'right' }}>
                    <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 'var(--space-2)' }}>
                      <button
                        type="button"
                        className="btn"
                        aria-label="Editar despesa"
                        onClick={(event) => {
                          event.stopPropagation()
                          onEdit(item)
                        }}
                      >
                        <Pencil size={16} />
                      </button>
                      <button
                        type="button"
                        className="btn"
                        aria-label="Excluir despesa"
                        onClick={(event) => {
                          event.stopPropagation()
                          setDeleteTarget(item)
                        }}
                      >
                        <Trash2 size={16} />
                      </button>
                    </div>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      {hasMore && (
        <button type="button" className="btn btn-secondary" onClick={onLoadMore} disabled={isLoadingMore}>
          {isLoadingMore ? 'Carregando...' : 'Carregar mais'}
        </button>
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
