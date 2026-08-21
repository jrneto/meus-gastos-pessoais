import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button, buttonVariants } from '@/components/ui/button'
import type { CategoryItem } from '@/lib/categories/types'
import { useUpdateCategory } from '../hooks/useUpdateCategory'
import { NameConflictError } from '../errors/categoryErrors'
import {
  categorySchema,
  type CategoryFormInput,
  type CategoryFormOutput,
} from '../schemas/categorySchema'
import { CategoryFormFields } from './CategoryFormFields'

interface EditCategoryFormProps {
  category: CategoryItem
}

export function EditCategoryForm({ category }: EditCategoryFormProps) {
  const navigate = useNavigate()
  const { updateCategory, isLoading, error, success } = useUpdateCategory(category.id)
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<CategoryFormInput, unknown, CategoryFormOutput>({
    resolver: zodResolver(categorySchema),
    defaultValues: { nome: category.nome, cor: category.cor, icone: category.icone },
  })

  useEffect(() => {
    if (success) {
      navigate('/categories')
    }
  }, [success, navigate])

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
      onSubmit={handleSubmit((data) => updateCategory(data))}
    >
      {genericError && (
        <Alert variant="destructive">
          <AlertTitle>Não foi possível salvar</AlertTitle>
          <AlertDescription>{genericError.message}</AlertDescription>
        </Alert>
      )}

      <CategoryFormFields register={register} control={control} errors={errors} />

      <div className="flex gap-2">
        <Button type="submit" disabled={isLoading}>
          {isLoading ? 'Salvando...' : 'Salvar'}
        </Button>
        <Link to="/categories" className={buttonVariants({ variant: 'outline' })}>
          Cancelar
        </Link>
      </div>
    </form>
  )
}
