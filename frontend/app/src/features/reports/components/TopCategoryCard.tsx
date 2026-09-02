import '@/styles/modernist/modernist.css'
import type { ReportTopCategory } from '../api/reportsApi'
import { formatCentsToCurrency } from '@/lib/currency'
import { formatPercent } from '../utils/period'

interface TopCategoryCardProps {
  category: ReportTopCategory | null
}

// Card "Maior gasto" (FEAT-27). `category` vem `null` quando não há
// nenhuma despesa no período (`porCategoria` vazio) — mostra um texto
// genérico no lugar (decisão 4 da spec). `percentualOrcamento` vem
// `null` quando a categoria não tem orçamento mensal definido — nesse
// caso mostra só nome e valor, sem percentual.
export function TopCategoryCard({ category }: TopCategoryCardProps) {
  return (
    <div className="ds-modernist card elev-sm" style={{ border: '1px solid var(--color-divider)' }}>
      <div className="card-kicker">Maior gasto</div>
      {category ? (
        <>
          <div className="card-title">{category.nome}</div>
          <div style={{ fontSize: '13px', opacity: 0.7 }}>
            {formatCentsToCurrency(category.gastoCents)}
            {category.percentualOrcamento !== null &&
              ` · ${formatPercent(category.percentualOrcamento)}% do orçamento`}
          </div>
        </>
      ) : (
        <div className="card-title" style={{ opacity: 0.55 }}>
          Nenhum gasto registrado
        </div>
      )}
    </div>
  )
}
