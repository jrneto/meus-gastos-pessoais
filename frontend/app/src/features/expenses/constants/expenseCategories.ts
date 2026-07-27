export const EXPENSE_CATEGORIES = [
  { value: 'Alimentacao', label: 'Alimentação' },
  { value: 'Transporte', label: 'Transporte' },
  { value: 'Moradia', label: 'Moradia' },
  { value: 'Saude', label: 'Saúde' },
  { value: 'Educacao', label: 'Educação' },
  { value: 'Lazer', label: 'Lazer' },
  { value: 'ComprasEServicos', label: 'Compras e Serviços' },
  { value: 'Outros', label: 'Outros' },
] as const

export type ExpenseCategory = (typeof EXPENSE_CATEGORIES)[number]['value']