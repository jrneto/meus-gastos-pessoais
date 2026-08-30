import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import type { TransactionQueryItem } from '../api/transactionsApi'
import { NotFoundError } from '../errors/transactionErrors'
import { useDeleteTransaction } from '../hooks/useDeleteTransaction'

interface TransactionDeleteDialogProps {
  transaction: TransactionQueryItem | null
  onOpenChange: (open: boolean) => void
  onDeleted: (id: string) => void
}

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist) no lugar do
// `AlertDialog` do shadcn/ui — mesmo padrão do `NavMoreSheet` (FEAT-15),
// com `role="alertdialog"` por se tratar de confirmação destrutiva.
export function TransactionDeleteDialog({ transaction, onOpenChange, onDeleted }: TransactionDeleteDialogProps) {
  const { deleteTransaction, isLoading, error, success } = useDeleteTransaction()
  const open = transaction !== null

  useEffect(() => {
    if (success && transaction) {
      onDeleted(transaction.id)
    }
  }, [success, transaction, onDeleted])

  useEffect(() => {
    if (error instanceof NotFoundError && transaction) {
      onDeleted(transaction.id)
    }
  }, [error, transaction, onDeleted])

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
        aria-labelledby="delete-transaction-title"
        aria-describedby="delete-transaction-description"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="delete-transaction-title">
          Excluir despesa
        </div>
        <p className="dialog-body" id="delete-transaction-description">
          Tem certeza que deseja excluir "{transaction?.description}"? Essa ação não pode ser
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
            onClick={() => transaction && deleteTransaction(transaction.id)}
          >
            {isLoading ? 'Excluindo...' : 'Excluir'}
          </button>
        </div>
      </div>
    </div>
  )
}
