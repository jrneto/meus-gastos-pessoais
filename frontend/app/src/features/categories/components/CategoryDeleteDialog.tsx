import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import type { CategoryItem } from '@/lib/categories/types'
import { NotFoundError } from '../errors/categoryErrors'
import { useDeleteCategory } from '../hooks/useDeleteCategory'

interface CategoryDeleteDialogProps {
  category: CategoryItem | null
  onOpenChange: (open: boolean) => void
  onDeleted: (id: string) => void
}

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist), mesmo
// padrão de `ExpenseDeleteDialog` (FEAT-16), no lugar do `AlertDialog`
// do shadcn/ui.
export function CategoryDeleteDialog({ category, onOpenChange, onDeleted }: CategoryDeleteDialogProps) {
  const { deleteCategory, isLoading, error, success } = useDeleteCategory()
  const open = category !== null

  useEffect(() => {
    if (success && category) {
      onDeleted(category.id)
    }
  }, [success, category, onDeleted])

  useEffect(() => {
    if (error instanceof NotFoundError && category) {
      onDeleted(category.id)
    }
  }, [error, category, onDeleted])

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
        aria-labelledby="delete-category-title"
        aria-describedby="delete-category-description"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="delete-category-title">
          Excluir categoria
        </div>
        <p className="dialog-body" id="delete-category-description">
          Tem certeza que deseja excluir "{category?.nome}"? Essa ação não pode ser desfeita.
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
            onClick={() => category && deleteCategory(category.id)}
          >
            {isLoading ? 'Excluindo...' : 'Excluir'}
          </button>
        </div>
      </div>
    </div>
  )
}
