import { z } from 'zod'

// Schema do modo "Criar conta" da tela de Login (FEAT-14). Não há endpoint
// de cadastro no backend hoje: este schema só valida o formulário antes de
// navegar para a página fake `/cadastro-em-breve` — nunca é enviado a
// nenhuma API.
export const signupSchema = z.object({
  name: z.string().min(1, 'Informe seu nome.'),
  email: z.string().email('Informe um email válido.'),
  password: z.string().min(8, 'A senha deve ter no mínimo 8 caracteres.'),
})

export type SignupFormData = z.infer<typeof signupSchema>
