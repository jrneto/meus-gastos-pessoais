import { z } from 'zod'
import { parseCurrencyToCents } from '@/lib/currency'

const CURRENCY_REGEX = /^\d+(\.\d{3})*(,\d{2})?$/

export const transactionSchema = z.object({
  description: z
    .string()
    .trim()
    .min(1, 'Informe a descrição.')
    .max(200, 'A descrição deve ter no máximo 200 caracteres.'),
  amount: z
    .string()
    .min(1, 'Informe o valor.')
    .regex(CURRENCY_REGEX, 'Use o formato 0,00.')
    .transform(parseCurrencyToCents)
    .refine((cents) => cents > 0, 'O valor deve ser maior que zero.'),
  categoryId: z.string().min(1, 'Selecione uma categoria.'),
  date: z.string().min(1, 'Informe a data.'),
})

export type TransactionFormInput = z.input<typeof transactionSchema>
export type TransactionFormOutput = z.output<typeof transactionSchema>
