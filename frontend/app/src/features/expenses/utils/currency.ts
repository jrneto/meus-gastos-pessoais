export function parseCurrencyToCents(value: string): number {
  const normalized = value.trim().replace(/\./g, '').replace(',', '.')
  return Math.round(Number(normalized) * 100)
}