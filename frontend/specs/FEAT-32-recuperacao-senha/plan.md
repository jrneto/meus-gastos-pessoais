# Plan — FEAT-32: Recuperação de senha (fluxo completo)

## Camadas afetadas

Só `features/auth/` (feature-based, sem camadas em sentido Clean
Architecture). Nenhuma rota nova, nenhum recurso novo em
`components/`/`lib/` — tudo fica dentro da feature `auth`, que já existe
(mesmo padrão da FEAT-31).

| Pasta | Muda? | O quê |
|---|---|---|
| `features/auth/api/authApi.ts` | Sim | `forgotPassword` e `resetPassword` |
| `features/auth/errors/authErrors.ts` | Sim | 2 erros novos |
| `features/auth/schemas/` | Sim | `passwordPolicySchema.ts` novo (compartilhado), `forgotPasswordSchema.ts` novo; `registerSchema.ts` passa a usar o novo `passwordPolicySchema` |
| `features/auth/hooks/` | Sim | 2 hooks novos (`useForgotPassword`, `useResetPassword`); `useResendCooldown` reaproveitado sem mudança |
| `features/auth/components/` | Sim | `OtpDigitsInput.tsx` novo (extraído de `ConfirmationForm`); `ConfirmationForm.tsx` refatorado pra usá-lo; `ForgotPasswordFlow.tsx` novo; `LoginForm.tsx` ganha a 4ª tela + link "Esqueci minha senha" |
| `routes/LoginPage.tsx` | Não | continua só o wrapper — nenhuma mudança de contrato com `LoginForm` |
| `app/router.tsx` | Não | nenhuma rota nova — mesmo padrão da FEAT-31 (estado interno de `LoginForm`, não URL própria) |

## Contratos técnicos

### `features/auth/api/authApi.ts`

```ts
export interface ForgotPasswordPayload {
  email: string
}

export interface ResetPasswordPayload {
  email: string
  code: string
  newPassword: string
}

async function forgotPassword(payload: ForgotPasswordPayload): Promise<void>
async function resetPassword(payload: ResetPasswordPayload): Promise<void>

export const authApi = { login, register, me, refresh, logout, confirm, resendConfirmation, forgotPassword, resetPassword }
```

- `forgotPassword`: `POST /auth/forgot-password`. Backend **sempre**
  retorna 200 (spec FEAT-36, decisão 1) — igual `resendConfirmation`,
  não há erro de negócio a mapear, só falha técnica (`!response.ok` →
  `UnknownAuthError`; erro de rede → `NetworkError` via `safeFetch`).
- `resetPassword`: `POST /auth/reset-password`. `200` → resolve sem
  valor. `400`:
  - `type` termina em `bad-request` (senha fora da política) →
    `WeakPasswordError`.
  - Qualquer outro `400` (`invalid-reset-code`, `expired-reset-code`, ou
    `validation-error` de campo ausente — inatingível pela UI porque o
    client já bloqueia campos vazios antes de chamar a API, mas mapeado
    pro mesmo lugar por segurança) → `InvalidResetCodeError`, mesmo
    princípio de não diferenciar `invalid-reset-code`/`expired-reset-
    code` já usado pela FEAT-31 (spec.md deste FEAT, decisão 3).
  - Qualquer outro `!response.ok` → `UnknownAuthError`.

```ts
async function resetPassword(payload: ResetPasswordPayload): Promise<void> {
  const response = await safeFetch(() => httpClient.post('/auth/reset-password', payload))

  if (response.status === 400) {
    const type = await readProblemType(response)
    if (type?.endsWith('bad-request')) {
      throw new WeakPasswordError()
    }
    throw new InvalidResetCodeError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
}
```

### `features/auth/errors/authErrors.ts`

- **Novo:** `InvalidResetCodeError` — mensagem "Código inválido ou
  expirado." (mais curta que a de `InvalidConfirmationCodeError`
  porque, aqui, o próprio texto do link de retorno já orienta o próximo
  passo — ver `NewPasswordStep` abaixo).
- **Novo:** `WeakPasswordError` — mensagem "A senha deve ter no mínimo
  8 caracteres, com letra maiúscula, minúscula, número e símbolo."
  (mesmo texto do `detail` que a API devolve, também usado como mensagem
  de validação client-side do `passwordPolicySchema` quando a regra
  falhar de forma agregada — ver schema abaixo).

