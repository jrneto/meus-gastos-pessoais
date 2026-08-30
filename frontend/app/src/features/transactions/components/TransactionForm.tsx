import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import { Link } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { useCategories } from '@/lib/categories/useCategories'
import { NotFoundError } from '../errors/transactionErrors'
import { useRegisterTransaction } from '../hooks/useRegisterTransaction'
import { useUpdateTransaction } from '../hooks/useUpdateTransaction'
import {
  transactionSchema,
  type TransactionFormInput,
  type TransactionFormOutput,
} from '../schemas/transactionSchema'

interface TransactionFormProps {
  mode?: 'create' | 'edit'
  transactionId?: string
  initialValues?: TransactionFormInput
  onSuccess?: () => void
  onCancel?: () => void
}

// Popup único de cadastro/edição de despesa (FEAT-17/FEAT-18), com
// campos próprios do Modernist — `ExpenseFormFields`/`EditExpenseForm`
// (shadcn/ui) foram removidos, sem mais consumidores. Nesta feature
// (FEAT-23) o cadastro/edição continua restrito a despesa — o
// seletor de tipo entra na FEAT-24.
export function TransactionForm({
  mode = 'create',
  transactionId,
  initialValues,
  onSuccess,
  onCancel,
}: TransactionFormProps) {
  const registerHook = useRegisterTransaction()
  const updateHook = useUpdateTransaction(transactionId ?? '')
  const { isLoading, error, success } = mode === 'edit' ? updateHook : registerHook
  const submit = mode === 'edit' ? updateHook.updateTransaction : registerHook.registerTransaction
  const { items: categories, isLoading: categoriesLoading } = useCategories()
  // Nesta feature só é possível lançar/editar despesa — o dropdown
  // não pode oferecer categoria de receita, já que o backend rejeita
  // (400) uma transação cujo tipo diverge do tipo da categoria (FEAT-22
  // do backend).
  const expenseCategories = categories.filter((category) => category.tipo === 'despesa')
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors },
  } = useForm<TransactionFormInput, unknown, TransactionFormOutput>({
    resolver: zodResolver(transactionSchema),
    defaultValues: initialValues ?? { description: '', amount: '', categoryId: '', date: '' },
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
    // A transação já não existe mais (excluída por outra sessão entre
    // abrir o popup e salvar) — trata como sucesso silencioso, sem
    // exibir erro (mesmo espírito do tratamento já usado em
    // TransactionDeleteDialog para NotFoundError).
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

  if (expenseCategories.length === 0) {
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
          Você ainda não tem nenhuma categoria de despesa cadastrada.
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
          {expenseCategories.map((category) => (
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
        <input className="input" type="date" aria-invalid={!!errors.date} {...register('date')} />
        {errors.date && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.date.message}
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
