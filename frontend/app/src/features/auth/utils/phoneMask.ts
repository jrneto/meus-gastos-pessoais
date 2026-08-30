// Máscara progressiva de telefone do formulário de cadastro (FEAT-21).
// Só formata para exibição — o valor enviado à API é sempre os
// dígitos crus (`extractDigits`, ver `utils/cpf.ts`).
import { extractDigits } from './cpf'

export function maskPhone(digits: string): string {
  const d = extractDigits(digits, 11)

  if (d.length <= 2) return d
  if (d.length <= 6) return `(${d.slice(0, 2)}) ${d.slice(2)}`
  if (d.length <= 10) return `(${d.slice(0, 2)}) ${d.slice(2, 6)}-${d.slice(6)}`
  return `(${d.slice(0, 2)}) ${d.slice(2, 7)}-${d.slice(7)}`
}