### `features/auth/schemas/passwordPolicySchema.ts` (novo)

```ts
// Política completa do Cognito (`cognito.tf` de hom/prod), a mesma que
// backend/specs/FEAT-36 já valida via ConfirmForgotPassword. Espelhada
// aqui tanto pro campo "Nova senha" desta feature quanto pro campo de
// senha do cadastro (`registerSchema`) — ver spec.md, decisão 1.
export const passwordPolicySchema = z
  .string()
  .min(8, 'A senha deve ter no mínimo 8 caracteres.')
  .regex(/[A-Z]/, 'A senha deve ter ao menos uma letra maiúscula.')
  .regex(/[a-z]/, 'A senha deve ter ao menos uma letra minúscula.')
  .regex(/[0-9]/, 'A senha deve ter ao menos um número.')
  .regex(/[^A-Za-z0-9]/, 'A senha deve ter ao menos um símbolo.')
```

`registerSchema.ts` troca `password: z.string().min(8, '...')` por
`password: passwordPolicySchema` — único ponto de uso hoje, sem efeito
colateral fora do campo.

### `features/auth/schemas/forgotPasswordSchema.ts` (novo)

```ts
export const forgotPasswordEmailSchema = z.object({
  email: z.string().email('Informe um email válido.'),
})
export type ForgotPasswordEmailData = z.infer<typeof forgotPasswordEmailSchema>

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
```

O código de 6 dígitos do Passo 2/3 reaproveita
`confirmationCodeSchema` (já existe, `confirmationSchema.ts`) sem
mudança — a regra "6 dígitos numéricos" é a mesma nos dois fluxos.

### Hooks novos (`features/auth/hooks/`)

```ts
// useForgotPassword.ts — mesmo formato de useResendConfirmation
function useForgotPassword(): {
  forgotPassword: (payload: ForgotPasswordPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
}

// useResetPassword.ts — mesmo formato de useConfirmAccount
function useResetPassword(): {
  resetPassword: (payload: ResetPasswordPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}
```

`useResendCooldown` é reaproveitado sem nenhuma mudança — já é genérico
("não sabe nada sobre confirmação de conta, só conta segundos", plan.md
da FEAT-31) e serve igualmente ao cooldown do Passo 2/3 deste fluxo.

### Componentes

**`OtpDigitsInput.tsx`** (novo, extraído de `ConfirmationForm.tsx`)

```ts
interface OtpDigitsInputProps {
  digits: string[]                                  // length 6
  disabled: boolean
  onChange: (index: number, value: string) => void
}
```

Componente puramente visual: os 6 `<input>` de 1 dígito, avanço
automático de foco ao digitar, volta de foco com Backspace em campo
vazio, `aria-label` por dígito. Extraído porque `ConfirmationForm`
(FEAT-31) e o novo Passo 2/3 desta feature precisam exatamente do mesmo
grid — sem essa extração, o comportamento de foco (a parte mais
delicada de acertar) seria duplicado por completo. Não sabe nada sobre
submit, cooldown ou qual API está por trás — só digita e avisa o
componente pai via `onChange`. Ref de foco (`inputRefs`) e a lógica de
"focar o primeiro input ao reabilitar" continuam no componente pai (cada
fluxo tem sua própria transição de `disabled`), não neste componente.

**`ConfirmationForm.tsx`** (refatorado, sem mudança de comportamento)

Passa a renderizar `<OtpDigitsInput digits={digits} disabled={isExpired}
onChange={handleDigitChange} />` no lugar do grid inline. Prop pública
(`ConfirmationFormProps`) e todo o resto do comportamento (submit,
cooldown, reenvio) continuam idênticos — nenhum critério de aceite da
FEAT-31 muda; os testes existentes de `ConfirmationForm.test.tsx`
continuam válidos como estão.

**`ForgotPasswordFlow.tsx`** (novo) — orquestra os 3 passos internos:

