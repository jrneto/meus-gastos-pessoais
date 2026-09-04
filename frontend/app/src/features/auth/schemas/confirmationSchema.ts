import { z } from 'zod'

// Regra "6 dígitos numéricos" do código de confirmação (FEAT-31) — a UI
// é 6 inputs de 1 dígito cada (não um único campo de texto de um
// `useForm`), então este schema é validado via `.safeParse()` no
// submit, não amarrado a um form completo do React Hook Form.
export const confirmationCodeSchema = z
  .string()
  .regex(/^\d{6}$/, 'Digite os 6 dígitos do código.')
