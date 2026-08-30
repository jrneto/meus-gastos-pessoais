import { z } from 'zod'
import { parseCurrencyToCents } from '@/lib/currency'

const CURRENCY_REGEX = /^\d+(\.\d{3})*(,\d{2})?$/

export const categorySchema = z
  .object({
    nome: z
      .string()
      .trim()
      .min(1, 'Informe o nome.')
      .max(50, 'O nome deve ter no máximo 50 caracteres.'),
    tipo: z.enum(['despesa', 'receita'], { message: 'Selecione o tipo da categoria.' }),
    orcamentoMensal: z
      .string()
      .optional()
      .refine((value) => !value || CURRENCY_REGEX.test(value), 'Use o formato 0,00.'),
  })
  .transform((data) => ({
    nome: data.nome,
    tipo: data.tipo,
    orcamentoMensalCents:
      data.tipo === 'despesa' && data.orcamentoMensal
        ? parseCurrencyToCents(data.orcamentoMensal)
        : undefined,
  }))
  .refine((data) => data.orcamentoMensalCents === undefined || data.orcamentoMensalCents > 0, {
    message: 'O teto deve ser maior que zero.',
    path: ['orcamentoMensal'],
  })

export type CategoryFormInput = z.input<typeof categorySchema>
export type CategoryFormOutput = z.output<typeof categorySchema>
