import { z } from 'zod'
import { CATEGORY_ICONS } from '@/lib/categories/categoryIcons'

const HEX_COLOR_REGEX = /^#[0-9A-Fa-f]{6}$/
const ICON_VALUES = CATEGORY_ICONS.map((icon) => icon.value) as [string, ...string[]]

export const categorySchema = z.object({
  nome: z
    .string()
    .trim()
    .min(1, 'Informe o nome.')
    .max(50, 'O nome deve ter no máximo 50 caracteres.'),
  cor: z.string().regex(HEX_COLOR_REGEX, 'Selecione uma cor.'),
  icone: z.enum(ICON_VALUES, { message: 'Selecione um ícone.' }),
})

export type CategoryFormInput = z.input<typeof categorySchema>
export type CategoryFormOutput = z.output<typeof categorySchema>
