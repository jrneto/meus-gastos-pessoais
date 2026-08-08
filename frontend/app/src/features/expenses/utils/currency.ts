export function parseCurrencyToCents(value: string): number {
  const normalized = value.trim().replace(/\./g, '').replace(',', '.')
  return Math.round(Number(normalized) * 100)
}

export function formatCentsToCurrency(cents: number): string {
  return (cents / 100)
    .toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
    .replace(/ /g, ' ')
}

export function centsToAmountInput(cents: number): string {
  return (cents / 100).toLocaleString('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}
