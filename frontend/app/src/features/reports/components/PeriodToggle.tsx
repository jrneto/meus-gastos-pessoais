import '@/styles/modernist/modernist.css'
import type { ReportPeriod } from '../api/reportsApi'

interface PeriodToggleProps {
  value: ReportPeriod
  onChange: (period: ReportPeriod) => void
}

const OPTIONS: { period: ReportPeriod; label: string }[] = [
  { period: 'week', label: 'Semana' },
  { period: 'month', label: 'Mês' },
  { period: 'year', label: 'Ano' },
]

// Seletor Semana/Mês/Ano (FEAT-27) — reaproveita `.seg`/`.seg-opt`
// (vendorizados desde a FEAT-14, hoje usados no painel de filtros de
// transação), mesmo padrão do `.dc.html` (bloco `isRep`).
export function PeriodToggle({ value, onChange }: PeriodToggleProps) {
  return (
    <div className="ds-modernist seg">
      {OPTIONS.map((option) => (
        <label key={option.period} className="seg-opt">
          <input
            type="radio"
            name="period"
            checked={value === option.period}
            onChange={() => onChange(option.period)}
            style={{ display: 'none' }}
          />
          {option.label}
        </label>
      ))}
    </div>
  )
}
