import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { useRegisterCategory } from '../hooks/useRegisterCategory'
import { NameConflictError } from '../errors/categoryErrors'
import {
  categorySchema,
  type CategoryFormInput,
  type CategoryFormOutput,
} from '../schemas/categorySchema'
import { CategoryFormFields } from './CategoryFormFields'

const DEFAULT_COLOR = '#3B82F6'

export function NewCategoryForm() {
  const { registerCategory, isLoading, error, success } = useRegisterCategory()
  const {
    register,
    control,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm<CategoryFormInput, unknown, CategoryFormOutput>({
    resolver: zodResolver(categorySchema),
    defaultValues: { nome: '', cor: DEFAULT_COLOR, icone: undefined },
  })

  useEffect(() => {
    if (success) {
      reset({ nome: '', cor: DEFAULT_COLOR, icone: undefined })
    }
  }, [success, reset])

  useEffect(() => {
    if (error instanceof NameConflictError) {
      setError('nome', { message: error.message })
    }
  }, [error, setError])

  const genericError = error && !(error instanceof NameConflictError) ? error : null

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={handleSubmit((data) => registerCategory(data))}
    >
      {genericError && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível salvar</AlertTitle>
          <AlertDescription>{genericError.message}</AlertDescription>
        </Alert>
      )}

      {success && (
        <Alert>
          <AlertTitle>Categoria criada</AlertTitle>
          <AlertDescription>Cadastre a próxima categoria abaixo.</AlertDescription>
        </Alert>
      )}

      <CategoryFormFields register={register} control={control} errors={errors} />

      <Button type="submit" disabled={isLoading}>
        {isLoading ? 'Salvando...' : 'Criar categoria'}
      </Button>
    </form>
  )
}
