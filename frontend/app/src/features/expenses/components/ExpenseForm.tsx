import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { useCategories } from '@/lib/categories/useCategories'
import { useRegisterExpense } from '../hooks/useRegisterExpense'
import {
  expenseSchema,
  type ExpenseFormInput,
  type ExpenseFormOutput,
} from '../schemas/expenseSchema'

interface ExpenseFormProps {
  onSuccess?: () => void
  onCancel?: () => void
}

// Campos próprios do Modernist, não compartilhados com
// `ExpenseFormFields` (usado por `EditExpenseForm`, fora do escopo da
// FEAT-17 — continua shadcn/ui até sua própria spec de migração).
export function ExpenseForm({ onSuccess, onCancel }: ExpenseFormProps) {
  const { registerExpense, isLoading, error, success } = useRegisterExpense()
  const { items: categories, isLoading: categoriesLoading } = useCategories()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ExpenseFormInput, unknown, ExpenseFormOutput>({
    resolver: zodResolver(expenseSchema),
    defaultValues: { description: '', amount: '', categoryId: '', expenseDate: '' },
  })

  useEffect(() => {
    if (success) {
      reset()
      onSuccess?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [success, reset])

  if (!categoriesLoading && categories.length === 0) {
    return (
      <div
        className="ds-modernist"
        style={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          gap: 'var(--space-4)',
          padding: 'var(--space-8) 0',
          textAlign: 'center',
        }}
      >
        <p style={{ opacity: 0.7, fontSize: '14px' }}>
          Você ainda não tem nenhuma categoria cadastrada.
        </p>
        <Link to="/categories/new" className="btn btn-primary">
          Criar categoria
        </Link>
      </div>
    )
  }

  return (
    <form
      className="ds-modernist"
      noValidate
      onSubmit={handleSubmit((data) => registerExpense(data))}
      style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}
    >
      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível registrar</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      <label className="field">
        <span>Descrição</span>
        <input className="input" aria-invalid={!!errors.description} {...register('description')} />
        {errors.description && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.description.message}
          </p>
        )}
      </label>

      <label className="field">
        <span>Valor</span>
        <input
          className="input"
          inputMode="decimal"
          placeholder="0,00"
          aria-invalid={!!errors.amount}
          {...register('amount')}
        />
        {errors.amount && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.amount.message}
          </p>
        )}
      </label>

      <label className="field">
        <span>Categoria</span>
        <select className="input" aria-invalid={!!errors.categoryId} {...register('categoryId')}>
          <option value="">Selecione uma categoria</option>
          {categories.map((category) => (
            <option key={category.id} value={category.id}>
              {category.nome}
            </option>
          ))}
        </select>
        {errors.categoryId && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.categoryId.message}
          </p>
        )}
      </label>

      <label className="field">
        <span>Data</span>
        <input className="input" type="date" aria-invalid={!!errors.expenseDate} {...register('expenseDate')} />
        {errors.expenseDate && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.expenseDate.message}
          </p>
        )}
      </label>

      <div className="dialog-actions">
        {onCancel && (
          <button type="button" className="btn btn-secondary" onClick={onCancel}>
            Cancelar
          </button>
        )}
        <button type="submit" className="btn btn-primary" disabled={isLoading}>
          {isLoading ? 'Salvando...' : 'Registrar despesa'}
        </button>
      </div>
    </form>
  )
}
