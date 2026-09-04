import { z } from 'zod'

// Política completa do Cognito (`cognito.tf` de hom/prod), a mesma que
// `backend/specs/FEAT-36-recuperacao-senha` já valida via
// `ConfirmForgotPassword`. Compartilhada entre o campo "Nova senha" da
// recuperação de senha (FEAT-32) e o campo de senha do cadastro
// (`registerSchema`) — antes desta feature, o cadastro só validava o
// mínimo de 8 caracteres, divergindo da política real.
export const passwordPolicySchema = z
  .string()
  .min(8, 'A senha deve ter no mínimo 8 caracteres.')
  .regex(/[A-Z]/, 'A senha deve ter ao menos uma letra maiúscula.')
  .regex(/[a-z]/, 'A senha deve ter ao menos uma letra minúscula.')
  .regex(/[0-9]/, 'A senha deve ter ao menos um número.')
  .regex(/[^A-Za-z0-9]/, 'A senha deve ter ao menos um símbolo.')
