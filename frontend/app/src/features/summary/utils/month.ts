const MONTHS_PT = [
  'janeiro',
  'fevereiro',
  'março',
  'abril',
  'maio',
  'junho',
  'julho',
  'agosto',
  'setembro',
  'outubro',
  'novembro',
  'dezembro',
]

// Mês corrente no formato YYYY-MM, no fuso do dispositivo do usuário —
// FEAT-26 sempre busca o resumo do mês atual, sem navegação pra outros
// meses (decisão fechada na spec).
export function getCurrentYearMonth(): string {
  const now = new Date()
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`
}

// "2026-08" -> "Agosto de 2026" — mesmo formato do rótulo de mês do
// design (`.dc.html`, `currentMonthLabel`).
export function formatMonthLabel(month: string): string {
  const [year, monthNumber] = month.split('-').map(Number)
  const name = MONTHS_PT[monthNumber - 1]
  return `${name.charAt(0).toUpperCase()}${name.slice(1)} de ${year}`
}
