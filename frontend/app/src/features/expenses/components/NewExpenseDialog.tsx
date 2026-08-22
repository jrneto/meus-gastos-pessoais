import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import { ExpenseForm } from './ExpenseForm'

interface NewExpenseDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onCreated: () => void
}

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist), mesmo
// padrão de `ExpenseDeleteDialog`/`NavMoreSheet` — substitui a antiga
// rota `/expenses/new` (FEAT-17).
export function NewExpenseDialog({ open, onOpenChange, onCreated }: NewExpenseDialogProps) {
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
    onCreated()
    onOpenChange(false)
  }

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="new-expense-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="new-expense-title">
          Nova despesa
        </div>
        <ExpenseForm onSuccess={handleSuccess} onCancel={() => onOpenChange(false)} />
      </div>
    </div>
  )
}
