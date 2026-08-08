import { z } from 'zod'
import { EXPENSE_CATEGORIES } from '../constants/expenseCategories'
import { parseCurrencyToCents } from '../utils/currency'

const CURRENCY_REGEX = /^\d+(\.\d{3})*(,\d{2})?$/

const CATEGORY_VALUES = EXPENSE_CATEGORIES.map((category) => category.value) as [
  string,
  ...string[],
]

export const expenseSchema = z.object({
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
  category: z.enum(CATEGORY_VALUES, { message: 'Selecione uma categoria.' }),
  expenseDate: z.string().min(1, 'Informe a data.'),
})

export type ExpenseFormInput = z.input<typeof expenseSchema>
export type ExpenseFormOutput = z.output<typeof expenseSchema>
