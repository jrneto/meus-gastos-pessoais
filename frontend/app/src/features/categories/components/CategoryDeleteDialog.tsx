import { useEffect } from 'react'
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import type { CategoryItem } from '@/lib/categories/types'
import { NotFoundError } from '../errors/categoryErrors'
import { useDeleteCategory } from '../hooks/useDeleteCategory'

interface CategoryDeleteDialogProps {
  category: CategoryItem | null
  onOpenChange: (open: boolean) => void
  onDeleted: (id: string) => void
}

export function CategoryDeleteDialog({
  category,
  onOpenChange,
  onDeleted,
}: CategoryDeleteDialogProps) {
  const { deleteCategory, isLoading, error, success } = useDeleteCategory()

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

  const otherError = error && !(error instanceof NotFoundError) ? error : null

  return (
    <AlertDialog open={category !== null} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Excluir categoria</AlertDialogTitle>
          <AlertDialogDescription>
            Tem certeza que deseja excluir "{category?.nome}"? Essa ação não pode ser desfeita.
          </AlertDialogDescription>
        </AlertDialogHeader>

        {otherError && (
          <Alert variant="destructive">
            <AlertTitle>Não foi possível excluir</AlertTitle>
            <AlertDescription>{otherError.message}</AlertDescription>
          </Alert>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel>Cancelar</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            disabled={isLoading}
            onClick={() => category && deleteCategory(category.id)}
          >
            {isLoading ? 'Excluindo...' : 'Excluir'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
