import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { useRegisterExpense } from '../hooks/useRegisterExpense'
import {
  expenseSchema,
  type ExpenseFormInput,
  type ExpenseFormOutput,
} from '../schemas/expenseSchema'
import { ExpenseFormFields } from './ExpenseFormFields'

export function ExpenseForm() {
  const { registerExpense, isLoading, error, success } = useRegisterExpense()
  const {
    register,
    control,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ExpenseFormInput, unknown, ExpenseFormOutput>({
    resolver: zodResolver(expenseSchema),
    defaultValues: { description: '', amount: '', expenseDate: '' },
  })

  useEffect(() => {
    if (success) {
      reset()
    }
  }, [success, reset])

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={handleSubmit((data) => registerExpense(data))}
    >
      {error && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível registrar</AlertTitle>
          <AlertDescription>{error.message}</AlertDescription>
        </Alert>
      )}

      {success && (
        <Alert>
          <AlertTitle>Despesa registrada</AlertTitle>
          <AlertDescription>Cadastre a próxima despesa abaixo.</AlertDescription>
        </Alert>
      )}

      <ExpenseFormFields register={register} control={control} errors={errors} />

      <Button type="submit" disabled={isLoading}>
        {isLoading ? 'Salvando...' : 'Registrar despesa'}
      </Button>
    </form>
  )
}
