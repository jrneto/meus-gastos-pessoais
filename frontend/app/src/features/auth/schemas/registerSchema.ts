import { z } from 'zod'
import { isValidCpf } from '../utils/cpf'

// Schema do modo "Criar conta" da tela de Login (FEAT-21). Espelha as
// regras já validadas pelo backend (`RegisterUserCommandValidator`,
// `backend/specs/FEAT-26-perfil-usuario-cadastro`) para dar feedback
// antes do submit — a API continua sendo a fonte de verdade final.
// `phoneDigits`/`cpfDigits` guardam só dígitos: a máscara de exibição
// (`maskPhone`/`maskCpf`) é aplicada só no `value` do input, nunca no
// estado do formulário.
export const registerSchema = z.object({
  name: z
    .string()
    .trim()
    .min(2, 'Informe seu nome completo.')
    .max(150, 'O nome não pode ter mais de 150 caracteres.'),
  phoneDigits: z.string().regex(/^\d{10,11}$/, 'Telefone deve ter 10 ou 11 dígitos.'),
  cpfDigits: z
    .string()
    .regex(/^\d{11}$/, 'CPF deve ter 11 dígitos.')
    .refine(isValidCpf, 'CPF inválido.'),
  email: z.string().email('Informe um email válido.'),
  password: z.string().min(8, 'A senha deve ter no mínimo 8 caracteres.'),
})

export type RegisterFormData = z.infer<typeof registerSchema>
