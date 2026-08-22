import { useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { CategoryBadge } from '@/lib/categories/CategoryBadge'
import { useCategories } from '@/lib/categories/useCategories'
import { ExpenseDeleteDialog } from '@/features/expenses/components/ExpenseDeleteDialog'
import { ExpenseNotFound } from '@/features/expenses/components/ExpenseNotFound'
import { NotFoundError } from '@/features/expenses/errors/expenseErrors'
import { useExpense } from '@/features/expenses/hooks/useExpense'
import { formatCentsToCurrency } from '@/features/expenses/utils/currency'

export function ExpenseDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, error } = useExpense(id ?? '')
  const { items: categories } = useCategories()
  const navigate = useNavigate()
  const [confirmingDelete, setConfirmingDelete] = useState(false)

  const category = data ? categories.find((c) => c.id === data.categoryId) : undefined

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Detalhes da despesa</h1>

      {isLoading && <p className="text-sm text-muted-foreground">Carregando...</p>}

      {!isLoading && error instanceof NotFoundError && <ExpenseNotFound />}

      {!isLoading && error && !(error instanceof NotFoundError) && (
        <Alert variant="destructive" className="w-full max-w-sm">
          <AlertTitle>Não foi possível carregar a despesa</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {!isLoading && data && (
        <div className="flex w-full max-w-sm flex-col gap-4">
          <dl className="flex flex-col gap-2 rounded-lg border border-border p-4 text-sm">
            <div className="flex items-baseline justify-between gap-4">
              <dt className="text-muted-foreground">Descrição</dt>
              <dd className="text-right font-medium">{data.description}</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="text-muted-foreground">Valor</dt>
              <dd className="text-right font-medium">
                {formatCentsToCurrency(data.amountInCents)}
              </dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="text-muted-foreground">Categoria</dt>
              <dd className="text-right font-medium">
                <CategoryBadge category={category} />
              </dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="text-muted-foreground">Data da despesa</dt>
              <dd className="text-right font-medium">{data.expenseDate}</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="text-muted-foreground">ID</dt>
              <dd className="text-right font-mono text-xs break-all">{data.id}</dd>
            </div>
            <div className="flex items-baseline justify-between gap-4">
              <dt className="text-muted-foreground">Criado em</dt>
              <dd className="text-right font-medium">
                {new Date(data.createdAt).toLocaleString('pt-BR')}
              </dd>
            </div>
          </dl>

          <div className="flex gap-2">
            <Link to="/expenses" className={cn(buttonVariants({}))}>
              Editar
            </Link>
            <Button variant="destructive" onClick={() => setConfirmingDelete(true)}>
              Excluir
            </Button>
          </div>
        </div>
      )}

      <ExpenseDeleteDialog
        expense={confirmingDelete ? data : null}
        onOpenChange={setConfirmingDelete}
        onDeleted={() => navigate('/expenses')}
      />
    </div>
  )
}
