import { useEffect, useState } from 'react'
import '@/styles/modernist/modernist.css'
import { CategoryForm } from '@/features/categories/components/CategoryForm'
import { CategoryList } from '@/features/categories/components/CategoryList'
import { useCategories } from '@/lib/categories/useCategories'
import type { CategoryItem } from '@/lib/categories/types'

type CategoryFormTarget = { mode: 'create' } | { mode: 'edit'; id: string } | null

export function CategoriesPage() {
  const { items: fetchedItems, isLoading, error } = useCategories()
  const [items, setItems] = useState<CategoryItem[]>([])
  const [formTarget, setFormTarget] = useState<CategoryFormTarget>(null)

  useEffect(() => {
    setItems(fetchedItems)
  }, [fetchedItems])

  function handleSaved(category: CategoryItem) {
    setItems((prev) => {
      const exists = prev.some((item) => item.id === category.id)
      return exists ? prev.map((item) => (item.id === category.id ? category : item)) : [...prev, category]
    })
    setFormTarget(null)
  }

  function handleNotFound(id: string) {
    setItems((prev) => prev.filter((item) => item.id !== id))
    setFormTarget(null)
  }

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-6)' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Categorias</h1>
        <button
          type="button"
          className="btn btn-primary"
          onClick={() => setFormTarget((current) => (current?.mode === 'create' ? null : { mode: 'create' }))}
        >
          + Nova categoria
        </button>
      </div>

      {formTarget?.mode === 'create' && (
        <CategoryForm mode="create" onSaved={handleSaved} onCancel={() => setFormTarget(null)} />
      )}

      <CategoryList
        items={items}
        isLoading={isLoading}
        error={error}
        onDeleted={(id) => setItems((prev) => prev.filter((item) => item.id !== id))}
        editingId={formTarget?.mode === 'edit' ? formTarget.id : null}
        onEditToggle={(id) =>
          setFormTarget((current) => (current?.mode === 'edit' && current.id === id ? null : { mode: 'edit', id }))
        }
        onSaved={handleSaved}
        onNotFound={handleNotFound}
      />
    </div>
  )
}
