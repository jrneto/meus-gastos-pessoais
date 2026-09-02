import '@/styles/modernist/modernist.css'
import type { ReportCategoryItem } from '../api/reportsApi'
import { formatCentsToCurrency } from '@/lib/currency'

interface CategoryReportListProps {
  items: ReportCategoryItem[]
}

// "Gasto por categoria" (FEAT-27) — a lista já vem ordenada por gasto
// decrescente (contrato do backend), então o primeiro item é sempre o
// maior gasto: a barra de cada categoria é proporcional a ele (100%
// para o primeiro, o resto proporcional). Sempre cor neutra — o
// contrato de `/reports` não traz orçamento por categoria em
// `porCategoria`, então não há como replicar o destaque de
// acima-do-orçamento aqui (decisão 2 da spec).
export function CategoryReportList({ items }: CategoryReportListProps) {
  if (items.length === 0) {
    return (
      <p className="ds-modernist" style={{ opacity: 0.55, fontSize: '13px' }}>
        Nenhuma despesa neste período.
      </p>
    )
  }

  const maxGastoCents = items[0].gastoCents

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column', gap: '14px' }}>
      {items.map((item) => {
        const widthPct = Math.round((item.gastoCents / maxGastoCents) * 100)

        return (
          <div key={item.categoryId} style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div
              style={{
                width: '80px',
                fontSize: '12.5px',
                flex: 'none',
                overflow: 'hidden',
                textOverflow: 'ellipsis',
                whiteSpace: 'nowrap',
              }}
            >
              {item.nome}
            </div>
            <div style={{ flex: 1, height: '18px', background: 'var(--color-neutral-300)', position: 'relative' }}>
              <div style={{ height: '100%', width: `${widthPct}%`, background: 'var(--color-neutral-800)' }} />
            </div>
            <div style={{ width: '90px', fontSize: '12.5px', textAlign: 'right', flex: 'none' }}>
              {formatCentsToCurrency(item.gastoCents)}
            </div>
          </div>
        )
      })}
    </div>
  )
}
