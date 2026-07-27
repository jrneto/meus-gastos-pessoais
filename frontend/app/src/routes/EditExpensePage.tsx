import { useParams } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { EditExpenseForm } from '@/features/expenses/components/EditExpenseForm'
import { ExpenseNotFound } from '@/features/expenses/components/ExpenseNotFound'
import { NotFoundError } from '@/features/expenses/errors/expenseErrors'
import { useExpense } from '@/features/expenses/hooks/useExpense'

export function EditExpensePage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, error } = useExpense(id ?? '')

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Editar despesa</h1>

      {isLoading && <p className="text-sm text-muted-foreground">Carregando...</p>}

      {!isLoading && error instanceof NotFoundError && <ExpenseNotFound />}

      {!isLoading && error && !(error instanceof NotFoundError) && (
        <Alert variant="destructive" className="w-full max-w-sm">
          <AlertTitle>Não foi possível carregar a despesa</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {!isLoading && data && <EditExpenseForm expense={data} />}
    </div>
  )
}
