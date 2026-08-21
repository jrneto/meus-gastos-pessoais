import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import { useCategories } from '@/lib/categories/useCategories'
import type { ExpenseDetail } from '../api/expensesApi'
import { NotFoundError } from '../errors/expenseErrors'
import { centsToAmountInput } from '../utils/currency'
import { useUpdateExpense } from '../hooks/useUpdateExpense'
import {
  expenseSchema,
  type ExpenseFormInput,
  type ExpenseFormOutput,
} from '../schemas/expenseSchema'
import { ExpenseFormFields } from './ExpenseFormFields'
import { ExpenseNotFound } from './ExpenseNotFound'

interface EditExpenseFormProps {
  expense: ExpenseDetail
}

export function EditExpenseForm({ expense }: EditExpenseFormProps) {
  const { updateExpense, isLoading, error, success } = useUpdateExpense(expense.id)
  const { items: categories, isLoading: categoriesLoading } = useCategories()
  const navigate = useNavigate()
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<ExpenseFormInput, unknown, ExpenseFormOutput>({
    resolver: zodResolver(expenseSchema),
    defaultValues: {
      description: expense.description,
      amount: centsToAmountInput(expense.amountInCents),
      categoryId: expense.categoryId,
      expenseDate: expense.expenseDate,
    },
  })

  useEffect(() => {
    if (success) {
      navigate('/expenses')
    }
  }, [success, navigate])

  if (error instanceof NotFoundError) {
    return <ExpenseNotFound />
  }

  if (!categoriesLoading && categories.length === 0) {
    return (
      <div className="flex w-full max-w-sm flex-col items-center gap-4 py-8 text-center">
        <p className="text-sm text-muted-foreground">
          Você ainda não tem nenhuma categoria cadastrada.
        </p>
        <Link to="/categories/new" className={cn(buttonVariants({}))}>
          Criar categoria
        </Link>
      </div>
    )
  }

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={handleSubmit((data) => updateExpense(data))}
    >
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível salvar</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      <ExpenseFormFields register={register} control={control} errors={errors} categories={categories} />

      <div className="flex gap-2">
        <Button type="submit" disabled={isLoading}>
          {isLoading ? 'Salvando...' : 'Salvar'}
        </Button>
        <Link to="/expenses" className={cn(buttonVariants({ variant: 'outline' }))}>
          Cancelar
        </Link>
      </div>
    </form>
  )
}
