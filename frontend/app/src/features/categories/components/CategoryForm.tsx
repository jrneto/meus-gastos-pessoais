import { zodResolver } from '@hookform/resolvers/zod'
import { useEffect } from 'react'
import { useForm } from 'react-hook-form'
import '@/styles/modernist/modernist.css'
import type { CategoryItem } from '@/lib/categories/types'
import { NameConflictError, NotFoundError } from '../errors/categoryErrors'
import { useRegisterCategory } from '../hooks/useRegisterCategory'
import { useUpdateCategory } from '../hooks/useUpdateCategory'
import {
  categorySchema,
  type CategoryFormInput,
  type CategoryFormOutput,
} from '../schemas/categorySchema'

const DEFAULT_VALUES: CategoryFormInput = { nome: '', tipo: 'despesa', orcamentoMensal: '' }

interface CategoryFormProps {
  mode?: 'create' | 'edit'
  categoryId?: string
  initialValues?: CategoryFormInput
  onSaved: (category: CategoryItem) => void
  onNotFound?: () => void
  onCancel: () => void
}

export function CategoryForm({
  mode = 'create',
  categoryId,
  initialValues,
  onSaved,
  onNotFound,
  onCancel,
}: CategoryFormProps) {
  const registerHook = useRegisterCategory()
  const updateHook = useUpdateCategory(categoryId ?? '')
  const { isLoading, error, success, data } = mode === 'edit' ? updateHook : registerHook
  const submit = mode === 'edit' ? updateHook.updateCategory : registerHook.registerCategory
  const {
    register,
    handleSubmit,
    reset,
    resetField,
    watch,
    setError,
    formState: { errors },
  } = useForm<CategoryFormInput, unknown, CategoryFormOutput>({
    resolver: zodResolver(categorySchema),
    defaultValues: initialValues ?? DEFAULT_VALUES,
  })
  const tipo = watch('tipo')
  const tipoField = register('tipo')

  useEffect(() => {
    if (success && data) {
      if (mode === 'create') {
        reset(DEFAULT_VALUES)
      }
      onSaved(data)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [success, data])

  useEffect(() => {
    if (error instanceof NameConflictError) {
      setError('nome', { message: error.message })
    }
    // A categoria já não existe mais (excluída por outra sessão) —
    // fecha silenciosamente, sem exibir erro (mesmo espírito do
    // tratamento já usado para despesas).
    if (mode === 'edit' && error instanceof NotFoundError) {
      onNotFound?.()
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [error, mode])

  const visibleError =
    error && !(error instanceof NameConflictError) && !(mode === 'edit' && error instanceof NotFoundError)
      ? error
      : null

  return (
    <form
      className="ds-modernist"
      noValidate
      onSubmit={handleSubmit((formData) => submit(formData))}
      style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}
    >
      {visibleError && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível salvar</div>
          <div style={{ fontSize: '13px' }}>{visibleError.message}</div>
        </div>
      )}

      <label className="field">
        <span>Nome</span>
        <input className="input" aria-invalid={!!errors.nome} {...register('nome')} />
        {errors.nome && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.nome.message}
          </p>
        )}
      </label>

      <fieldset style={{ border: 'none', padding: 0, margin: 0 }}>
        <legend style={{ fontSize: '12px', opacity: 0.7, padding: 0, marginBottom: 'var(--space-2)' }}>
          Tipo da categoria
        </legend>
        <div className="seg">
          <label className="seg-opt">
            <input type="radio" value="despesa" {...tipoField} />
            Despesa
          </label>
          <label className="seg-opt">
            <input
              type="radio"
              value="receita"
              {...tipoField}
              onChange={(event) => {
                tipoField.onChange(event)
                resetField('orcamentoMensal')
              }}
            />
            Receita
          </label>
        </div>
        {errors.tipo && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.tipo.message}
          </p>
        )}
      </fieldset>

      {tipo === 'despesa' && (
        <label className="field">
          <span>Teto mensal (R$)</span>
          <input
            className="input"
            placeholder="0,00"
            aria-invalid={!!errors.orcamentoMensal}
            {...register('orcamentoMensal')}
          />
          {errors.orcamentoMensal && (
            <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
              {errors.orcamentoMensal.message}
            </p>
          )}
        </label>
      )}

      <div className="dialog-actions">
        <button type="button" className="btn btn-secondary" onClick={onCancel}>
          Cancelar
        </button>
        <button type="submit" className="btn btn-primary" disabled={isLoading}>
          {isLoading ? 'Salvando...' : mode === 'edit' ? 'Salvar' : 'Criar categoria'}
        </button>
      </div>
    </form>
  )
}
