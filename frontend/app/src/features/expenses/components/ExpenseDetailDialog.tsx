import '@/styles/modernist/modernist.css'
import { useEffect } from 'react'
import { CategoryLetterTile } from '@/lib/categories/CategoryLetterTile'
import { useCategories } from '@/lib/categories/useCategories'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { formatCentsToCurrency } from '../utils/currency'

interface ExpenseDetailDialogProps {
  expense: ExpenseQueryItem | null
  onOpenChange: (open: boolean) => void
  onEdit: (expense: ExpenseQueryItem) => void
  onDelete: (expense: ExpenseQueryItem) => void
}

// Popup "Detalhe da despesa" (FEAT-20) — sem chamada à API, usa o item
// já carregado na listagem. Só orquestra os popups de editar
// (`ExpenseFormDialog`) e excluir (`ExpenseDeleteDialog`) já
// existentes, sem duplicar lógica de formulário/exclusão.
export function ExpenseDetailDialog({ expense, onOpenChange, onEdit, onDelete }: ExpenseDetailDialogProps) {
  const { items: categories } = useCategories()
  const open = expense !== null

  useEffect(() => {
    if (!open) return undefined

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onOpenChange(false)
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onOpenChange])

  if (!open) {
    return null
  }

  const category = categories.find((c) => c.id === expense.categoryId)

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="expense-detail-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="expense-detail-title">
          Detalhe da despesa
        </div>

        <div>
          <div style={{ fontSize: '30px', fontWeight: 800, fontFamily: 'var(--font-heading)', color: 'var(--color-accent-700)' }}>
            {formatCentsToCurrency(expense.amountInCents)}
          </div>
          <div style={{ fontSize: '13px', opacity: 0.6 }}>{expense.expenseDate}</div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
          {category ? (
            <>
              <CategoryLetterTile name={category.nome} />
              <span style={{ fontSize: '14px' }}>{category.nome}</span>
            </>
          ) : (
            <span style={{ fontSize: '14px', opacity: 0.6 }}>Categoria não encontrada</span>
          )}
        </div>

        <div>
          <div style={{ fontSize: '12px', opacity: 0.6, marginBottom: '4px' }}>Descrição</div>
          <div style={{ fontSize: '14px' }}>{expense.description}</div>
        </div>

        <div className="dialog-actions" style={{ justifyContent: 'space-between' }}>
          <button
            type="button"
            className="btn btn-ghost"
            style={{ color: 'var(--color-accent-700)' }}
            onClick={() => {
              onDelete(expense)
              onOpenChange(false)
            }}
          >
            Excluir
          </button>
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                onEdit(expense)
                onOpenChange(false)
              }}
            >
              Editar
            </button>
            <button type="button" className="btn btn-primary" onClick={() => onOpenChange(false)}>
              Fechar
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
