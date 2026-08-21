import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import type { ExpenseQueryItem } from '../api/expensesApi'
import { NotFoundError } from '../errors/expenseErrors'
import { useDeleteExpense } from '../hooks/useDeleteExpense'

interface ExpenseDeleteDialogProps {
  expense: ExpenseQueryItem | null
  onOpenChange: (open: boolean) => void
  onDeleted: (id: string) => void
}

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist) no lugar do
// `AlertDialog` do shadcn/ui — mesmo padrão do `NavMoreSheet` (FEAT-15),
// com `role="alertdialog"` por se tratar de confirmação destrutiva.
export function ExpenseDeleteDialog({ expense, onOpenChange, onDeleted }: ExpenseDeleteDialogProps) {
  const { deleteExpense, isLoading, error, success } = useDeleteExpense()
  const open = expense !== null

  useEffect(() => {
    if (success && expense) {
      onDeleted(expense.id)
    }
  }, [success, expense, onDeleted])

  useEffect(() => {
    if (error instanceof NotFoundError && expense) {
      onDeleted(expense.id)
    }
  }, [error, expense, onDeleted])

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

  const otherError = error && !(error instanceof NotFoundError) ? error : null

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="alertdialog"
        aria-modal="true"
        aria-labelledby="delete-expense-title"
        aria-describedby="delete-expense-description"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="delete-expense-title">
          Excluir despesa
        </div>
        <p className="dialog-body" id="delete-expense-description">
          Tem certeza que deseja excluir "{expense?.description}"? Essa ação não pode ser
          desfeita.
        </p>

        {otherError && (
          <div style={{ color: 'var(--color-accent-700)' }}>
            <div style={{ fontWeight: 700 }}>Não foi possível excluir</div>
            <div style={{ fontSize: '13px' }}>{otherError.message}</div>
          </div>
        )}

        <div className="dialog-actions">
          <button type="button" className="btn btn-secondary" onClick={() => onOpenChange(false)}>
            Cancelar
          </button>
          <button
            type="button"
            className="btn btn-primary"
            disabled={isLoading}
            onClick={() => expense && deleteExpense(expense.id)}
          >
            {isLoading ? 'Excluindo...' : 'Excluir'}
          </button>
        </div>
      </div>
    </div>
  )
}
