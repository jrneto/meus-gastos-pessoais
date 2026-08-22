import { Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import { findCategoryIcon } from '@/lib/categories/categoryIcons'
import type { CategoryItem } from '@/lib/categories/types'
import { CategoryDeleteDialog } from './CategoryDeleteDialog'
import { CategoryForm } from './CategoryForm'

interface CategoryListProps {
  items: CategoryItem[]
  isLoading: boolean
  error: Error | null
  onDeleted: (id: string) => void
  editingId: string | null
  onEditToggle: (id: string) => void
  onSaved: (category: CategoryItem) => void
  onNotFound: (id: string) => void
}

export function CategoryList({
  items,
  isLoading,
  error,
  onDeleted,
  editingId,
  onEditToggle,
  onSaved,
  onNotFound,
}: CategoryListProps) {
  const [deleteTarget, setDeleteTarget] = useState<CategoryItem | null>(null)

  return (
    <div className="ds-modernist" style={{ display: 'flex', width: '100%', flexDirection: 'column', gap: 'var(--space-4)' }}>
      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível buscar as categorias</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      {!isLoading && items.length === 0 && !error && (
        <p style={{ opacity: 0.55, fontSize: '13px' }}>
          Você ainda não tem nenhuma categoria cadastrada.
        </p>
      )}

      <ul style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', margin: 0, padding: 0, listStyle: 'none' }}>
        {items.map((item) => {
          const Icon = findCategoryIcon(item.icone)
          return (
            <li
              key={item.id}
              style={{
                display: 'flex',
                flexDirection: 'column',
                gap: 'var(--space-3)',
                borderBottom: '1px solid var(--color-divider)',
                paddingBottom: 'var(--space-3)',
              }}
            >
              {editingId === item.id ? (
                <CategoryForm
                  mode="edit"
                  categoryId={item.id}
                  initialValues={{ nome: item.nome, cor: item.cor, icone: item.icone }}
                  onSaved={onSaved}
                  onNotFound={() => onNotFound(item.id)}
                  onCancel={() => onEditToggle(item.id)}
                />
              ) : (
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ display: 'inline-flex', alignItems: 'center', gap: 'var(--space-2)', color: item.cor }}>
                    {Icon && <Icon size={16} />}
                    <span>{item.nome}</span>
                  </span>
                  <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
                    <button
                      type="button"
                      className="btn"
                      aria-label="Editar categoria"
                      onClick={() => onEditToggle(item.id)}
                    >
                      <Pencil size={16} />
                    </button>
                    <button
                      type="button"
                      className="btn"
                      aria-label="Excluir categoria"
                      onClick={() => setDeleteTarget(item)}
                    >
                      <Trash2 size={16} />
                    </button>
                  </div>
                </div>
              )}
            </li>
          )
        })}
      </ul>

      <CategoryDeleteDialog
        key={deleteTarget?.id ?? 'closed'}
        category={deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        onDeleted={(id) => {
          onDeleted(id)
          setDeleteTarget(null)
        }}
      />
    </div>
  )
}
