// Utilitários de máscara/validação de CPF do formulário de cadastro
// (FEAT-21). `isValidCpf` replica o mesmo algoritmo de dígito
// verificador usado no backend (`GastosApp.Domain.Users.Cpf`),
// incluindo a rejeição de sequências com todos os dígitos iguais —
// validação client-side é só UX antecipada, a fonte de verdade
// continua sendo a API.

export function extractDigits(value: string, maxLen: number): string {
  return value.replace(/\D/g, '').slice(0, maxLen)
}

export function maskCpf(digits: string): string {
  const d = extractDigits(digits, 11)

  if (d.length <= 3) return d
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`
}

function calculateCheckDigit(digits: string, weightStart: number): number {
  let sum = 0
  for (let i = 0; i < digits.length; i++) {
    sum += Number(digits[i]) * (weightStart - i)
  }
  const remainder = sum % 11
  return remainder < 2 ? 0 : 11 - remainder
}

export function isValidCpf(digits: string): boolean {
  if (!/^\d{11}$/.test(digits)) return false
  if (/^(\d)\1{10}$/.test(digits)) return false

  const firstCheckDigit = calculateCheckDigit(digits.slice(0, 9), 10)
  if (firstCheckDigit !== Number(digits[9])) return false

  const secondCheckDigit = calculateCheckDigit(digits.slice(0, 10), 11)
  return secondCheckDigit === Number(digits[10])
}