```ts
type Step = 'email' | 'code' | 'new-password'

interface ForgotPasswordFlowProps {
  onDone: (email: string) => void   // Passo 3 com sucesso
  onBack: () => void                // Passo 1 → "← Voltar ao login"
}

export function ForgotPasswordFlow({ onDone, onBack }: ForgotPasswordFlowProps) {
  const [step, setStep] = useState<Step>('email')
  const [email, setEmail] = useState('')
  const [code, setCode] = useState('')

  if (step === 'email') {
    return <ForgotPasswordEmailStep onSent={(email) => { setEmail(email); setStep('code') }} onBack={onBack} />
  }
  if (step === 'code') {
    return <ForgotPasswordCodeStep email={email} onConfirmed={(code) => { setCode(code); setStep('new-password') }} onBack={() => setStep('email')} />
  }
  return <NewPasswordStep email={email} code={code} onSuccess={() => onDone(email)} onBackToCode={() => setStep('code')} />
}
```

`ForgotPasswordEmailStep`, `ForgotPasswordCodeStep` e `NewPasswordStep`
ficam como funções internas do mesmo arquivo (mesmo padrão de
`LoginModeForm`/`SignupForm` dentro de `LoginForm.tsx`) — nenhuma é
reaproveitada fora deste fluxo.

- **`ForgotPasswordEmailStep`** (Passo 1/3, `24-recuperar-senha.png`):
  `useForm` com `forgotPasswordEmailSchema`; submit chama
  `forgotPassword({ email })`; sucesso (`useForgotPassword().success`
  não existe nesse hook — usa o mesmo padrão de `SignupForm`, com um
  `submittedEmailRef` guardando o email no submit e disparando
  `onSent(submittedEmailRef.current)` via `useEffect` observando
  ausência de erro após `isLoading` cair) avança pro Passo 2/3. "← Voltar
  ao login" chama `onBack()` direto, sem API.
- **`ForgotPasswordCodeStep`** (Passo 2/3, `25-otp-recuperacao.png`):
  mesmo grid (`OtpDigitsInput`) e mesmo `useResendCooldown(60)` da
  `ConfirmationForm`, mas **sem chamar API no submit** — "Confirmar
  código" só valida `confirmationCodeSchema` e chama
  `onConfirmed(code)` localmente (spec.md, decisão 3: não existe
  endpoint de verificação isolada). Contador chegando a zero desabilita
  os campos e troca o botão por "Reenviar e-mail", que chama
  `useForgotPassword().forgotPassword({ email })` de novo (mesmo
  endpoint do Passo 1/3 — reset não tem endpoint de reenvio dedicado),
  limpa os campos e reinicia o cooldown. Sem `autoResendOnEnter` (não
  existe caminho de entrada equivalente ao CTA de login da FEAT-31; o
  Passo 1/3 sempre já disparou o primeiro código). "← Voltar" chama
  `onBack()`, que volta ao Passo 1/3 (spec.md, decisão 2 — diferente da
  FEAT-31).
- **`NewPasswordStep`** (Passo 3/3, `26-nova-senha.png`): `useForm` com
  `newPasswordSchema` (`newPassword`/`confirmNewPassword`, com o
  `.refine` de "senhas não coincidem" resolvido no client antes de
  qualquer chamada). Submit chama `resetPassword({ email, code,
  newPassword })`. Sucesso (`useResetPassword().success`) dispara
  `onSuccess()`. Erro exibido inline a partir de
  `useResetPassword().error`:
  - `WeakPasswordError` ou `NetworkError` → só a mensagem, usuário
    continua no Passo 3/3 pra corrigir e tentar de novo.
  - `InvalidResetCodeError` → mensagem + botão "Voltar e conferir o
    código", que chama `onBackToCode()` (spec.md, decisão 3) —
    diferenciado dos outros dois erros via `error instanceof
    InvalidResetCodeError`, mesmo padrão já usado em `LoginForm.tsx`
    para `AccountNotConfirmedError`.

**`LoginForm.tsx`** (alterado)

```ts
type Screen = 'login' | 'signup' | 'confirmation' | 'forgot-password'

type LoginBanner = { email: string; message: string } | null
const [banner, setBanner] = useState<LoginBanner>(null)
```

- Novo estado `screen === 'forgot-password'` renderiza
  `<ForgotPasswordFlow onDone={(email) => { setBanner({ email, message: 'Senha redefinida. Entre com a nova senha.' }); setScreen('login') }} onBack={() => setScreen('login')} />`,
  ocupando o card inteiro (sem o seletor "Entrar"/"Criar conta"), mesmo
  padrão da tela de confirmação.
- `LoginModeForm` ganha o link "Esqueci minha senha" (abaixo do campo
  "Senha", `27-login-senha-redefinida.png`) → `onForgotPassword()` →
  `setScreen('forgot-password')`, sem chamar API.
