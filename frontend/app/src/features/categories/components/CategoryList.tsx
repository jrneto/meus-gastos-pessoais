import { Pencil, Trash2 } from 'lucide-react'
import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import { centsToAmountInput, formatCentsToCurrency } from '@/lib/currency'
import { CategoryLetterTile } from '@/lib/categories/CategoryLetterTile'
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

const SECTION_HEADING_STYLE: React.CSSProperties = {
  fontSize: '13px',
  margin: '0 0 var(--space-2)',
  letterSpacing: '.08em',
  textTransform: 'uppercase',
  paddingBottom: 'var(--space-2)',
  borderBottom: '2px solid var(--color-divider)',
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
  const expenseItems = items.filter((item) => item.tipo === 'despesa')
  const incomeItems = items.filter((item) => item.tipo === 'receita')

  function renderItem(item: CategoryItem) {
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
            initialValues={{
              nome: item.nome,
              tipo: item.tipo,
              orcamentoMensal:
                item.orcamentoMensalCents != null ? centsToAmountInput(item.orcamentoMensalCents) : '',
            }}
            onSaved={onSaved}
            onNotFound={() => onNotFound(item.id)}
            onCancel={() => onEditToggle(item.id)}
          />
        ) : (
          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 'var(--space-3)' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
              <CategoryLetterTile name={item.nome} tipo={item.tipo} />
              <span>{item.nome}</span>
            </div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
              {item.tipo === 'despesa' && (
                <span style={{ fontSize: '13px', opacity: item.orcamentoMensalCents == null ? 0.55 : 1 }}>
                  {item.orcamentoMensalCents != null
                    ? formatCentsToCurrency(item.orcamentoMensalCents)
                    : 'Sem teto definido'}
                </span>
              )}
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
          </div>
        )}
      </li>
    )
  }

  return (
    <div className="ds-modernist" style={{ display: 'flex', width: '100%', flexDirection: 'column', gap: 'var(--space-6)' }}>
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

      {items.length > 0 && (
        <>
          <section>
            <h2 style={SECTION_HEADING_STYLE}>Categorias de despesa</h2>
            <ul style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', margin: 0, padding: 0, listStyle: 'none' }}>
              {expenseItems.map(renderItem)}
            </ul>
          </section>

          <section>
            <h2 style={SECTION_HEADING_STYLE}>Categorias de receita</h2>
            <ul style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-2)', margin: 0, padding: 0, listStyle: 'none' }}>
              {incomeItems.map(renderItem)}
            </ul>
          </section>
        </>
      )}

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
