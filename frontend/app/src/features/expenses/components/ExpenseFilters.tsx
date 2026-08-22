import { zodResolver } from '@hookform/resolvers/zod'
import { useState } from 'react'
import { useForm } from 'react-hook-form'
import '@/styles/modernist/modernist.css'
import { useCategories } from '@/lib/categories/useCategories'
import {
  expenseFilterSchema,
  type ExpenseFilterInput,
  type ExpenseFilterOutput,
} from '../schemas/expenseFilterSchema'

interface ExpenseFiltersProps {
  onApply: (filters: ExpenseFilterOutput) => void
}

const ADVANCED_FIELD_NAMES = ['yearMonth', 'dateFrom', 'dateTo', 'minAmount', 'maxAmount'] as const

export function ExpenseFilters({ onApply }: ExpenseFiltersProps) {
  const { items: categories } = useCategories()
  const [advancedOpen, setAdvancedOpen] = useState(false)
  const {
    register,
    handleSubmit,
    setValue,
    reset,
    watch,
    formState: { errors },
  } = useForm<ExpenseFilterInput, unknown, ExpenseFilterOutput>({
    resolver: zodResolver(expenseFilterSchema),
    defaultValues: {
      yearMonth: '',
      categoryId: '',
      dateFrom: '',
      dateTo: '',
      minAmount: '',
      maxAmount: '',
    },
  })

  const categoryId = watch('categoryId')
  const advancedValues = watch(ADVANCED_FIELD_NAMES)
  const hasActiveAdvancedFilters = advancedValues.some((value) => !!value)

  const submit = handleSubmit((data) => onApply(data))

  function toggleCategory(id: string) {
    setValue('categoryId', categoryId === id ? '' : id)
    void submit()
  }

  function clearAdvancedFilters() {
    reset({ yearMonth: '', categoryId, dateFrom: '', dateTo: '', minAmount: '', maxAmount: '' })
    void submit()
  }

  return (
    <form
      className="ds-modernist"
      noValidate
      onSubmit={submit}
      style={{ display: 'flex', width: '100%', flexDirection: 'column', gap: 'var(--space-4)' }}
    >
      <div style={{ display: 'flex', gap: 'var(--space-2)', flexWrap: 'wrap', alignItems: 'center' }}>
        {categories.map((category) => (
          <button
            key={category.id}
            type="button"
            className="tag"
            aria-pressed={categoryId === category.id}
            onClick={() => toggleCategory(category.id)}
          >
            {category.nome}
          </button>
        ))}

        <button
          type="button"
          className="btn btn-secondary"
          style={{ position: 'relative' }}
          aria-expanded={advancedOpen}
          onClick={() => setAdvancedOpen((open) => !open)}
        >
          Filtros avançados
          {hasActiveAdvancedFilters && (
            <span
              aria-hidden="true"
              style={{
                position: 'absolute',
                top: '6px',
                right: '8px',
                width: '7px',
                height: '7px',
                borderRadius: '50%',
                background: 'var(--color-accent)',
              }}
            />
          )}
        </button>
      </div>

      {advancedOpen && (
        <div
          style={{
            border: '1px solid var(--color-divider)',
            padding: 'var(--space-4)',
            display: 'flex',
            gap: 'var(--space-4)',
            alignItems: 'flex-end',
            flexWrap: 'wrap',
          }}
        >
          <label className="field" style={{ minWidth: '160px' }}>
            <span>Mês</span>
            <input className="input" type="month" {...register('yearMonth')} />
          </label>

          <label className="field" style={{ minWidth: '160px' }}>
            <span>De</span>
            <input className="input" type="date" {...register('dateFrom')} />
          </label>

          <label className="field" style={{ minWidth: '160px' }}>
            <span>Até</span>
            <input className="input" type="date" {...register('dateTo')} />
            {errors.dateTo && (
              <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
                {errors.dateTo.message}
              </p>
            )}
          </label>

          <label className="field" style={{ minWidth: '140px' }}>
            <span>Valor mín.</span>
            <input className="input" inputMode="decimal" placeholder="0,00" {...register('minAmount')} />
            {errors.minAmount && (
              <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
                {errors.minAmount.message}
              </p>
            )}
          </label>

          <label className="field" style={{ minWidth: '140px' }}>
            <span>Valor máx.</span>
            <input className="input" inputMode="decimal" placeholder="0,00" {...register('maxAmount')} />
            {errors.maxAmount && (
              <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
                {errors.maxAmount.message}
              </p>
            )}
          </label>

          <button type="submit" className="btn btn-primary">
            Filtrar
          </button>
          <button type="button" className="btn btn-ghost" onClick={clearAdvancedFilters}>
            Limpar filtros
          </button>
        </div>
      )}
    </form>
  )
}