- **Refatoração:** `justConfirmedEmail: string | null` (FEAT-31) é
  substituído pelo `banner: LoginBanner` acima — unifica os dois avisos
  de sucesso (email confirmado / senha redefinida) num único slot, já
  que são mutuamente exclusivos (só um caminho leva de volta ao login
  por vez) e têm exatamente a mesma estrutura visual. `ConfirmationForm
  .onConfirmed` passa a chamar `setBanner({ email, message: 'Email
  confirmado. Sua conta está ativa — entre com seus dados.' })` no lugar
  de `setJustConfirmedEmail(email)`. `LoginModeForm` troca a prop
  `justConfirmedEmail` por `banner: LoginBanner`, usando `banner?.email
  ?? ''` como `defaultValues.email` e `banner?.message` no corpo do
  aviso — mesmo bloco visual (ícone de check + texto), sem duplicar
  JSX. Ver "Pontos a confirmar" — esta refatoração toca código já
  entregue e testado na FEAT-31.

## Mapeamento de erros

| Chamada | Status/`type` | Erro tipado | Mensagem exibida |
|---|---|---|---|
| `POST /auth/forgot-password` | `!ok` (qualquer, já que 400/409 de negócio não existem — backend sempre 200) | `UnknownAuthError` | "Ocorreu um erro inesperado. Tente novamente." (já existente) |
| `POST /auth/reset-password` | `400` (`bad-request`) | `WeakPasswordError` | "A senha deve ter no mínimo 8 caracteres, com letra maiúscula, minúscula, número e símbolo." |
| `POST /auth/reset-password` | `400` (`invalid-reset-code`/`expired-reset-code`/outro) | `InvalidResetCodeError` | "Código inválido ou expirado." + botão "Voltar e conferir o código" |
| `POST /auth/reset-password` | outro `!ok` | `UnknownAuthError` | "Ocorreu um erro inesperado. Tente novamente." |
| `POST /auth/forgot-password` / `POST /auth/reset-password` | erro de rede | `NetworkError` | "Não foi possível conectar à API. Verifique sua conexão." |
| Client, submit do Passo 1/3 com email inválido | — (Zod, não chama API) | `forgotPasswordEmailSchema` | "Informe um email válido." |
| Client, submit do Passo 2/3 com código incompleto | — (Zod, não chama API) | `confirmationCodeSchema` (reaproveitado) | "Digite os 6 dígitos do código." |
| Client, submit do Passo 3/3 com senha fora da política | — (Zod, não chama API) | `passwordPolicySchema` | primeira regra que falhar (ex.: "A senha deve ter ao menos um símbolo.") |
| Client, submit do Passo 3/3 com senhas diferentes | — (Zod `.refine`, não chama API) | `newPasswordSchema` | "As senhas não coincidem." |
| Client, submit do cadastro com senha fora da política | — (Zod, não chama API) | `passwordPolicySchema` (reaproveitado em `registerSchema`) | idem acima |

## Recursos AWS

Nenhum. Os dois endpoints (`/auth/forgot-password`,
`/auth/reset-password`) já existem e não mudam de contrato — FEAT-36
(backend) está concluída. Nenhuma variável de ambiente nova, nenhum
recurso Terraform.

## Testes

Mesmo padrão de `ConfirmationForm.test.tsx`/`LoginForm.test.tsx`
(Vitest + RTL + MSW, `server.use` por teste, helper `problem(status,
type)` já existente). Arquivos:

