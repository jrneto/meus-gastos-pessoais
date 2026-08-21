import type { Control, FieldErrors, UseFormRegister } from 'react-hook-form'
import { Controller, useWatch } from 'react-hook-form'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { CategoryFormInput, CategoryFormOutput } from '../schemas/categorySchema'
import { IconPicker } from './IconPicker'

interface CategoryFormFieldsProps {
  register: UseFormRegister<CategoryFormInput>
  control: Control<CategoryFormInput, unknown, CategoryFormOutput>
  errors: FieldErrors<CategoryFormInput>
}

export function CategoryFormFields({ register, control, errors }: CategoryFormFieldsProps) {
  const cor = useWatch({ control, name: 'cor' })

  return (
    <>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="nome">Nome</Label>
        <Input id="nome" aria-invalid={!!errors.nome} {...register('nome')} />
        {errors.nome && (
          <p className="text-sm text-destructive" role="alert">
            {errors.nome.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="cor">Cor</Label>
        <div className="flex items-center gap-2">
          <input
            id="cor"
            type="color"
            aria-invalid={!!errors.cor}
            className="h-8 w-12 cursor-pointer rounded-lg border border-input bg-transparent p-0.5"
            {...register('cor')}
          />
          <span className="text-sm text-muted-foreground">{cor}</span>
        </div>
        {errors.cor && (
          <p className="text-sm text-destructive" role="alert">
            {errors.cor.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="icone">Ícone</Label>
        <Controller
          control={control}
          name="icone"
          render={({ field }) => (
            <IconPicker value={field.value} onChange={field.onChange} error={!!errors.icone} />
          )}
        />
        {errors.icone && (
          <p className="text-sm text-destructive" role="alert">
            {errors.icone.message}
          </p>
        )}
      </div>
    </>
  )
}
