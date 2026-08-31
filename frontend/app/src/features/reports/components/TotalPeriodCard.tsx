import '@/styles/modernist/modernist.css'
import type { ReportPeriod } from '../api/reportsApi'
import { formatCentsToCurrency } from '@/lib/currency'
import { formatComparisonLabel } from '../utils/period'

interface TotalPeriodCardProps {
  totalCents: number
  variacaoPercentual: number | null
  period: ReportPeriod
}

// Card "Total no período" (FEAT-27) — a linha de comparação com o
// período anterior some quando `variacaoPercentual` é `null` (período
// anterior sem gasto, período atual com gasto — não computável,
// decisão 3 da spec).
export function TotalPeriodCard({ totalCents, variacaoPercentual, period }: TotalPeriodCardProps) {
  return (
    <div className="ds-modernist card elev-sm">
      <div className="card-kicker">Total no período</div>
      <div style={{ fontSize: '24px', fontWeight: 800, fontFamily: 'var(--font-heading)' }}>
        {formatCentsToCurrency(totalCents)}
      </div>
      {variacaoPercentual !== null && (
        <div style={{ fontSize: '12px', opacity: 0.6 }}>{formatComparisonLabel(variacaoPercentual, period)}</div>
      )}
    </div>
  )
}
