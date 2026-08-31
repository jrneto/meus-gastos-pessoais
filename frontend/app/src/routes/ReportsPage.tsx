import { useState } from 'react'
import '@/styles/modernist/modernist.css'
import type { ReportPeriod } from '@/features/reports/api/reportsApi'
import { CategoryReportList } from '@/features/reports/components/CategoryReportList'
import { PeriodToggle } from '@/features/reports/components/PeriodToggle'
import { TopCategoryCard } from '@/features/reports/components/TopCategoryCard'
import { TotalPeriodCard } from '@/features/reports/components/TotalPeriodCard'
import { useReports } from '@/features/reports/hooks/useReports'
import { getCurrentDate } from '@/features/reports/utils/period'

// Tela "Relatórios" (FEAT-27) — substitui o placeholder de
// ReportsComingSoonPage. Sempre a data corrente, sem seletor de data
// (decisão fechada na spec); o usuário só troca o `period` pelo
// `PeriodToggle`.
export function ReportsPage() {
  const [period, setPeriod] = useState<ReportPeriod>('month')
  const date = getCurrentDate()
  const { data, isLoading, error } = useReports(period, date)

  return (
    <div
      className="ds-modernist"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-6)',
        maxWidth: '920px',
        margin: '0 auto',
        padding: '40px 40px 60px',
        boxSizing: 'border-box',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <h1 style={{ fontSize: '30px', margin: 0 }}>Relatórios</h1>
        <PeriodToggle value={period} onChange={setPeriod} />
      </div>

      {isLoading && <p style={{ opacity: 0.7, fontSize: '14px' }}>Carregando...</p>}

      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível carregar o relatório</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      {!isLoading && !error && data && (
        <div style={{ display: 'grid', gridTemplateColumns: '1.3fr 1fr', gap: 'var(--space-8)' }}>
          <div style={{ minWidth: 0 }}>
            <h2 style={{ fontSize: '15px', margin: '0 0 14px' }}>Gasto por categoria</h2>
            <CategoryReportList items={data.porCategoria} />
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)', minWidth: 0 }}>
            <TotalPeriodCard totalCents={data.totalCents} variacaoPercentual={data.variacaoPercentual} period={period} />
            <TopCategoryCard category={data.maiorGasto} />
          </div>
        </div>
      )}
    </div>
  )
}
