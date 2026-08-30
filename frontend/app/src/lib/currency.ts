export function parseCurrencyToCents(value: string): number {
  const normalized = value.trim().replace(/\./g, '').replace(',', '.')
  return Math.round(Number(normalized) * 100)
}

// `toLocaleString('pt-BR', { style: 'currency', ... })` usa espaço
// não-quebravel (U+00A0) entre "R$" e o valor, nao espaco normal (\x20)
// - normaliza pra espaco comum, senao comparacoes de string (testes,
// snapshots) ficam refens de qual caractere o ICU da engine JS decidiu
// usar.
export function formatCentsToCurrency(cents: number): string {
  return (cents / 100)
    .toLocaleString('pt-BR', { style: 'currency', currency: 'BRL' })
    .replace(/\u00A0/g, ' ')
}

export function centsToAmountInput(cents: number): string {
  return (cents / 100).toLocaleString('pt-BR', {
    minimumFractionDigits: 2,
    maximumFractionDigits: 2,
  })
}
