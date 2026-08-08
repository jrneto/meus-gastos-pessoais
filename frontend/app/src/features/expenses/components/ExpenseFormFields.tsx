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
import { EXPENSE_CATEGORIES } from '../constants/expenseCategories'
import type { ExpenseFormInput, ExpenseFormOutput } from '../schemas/expenseSchema'

interface ExpenseFormFieldsProps {
  register: UseFormRegister<ExpenseFormInput>
  control: Control<ExpenseFormInput, unknown, ExpenseFormOutput>
  errors: FieldErrors<ExpenseFormInput>
}

export function ExpenseFormFields({ register, control, errors }: ExpenseFormFieldsProps) {
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
        <Label htmlFor="category">Categoria</Label>
        <Controller
          control={control}
          name="category"
          render={({ field }) => (
            <Select value={field.value ?? ''} onValueChange={field.onChange}>
              <SelectTrigger id="category" aria-invalid={!!errors.category} className="w-full">
                <SelectValue placeholder="Selecione uma categoria">
                  {(value: string) =>
                    EXPENSE_CATEGORIES.find((category) => category.value === value)?.label ?? ''
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                {EXPENSE_CATEGORIES.map((category) => (
                  <SelectItem key={category.value} value={category.value}>
                    {category.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
        {errors.category && (
          <p className="text-sm text-destructive" role="alert">
            {errors.category.message}
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
