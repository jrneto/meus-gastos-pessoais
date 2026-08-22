import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import { NotFoundError } from '../errors/expenseErrors'
import { useExpense } from '../hooks/useExpense'
import { centsToAmountInput } from '../utils/currency'
import { ExpenseForm } from './ExpenseForm'

interface ExpenseFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: () => void
  expenseId?: string
}

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist), mesmo
// padrão de `ExpenseDeleteDialog`/`NavMoreSheet` — substitui as antigas
// rotas `/expenses/new` (FEAT-17) e `/expenses/:id/edit` (FEAT-18),
// unificando cadastro e edição no mesmo popup.
export function ExpenseFormDialog({ open, onOpenChange, onSaved, expenseId }: ExpenseFormDialogProps) {
  const isEdit = !!expenseId
  const { data, isLoading, error } = useExpense(expenseId ?? '')

  useEffect(() => {
    // A despesa não existe mais (excluída por outra sessão) — fecha o
    // popup silenciosamente e atualiza a listagem, sem exibir erro.
    if (open && isEdit && error instanceof NotFoundError) {
      onSaved()
      onOpenChange(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, isEdit, error])

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

  function handleSuccess() {
    onSaved()
    onOpenChange(false)
  }

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="expense-form-dialog-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="expense-form-dialog-title">
          {isEdit ? 'Editar despesa' : 'Nova despesa'}
        </div>

        {isEdit && isLoading && <p>Carregando...</p>}

        {isEdit && !isLoading && data && (
          <ExpenseForm
            mode="edit"
            expenseId={data.id}
            initialValues={{
              description: data.description,
              amount: centsToAmountInput(data.amountInCents),
              categoryId: data.categoryId,
              expenseDate: data.expenseDate,
            }}
            onSuccess={handleSuccess}
            onCancel={() => onOpenChange(false)}
          />
        )}

        {!isEdit && <ExpenseForm mode="create" onSuccess={handleSuccess} onCancel={() => onOpenChange(false)} />}
      </div>
    </div>
  )
}
