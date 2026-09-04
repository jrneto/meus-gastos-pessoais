import { z } from 'zod'
import { passwordPolicySchema } from './passwordPolicySchema'

// Passo 1/3 (email) da recuperação de senha (FEAT-32).
export const forgotPasswordEmailSchema = z.object({
  email: z.string().email('Informe um email válido.'),
})
export type ForgotPasswordEmailData = z.infer<typeof forgotPasswordEmailSchema>

// Passo 3/3 (nova senha). `passwordPolicySchema` valida a força da
// senha; o `.refine` garante que os dois campos coincidem — checado no
// client antes de qualquer chamada a `POST /auth/reset-password`.
export const newPasswordSchema = z
  .object({
    newPassword: passwordPolicySchema,
    confirmNewPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmNewPassword, {
    message: 'As senhas não coincidem.',
    path: ['confirmNewPassword'],
  })
export type NewPasswordFormData = z.infer<typeof newPasswordSchema>
