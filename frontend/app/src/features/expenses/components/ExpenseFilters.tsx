import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import { Button } from '@/components/ui/button'
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
import {
  expenseFilterSchema,
  type ExpenseFilterInput,
  type ExpenseFilterOutput,
} from '../schemas/expenseFilterSchema'

interface ExpenseFiltersProps {
  onApply: (filters: ExpenseFilterOutput) => void
}

export function ExpenseFilters({ onApply }: ExpenseFiltersProps) {
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<ExpenseFilterInput, unknown, ExpenseFilterOutput>({
    resolver: zodResolver(expenseFilterSchema),
    defaultValues: {
      yearMonth: '',
      category: '',
      dateFrom: '',
      dateTo: '',
      minAmount: '',
      maxAmount: '',
    },
  })

  return (
    <form
      className="flex w-full max-w-sm flex-col gap-4"
      noValidate
      onSubmit={handleSubmit((data) => onApply(data))}
    >
      <div className="flex flex-col gap-1.5">
        <Label htmlFor="yearMonth">Mês</Label>
        <Input id="yearMonth" type="month" {...register('yearMonth')} />
      </div>

      <div className="flex flex-col gap-1.5">
        <Label htmlFor="category">Categoria</Label>
        <Controller
          control={control}
          name="category"
          render={({ field }) => (
            <Select value={field.value ?? ''} onValueChange={field.onChange}>
              <SelectTrigger id="category" className="w-full">
                <SelectValue placeholder="Todas">
                  {(value: string) =>
                    EXPENSE_CATEGORIES.find((category) => category.value === value)?.label ??
                    'Todas'
                  }
                </SelectValue>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="">Todas</SelectItem>
                {EXPENSE_CATEGORIES.map((category) => (
                  <SelectItem key={category.value} value={category.value}>
                    {category.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
      </div>

      <div className="flex gap-2">
        <div className="flex flex-1 flex-col gap-1.5">
          <Label htmlFor="dateFrom">De</Label>
          <Input id="dateFrom" type="date" {...register('dateFrom')} />
        </div>
        <div className="flex flex-1 flex-col gap-1.5">
          <Label htmlFor="dateTo">Até</Label>
          <Input
            id="dateTo"
            type="date"
            aria-invalid={!!errors.dateTo}
            {...register('dateTo')}
          />
          {errors.dateTo && (
            <p className="text-sm text-destructive" role="alert">
              {errors.dateTo.message}
            </p>
          )}
        </div>
      </div>

      <div className="flex gap-2">
        <div className="flex flex-1 flex-col gap-1.5">
          <Label htmlFor="minAmount">Valor mín.</Label>
          <Input
            id="minAmount"
            inputMode="decimal"
            placeholder="0,00"
            aria-invalid={!!errors.minAmount}
            {...register('minAmount')}
          />
          {errors.minAmount && (
            <p className="text-sm text-destructive" role="alert">
              {errors.minAmount.message}
            </p>
          )}
        </div>
        <div className="flex flex-1 flex-col gap-1.5">
          <Label htmlFor="maxAmount">Valor máx.</Label>
          <Input
            id="maxAmount"
            inputMode="decimal"
            placeholder="0,00"
            aria-invalid={!!errors.maxAmount}
            {...register('maxAmount')}
          />
          {errors.maxAmount && (
            <p className="text-sm text-destructive" role="alert">
              {errors.maxAmount.message}
            </p>
          )}
        </div>
      </div>

      <Button type="submit">Filtrar</Button>
    </form>
  )
}
