import '@/styles/modernist/modernist.css'
import type { CategorySummaryItem } from '../api/summaryApi'
import { formatCentsToCurrency } from '@/lib/currency'

interface CategorySpendingListProps {
  items: CategorySummaryItem[]
}

// "Onde o dinheiro foi este mês" (FEAT-26) — só categorias de despesa
// com orçamento definido chegam aqui (já filtradas/ordenadas por
// GET /summary, backend FEAT-23); categoria acima do orçamento fica
// destacada (texto e barra na cor accent), mesmo padrão do
// `computeCategories()` do `.dc.html`.
export function CategorySpendingList({ items }: CategorySpendingListProps) {
  if (items.length === 0) {
    return (
      <p className="ds-modernist" style={{ opacity: 0.55, fontSize: '13px' }}>
        Nenhuma categoria com orçamento definido ainda.
      </p>
    )
  }

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
      {items.map((item) => {
        const over = item.gastoCents > item.orcamentoMensalCents
        const pct =
          item.orcamentoMensalCents > 0 ? Math.min(100, (item.gastoCents / item.orcamentoMensalCents) * 100) : 0
        const color = over ? 'var(--color-accent-700)' : 'var(--color-text)'
        const barColor = over ? 'var(--color-accent)' : 'var(--color-neutral-800)'

        return (
          <div key={item.categoryId}>
            <div
              style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                gap: 'var(--space-2)',
                marginBottom: '5px',
              }}
            >
              <span style={{ fontSize: '13px' }}>{item.nome}</span>
              <span style={{ fontSize: '12px', color, whiteSpace: 'nowrap' }}>
                {formatCentsToCurrency(item.gastoCents)} / {formatCentsToCurrency(item.orcamentoMensalCents)}
              </span>
            </div>
            <div className="je-track">
              <div className="je-fill" style={{ width: `${pct}%`, background: barColor }} />
            </div>
          </div>
        )
      })}
    </div>
  )
}
