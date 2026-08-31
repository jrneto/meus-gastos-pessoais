import type { ReportPeriod } from '../api/reportsApi'

export function getCurrentDate(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`
}

const PREVIOUS_PERIOD_LABEL: Record<ReportPeriod, string> = {
  week: 'semana passada',
  month: 'mês passado',
  year: 'ano passado',
}

// 1 casa decimal, vírgula (pt-BR) — mesma convenção de
// `formatCentsToCurrency` (`lib/currency.ts`): `12` continua `"12"`
// (sem ".0"/",0" à toa), `54.4` vira `"54,4"`.
export function formatPercent(value: number): string {
  return value.toLocaleString('pt-BR', { maximumFractionDigits: 1 })
}

// Só chamada quando `variacaoPercentual !== null` (ver TotalPeriodCard).
// Sinal `+` explícito pra valores >= 0 (`0` inclusive — "sem variação"
// também é informação); `-` já vem embutido no número quando negativo.
export function formatComparisonLabel(variacaoPercentual: number, period: ReportPeriod): string {
  const sign = variacaoPercentual >= 0 ? '+' : ''
  return `${sign}${formatPercent(variacaoPercentual)}% vs ${PREVIOUS_PERIOD_LABEL[period]}`
}
