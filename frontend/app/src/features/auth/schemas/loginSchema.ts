import { z } from 'zod'

export const loginSchema = z.object({
  email: z.string().email('Informe um email válido.'),
  password: z.string().min(8, 'A senha deve ter no mínimo 8 caracteres.'),
})

export type LoginCredentials = z.infer<typeof loginSchema>