- `LoginForm.test.tsx` — ajustado: teste do banner pós-confirmação
  passa a checar via `banner` (mensagem específica de "Email
  confirmado..."); novo teste do link "Esqueci minha senha" abrindo o
  Passo 1/3; novas constantes `FORGOT_PASSWORD_URL`/`RESET_PASSWORD_URL`
  ao lado das já existentes.
- `ForgotPasswordFlow.test.tsx` (novo) — cobre o fluxo ponta a ponta e
  os pontos de decisão: Passo 1/3 com email inexistente avança igual
  (sem diferença observável, US2), Passo 2/3 avança sem chamar API com
  código completo, submit bloqueado com código incompleto, contador
  zerado + reenvio chamando `forgot-password` de novo, "← Voltar" do
  Passo 2/3 volta ao Passo 1/3 preservando o email, Passo 3/3 com
  senhas diferentes bloqueia no client, sucesso ponta a ponta chama
  `onDone` com o email, erro de código no Passo 3/3 mostra o link de
  volta e, ao clicar, volta ao Passo 2/3, erro de senha fora da
  política mantém o usuário no Passo 3/3, erro de rede em cada chamada.
- `OtpDigitsInput.test.tsx` (novo, se a extração justificar teste
  isolado) — foco automático, Backspace, `disabled` — cobertura que já
  existia indiretamente via `ConfirmationForm.test.tsx`, mantida lá e
  não duplicada aqui a menos que a extração vire ponto de falha
  isolado.
- `registerSchema.test.ts` (existente) — casos novos para a política
  completa de senha (sem maiúscula, sem símbolo etc.), substituindo o
  único caso antigo de "mínimo 8 caracteres".
- `passwordPolicySchema.test.ts` (novo) — cobre as 5 regras
  isoladamente do formulário.

## Decisões técnicas e trade-offs

1. **Extração de `OtpDigitsInput`** em vez de duplicar o grid de 6
   inputs ou parametrizar `ConfirmationForm` com um `purpose: 'signup' |
   'reset'`. A lógica de foco automático é a parte mais fácil de
   introduzir bug reescrevendo; extrair o pedaço puramente visual evita
   duplicação sem acoplar dois fluxos de negócio diferentes (que têm
   submit, resend e navegação distintos) num único componente com
   ramificação interna por `purpose`.
2. **`ForgotPasswordCodeStep` não chama nenhuma API no "Confirmar
   código".** Consequência direta de o backend só ter
   `POST /auth/reset-password` combinando código + senha — não é uma
   escolha de UX, é a única forma de funcionar com o contrato existente
   (spec.md, decisão 3).
3. **Cooldown reinicia em 60s se o usuário voltar do Passo 3/3 pro Passo
   2/3 pelo link de erro.** Como `ForgotPasswordCodeStep` desmonta ao
   sair pro Passo 3/3 e remonta ao voltar, `useResendCooldown` (que
   inicia a contagem no mount) reinicia do zero — diferente da FEAT-31,
   onde o cooldown nunca é resetado por um erro porque o componente
   nunca desmonta durante o fluxo de confirmação. Aceito como trade-off
   simples (elevar o cooldown pro `ForgotPasswordFlow` faria ele começar
   a contar já no Passo 1/3, antes de existir qualquer código enviado,
   o que seria pior) — sinalizado explicitamente pra confirmação abaixo.
4. **Unificação de `justConfirmedEmail` (FEAT-31) em `banner:
   LoginBanner`.** Os dois avisos (email confirmado / senha redefinida)
   têm exatamente a mesma estrutura (email + mensagem + mesmo bloco
   visual) e são mutuamente exclusivos — mantê-los como dois `useState`
   paralelos duplicaria a prop e o JSX do banner em `LoginModeForm`.
   Toca `LoginForm.tsx`, `ConfirmationForm`'s caller e
   `LoginForm.test.tsx`, todos já cobertos por teste.
5. **`passwordPolicySchema` como arquivo próprio** (não inline em
   `registerSchema.ts` nem em `forgotPasswordSchema.ts`) — evita
   dependência cruzada entre os dois arquivos de schema e deixa claro
   que a regra é compartilhada, não específica de nenhum dos dois
   formulários que a usam.

## Pontos a confirmar antes do `/tasks`

- **Decisão 3 (cooldown reinicia ao voltar do Passo 3/3 pro Passo
  2/3):** aceitável, ou prefere que o cooldown persista entre os passos
  (exigiria levantar `useResendCooldown` pro `ForgotPasswordFlow` e só
  "ativá-lo" a partir da entrada no Passo 2/3, com um pouco mais de
  código pra evitar que ele comece a contar já no Passo 1/3)?
- **Decisão 4 (unificar `justConfirmedEmail` em `banner: LoginBanner`):**
  ok em tocar esse trecho já entregue da FEAT-31, ou prefere manter
  `justConfirmedEmail` como está e adicionar um `passwordResetEmail`
  paralelo (mais duplicação, porém zero risco sobre código já
  validado)?
- **Nome dos componentes/arquivos novos:** `ForgotPasswordFlow`,
  `OtpDigitsInput` — ok, ou prefere outros nomes?
