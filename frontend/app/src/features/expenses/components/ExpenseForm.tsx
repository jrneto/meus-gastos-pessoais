import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { useCategories } from '@/lib/categories/useCategories'
import { NotFoundError } from '../errors/expenseErrors'
import { useRegisterExpense } from '../hooks/useRegisterExpense'
import { useUpdateExpense } from '../hooks/useUpdateExpense'
import {
  expenseSchema,
  type ExpenseFormInput,
  type ExpenseFormOutput,
} from '../schemas/expenseSchema'

interface ExpenseFormProps {
  mode?: 'create' | 'edit'
  expenseId?: string
  initialValues?: ExpenseFormInput
  onSuccess?: () => void
  onCancel?: () => void
}

// Popup único de cadastro/edição de despesa (FEAT-17/FEAT-18), com
// campos próprios do Modernist — `ExpenseFormFields`/`EditExpenseForm`
// (shadcn/ui) foram removidos, sem mais consumidores.
export function ExpenseForm({
  mode = 'create',
  expenseId,
  initialValues,
  onSuccess,
  onCancel,
}: ExpenseFormProps) {
  const registerHook = useRegisterExpense()
  const updateHook = useUpdateExpense(expenseId ?? '')
  const { isLoading, error, success } = mode === 'edit' ? updateHook : registerHook
  const submit = mode === 'edit' ? updateHook.updateExpense : registerHook.registerExpense
  const { items: categories, isLoading: categoriesLoading } = useCategories()
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<ExpenseFormInput, unknown, ExpenseFormOutput>({
    resolver: zodResolver(expenseSchema),
    defaultValues: initialValues ?? { description: '', amount: '', categoryId: '', expenseDate: '' },
  })

  useEffect(() => {
    if (success) {
      if (mode === 'create') {
        reset()
      }
      onSuccess?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [success])

  useEffect(() => {
    // A despesa já não existe mais (excluída por outra sessão entre
    // abrir o popup e salvar) — trata como sucesso silencioso, sem
    // exibir erro (mesmo espírito do tratamento já usado em
    // ExpenseDeleteDialog para NotFoundError).
    if (mode === 'edit' && error instanceof NotFoundError) {
      onSuccess?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [error, mode])

  const visibleError = error && !(mode === 'edit' && error instanceof NotFoundError) ? error : null

  if (categoriesLoading) {
    // Só monta o <select> de categoria depois que as opções existem —
    // um <select> nativo com register() do RHF não reaplica o value
    // inicial se a opção correspondente ainda não estiver no DOM.
    return (
      <p className="ds-modernist" style={{ opacity: 0.7, fontSize: '14px' }}>
        Carregando...
      </p>
    )
  }

  if (categories.length === 0) {
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
        <Link to="/categories" className="btn btn-primary">
          Criar categoria
        </Link>
      </div>
    )
  }

  return (
    <form
      className="ds-modernist"
      noValidate
      onSubmit={handleSubmit((data) => submit(data))}
      style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}
    >
      {visibleError && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>
            {mode === 'edit' ? 'Não foi possível salvar' : 'Não foi possível registrar'}
          </div>
          <div style={{ fontSize: '13px' }}>{visibleError.message}</div>
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
          {isLoading ? 'Salvando...' : mode === 'edit' ? 'Salvar alterações' : 'Registrar despesa'}
        </button>
      </div>
    </form>
  )
}
