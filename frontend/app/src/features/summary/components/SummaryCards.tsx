import '@/styles/modernist/modernist.css'
import type { SummaryResponse } from '../api/summaryApi'
import { formatCentsToCurrency } from '@/lib/currency'

interface SummaryCardsProps {
  summary: SummaryResponse
}

// Os cinco cartões do "Resumo" (FEAT-26) — saldo e restante mostram o
// valor negativo real (com sinal e cor accent) em vez de escondê-lo,
// decisão fechada na spec: o backend permite `restanteCents` negativo
// e deixa a exibição do estouro a critério do frontend.
export function SummaryCards({ summary }: SummaryCardsProps) {
  const saldoNegative = summary.saldoCents < 0
  const saldoColor = saldoNegative ? 'var(--color-accent-700)' : 'var(--color-positive-700)'
  const saldoFmt = `${saldoNegative ? '- ' : ''}${formatCentsToCurrency(Math.abs(summary.saldoCents))}`

  const restanteNegative = summary.restanteCents < 0
  const restanteColor = restanteNegative ? 'var(--color-accent-700)' : 'var(--color-text)'
  const restanteFmt = `${restanteNegative ? '- ' : ''}${formatCentsToCurrency(Math.abs(summary.restanteCents))}`
  const restantePct =
    summary.orcamentoTotalCents > 0
      ? Math.min(100, (summary.gastoCents / summary.orcamentoTotalCents) * 100)
      : 0

  return (
    <div
      className="ds-modernist"
      style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 'var(--space-4)' }}
    >
      <div className="card elev-sm">
        <div className="card-kicker">Saldo do mês</div>
        <div style={{ fontSize: '22px', fontWeight: 800, fontFamily: 'var(--font-heading)', color: saldoColor }}>
          {saldoFmt}
        </div>
      </div>
      <div className="card elev-sm">
        <div className="card-kicker">Receitas no mês</div>
        <div style={{ fontSize: '22px', fontWeight: 800, fontFamily: 'var(--font-heading)' }}>
          {formatCentsToCurrency(summary.receitasCents)}
        </div>
      </div>
      <div className="card elev-sm">
        <div className="card-kicker">Gasto no mês</div>
        <div style={{ fontSize: '22px', fontWeight: 800, fontFamily: 'var(--font-heading)' }}>
          {formatCentsToCurrency(summary.gastoCents)}
        </div>
      </div>
      <div className="card elev-sm">
        <div className="card-kicker">Orçamento total</div>
        <div style={{ fontSize: '22px', fontWeight: 800, fontFamily: 'var(--font-heading)' }}>
          {formatCentsToCurrency(summary.orcamentoTotalCents)}
        </div>
      </div>
      <div className="card elev-sm">
        <div className="card-kicker">Restante</div>
        <div style={{ fontSize: '22px', fontWeight: 800, fontFamily: 'var(--font-heading)', color: restanteColor }}>
          {restanteFmt}
        </div>
        <div className="je-track">
          <div className="je-fill" style={{ width: `${restantePct}%`, background: 'var(--color-text)' }} />
        </div>
      </div>
    </div>
  )
}
