import { z } from 'zod'
import { parseCurrencyToCents } from '@/lib/currency'

const CURRENCY_REGEX = /^\d+(\.\d{3})*(,\d{2})?$/

export const expenseFilterSchema = z
  .object({
    yearMonth: z.string().optional(),
    categoryId: z.string().optional(),
    dateFrom: z.string().optional(),
    dateTo: z.string().optional(),
    minAmount: z
      .string()
      .optional()
      .refine((value) => !value || CURRENCY_REGEX.test(value), 'Use o formato 0,00.'),
    maxAmount: z
      .string()
      .optional()
      .refine((value) => !value || CURRENCY_REGEX.test(value), 'Use o formato 0,00.'),
  })
  .transform((data) => ({
    yearMonth: data.yearMonth || undefined,
    categoryId: data.categoryId || undefined,
    dateFrom: data.dateFrom || undefined,
    dateTo: data.dateTo || undefined,
    minAmountInCents: data.minAmount ? parseCurrencyToCents(data.minAmount) : undefined,
    maxAmountInCents: data.maxAmount ? parseCurrencyToCents(data.maxAmount) : undefined,
  }))
  .refine((filters) => !filters.dateFrom || !filters.dateTo || filters.dateFrom <= filters.dateTo, {
    message: 'Data inicial não pode ser depois da data final.',
    path: ['dateTo'],
  })
  .refine(
    (filters) =>
      filters.minAmountInCents === undefined ||
      filters.maxAmountInCents === undefined ||
      filters.minAmountInCents <= filters.maxAmountInCents,
    { message: 'Valor mínimo não pode ser maior que o máximo.', path: ['maxAmount'] },
  )

export type ExpenseFilterInput = z.input<typeof expenseFilterSchema>
export type ExpenseFilterOutput = z.output<typeof expenseFilterSchema>
