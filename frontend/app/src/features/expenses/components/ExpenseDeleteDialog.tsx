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
import type { ExpenseQueryItem } from '../api/expensesApi'
import { NotFoundError } from '../errors/expenseErrors'
import { useDeleteExpense } from '../hooks/useDeleteExpense'

interface ExpenseDeleteDialogProps {
  expense: ExpenseQueryItem | null
  onOpenChange: (open: boolean) => void
  onDeleted: (id: string) => void
}

export function ExpenseDeleteDialog({ expense, onOpenChange, onDeleted }: ExpenseDeleteDialogProps) {
  const { deleteExpense, isLoading, error, success } = useDeleteExpense()

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

  const otherError = error && !(error instanceof NotFoundError) ? error : null

  return (
    <AlertDialog open={expense !== null} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>Excluir despesa</AlertDialogTitle>
          <AlertDialogDescription>
            Tem certeza que deseja excluir "{expense?.description}"? Essa ação não pode ser
            desfeita.
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
            onClick={() => expense && deleteExpense(expense.id)}
          >
            {isLoading ? 'Excluindo...' : 'Excluir'}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  )
}
