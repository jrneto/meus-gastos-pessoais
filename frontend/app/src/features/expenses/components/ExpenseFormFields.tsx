import type { Control, FieldErrors, UseFormRegister } from 'react-hook-form'
import { Controller } from 'react-hook-form'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select'
import type { CategoryItem } from '@/lib/categories/types'
import type { ExpenseFormInput, ExpenseFormOutput } from '../schemas/expenseSchema'

interface ExpenseFormFieldsProps {
  register: UseFormRegister<ExpenseFormInput>
  control: Control<ExpenseFormInput, unknown, ExpenseFormOutput>
  errors: FieldErrors<ExpenseFormInput>
  categories: CategoryItem[]
}

export function ExpenseFormFields({ register, control, errors, categories }: ExpenseFormFieldsProps) {
  return (
    <>
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="description">Descrição</Label>
        <Input
          id="description"
          aria-invalid={!!errors.description}
          {...register('description')}
        />
        {errors.description && (
          <p className="text-sm text-destructive" role="alert">
            {errors.description.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="amount">Valor</Label>
        <Input
          id="amount"
          inputMode="decimal"
          placeholder="0,00"
          aria-invalid={!!errors.amount}
          {...register('amount')}
        />
        {errors.amount && (
          <p className="text-sm text-destructive" role="alert">
            {errors.amount.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="categoryId">Categoria</Label>
        <Controller
          control={control}
          name="categoryId"
          render={({ field }) => (
            <Select value={field.value ?? ''} onValueChange={field.onChange}>
              <SelectTrigger id="categoryId" aria-invalid={!!errors.categoryId} className="w-full">
                <SelectValue placeholder="Selecione uma categoria">
                  {(value: string) =>
                    categories.find((category) => category.id === value)?.nome ?? ''
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {categories.map((category) => (
                  <SelectItem key={category.id} value={category.id}>
                    {category.nome}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
        {errors.categoryId && (
          <p className="text-sm text-destructive" role="alert">
            {errors.categoryId.message}
          </p>
        )}
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="expenseDate">Data</Label>
        <Input
          id="expenseDate"
          type="date"
          aria-invalid={!!errors.expenseDate}
          {...register('expenseDate')}
        />
        {errors.expenseDate && (
          <p className="text-sm text-destructive" role="alert">
            {errors.expenseDate.message}
          </p>
        )}
      </div>
    </>
  )
}
