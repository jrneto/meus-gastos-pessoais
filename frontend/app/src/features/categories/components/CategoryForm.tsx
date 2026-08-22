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
import { IconPicker } from './IconPicker'

const DEFAULT_COLOR = '#3B82F6'

interface CategoryFormProps {
  mode?: 'create' | 'edit'
  categoryId?: string
  initialValues?: CategoryFormInput
  onSaved: (category: CategoryItem) => void
  onNotFound?: () => void
  onCancel: () => void
}

// Formulário único de categoria (cadastro/edição), montado inline —
// sem popup, fiel ao design de referência (FEAT-19). Substitui
// NewCategoryForm/EditCategoryForm/CategoryFormFields.
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
    setValue,
    setError,
    watch,
    formState: { errors },
  } = useForm<CategoryFormInput, unknown, CategoryFormOutput>({
    resolver: zodResolver(categorySchema),
    defaultValues: initialValues ?? { nome: '', cor: DEFAULT_COLOR, icone: undefined },
  })

  const cor = watch('cor')
  const icone = watch('icone')

  useEffect(() => {
    if (success && data) {
      if (mode === 'create') {
        reset({ nome: '', cor: DEFAULT_COLOR, icone: undefined })
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

      <div className="field">
        <label htmlFor="cor">Cor</label>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
          <input
            id="cor"
            type="color"
            aria-invalid={!!errors.cor}
            style={{ width: '40px', height: '32px', padding: 0, border: '1px solid var(--color-divider)', cursor: 'pointer' }}
            {...register('cor')}
          />
          <span style={{ fontSize: '13px', opacity: 0.7 }}>{cor}</span>
        </div>
        {errors.cor && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.cor.message}
          </p>
        )}
      </div>

      <div className="field">
        <span>Ícone</span>
        <IconPicker value={icone} onChange={(value) => setValue('icone', value)} error={!!errors.icone} />
        {errors.icone && (
          <p role="alert" style={{ color: 'var(--color-accent-700)', fontSize: '12px', margin: '4px 0 0' }}>
            {errors.icone.message}
          </p>
        )}
      </div>

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
