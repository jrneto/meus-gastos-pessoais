# Plan — FEAT-31: Confirmação de cadastro via código (OTP)

## Camadas afetadas

Só `features/auth/` (feature-based, sem camadas em sentido Clean
Architecture). Nenhuma rota nova, nenhum recurso compartilhado em
`components/`/`lib/` — tudo fica dentro da feature `auth`, que já existe.

| Pasta | Muda? | O quê |
|---|---|---|
| `features/auth/api/authApi.ts` | Sim | `confirm` e `resendConfirmation` |
| `features/auth/errors/authErrors.ts` | Sim | novo erro + rename de um existente |
| `features/auth/schemas/` | Sim | novo `confirmationSchema.ts` |
| `features/auth/hooks/` | Sim | 3 hooks novos |
| `features/auth/components/` | Sim | `LoginForm.tsx` refeito (máquina de 3 telas) + `ConfirmationForm.tsx` novo |
| `routes/LoginPage.tsx` | Não | continua só o wrapper (`.ds-modernist`, logo, redireciona se autenticado) — nenhuma mudança de contrato com `LoginForm` |
| `app/router.tsx` | Não | nenhuma rota nova — a tela de confirmação é um terceiro estado interno de `LoginForm`, mesmo padrão já usado para `login`/`signup` (não uma URL própria) |

## Contratos técnicos

### `features/auth/api/authApi.ts`

```ts
export interface ConfirmPayload {
  email: string
  code: string
}

export interface ResendConfirmationPayload {
  email: string
}

async function confirm(payload: ConfirmPayload): Promise<void>
async function resendConfirmation(payload: ResendConfirmationPayload): Promise<void>

export const authApi = { login, register, me, refresh, logout, confirm, resendConfirmation }
```

- `confirm`: `POST /auth/confirm`. `200` → resolve sem valor (sem corpo,
  igual `logout`). `400` (qualquer `type` — `invalid-confirmation-code`
  ou `expired-confirmation-code`, ver spec "decisão 3") →
  `InvalidConfirmationCodeError`. Qualquer outro `!response.ok` →
  `UnknownAuthError`. Erro de rede → `NetworkError` (via `safeFetch`,
  já existente).
- `resendConfirmation`: `POST /auth/resend-confirmation`. Backend
  **sempre** retorna 200 (spec FEAT-35, decisão 3) — não há `400`/`409`
  de negócio a mapear aqui, só o caminho de falha técnica
  (`!response.ok` → `UnknownAuthError`; erro de rede → `NetworkError`).

### `features/auth/errors/authErrors.ts`

- **Novo:** `InvalidConfirmationCodeError` — mensagem "Código inválido
  ou expirado. Confira o email ou solicite um novo código."
- **Rename:** `AccountPendingApprovalError` → `AccountNotConfirmedError`.
  O nome atual descreve um conceito que deixa de existir nesta feature
  (aprovação manual de administrador) — mantê-lo confundiria o próximo
  desenvolvedor a ler o código. Mensagem passa a ser "Confirme seu
  cadastro pelo código enviado por e-mail antes de entrar." Único
  ponto de uso hoje é `LoginForm.tsx`/`authApi.ts`/os dois arquivos de
  teste correspondentes — rename sem efeito colateral fora da feature.

### `features/auth/schemas/confirmationSchema.ts` (novo)

```ts
export const confirmationCodeSchema = z
  .string()
  .regex(/^\d{6}$/, 'Digite os 6 dígitos do código.')
```

Não é um schema de formulário React Hook Form inteiro (a UI é 6 inputs
de 1 dígito cada, não um único campo de texto) — mas a regra "6 dígitos
numéricos" ainda passa por Zod como fonte única de verdade (constitution:
"Toda validação de schema usa Zod"), validada via `.safeParse()` no
submit antes de chamar a API, em vez de bloquear o botão dinamicamente.

### Hooks novos (`features/auth/hooks/`)

```ts
// useConfirmAccount.ts — mesmo formato de useRegister/useLogin
function useConfirmAccount(): {
  confirm: (payload: ConfirmPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

// useResendConfirmation.ts
function useResendConfirmation(): {
  resend: (payload: ResendConfirmationPayload) => Promise<void>
  isLoading: boolean
  error: Error | null
}

// useResendCooldown.ts — só UI, sem chamada de API
function useResendCooldown(initialSeconds = 60): {
  secondsLeft: number      // conta regressiva de initialSeconds até 0
  isExpired: boolean       // secondsLeft === 0
  restart: () => void      // volta pra initialSeconds e reinicia o interval
}
```

`useResendCooldown` isola o `setInterval`/`clearInterval` (mesmo padrão
de limpeza no unmount que o protótipo faz em `componentWillUnmount`),
puro e testável com `vi.useFakeTimers()`, sem depender de rede — não
sabe nada sobre confirmação de conta, só conta segundos. Fica em
`features/auth/hooks/` (não em `lib/` compartilhado) porque hoje só a
tela de confirmação usa; sobe pra `lib/` se uma segunda feature
(ex.: recuperação de senha, fora do escopo) precisar do mesmo padrão.

### Componentes

**`ConfirmationForm.tsx`** (novo, `features/auth/components/`)

Props:
```ts
interface ConfirmationFormProps {
  email: string
  autoResendOnEnter: boolean   // true quando entrou pelo CTA do login (decisão 1 da spec)
  onConfirmed: (email: string) => void
  onBack: () => void
}
```

Estado interno: `digits: string[6]` (um dígito por posição, refs pra
foco), `clientError: string | null` (erro de validação local — "Digite
os 6 dígitos do código.", nunca vem da API). O erro vindo da API
(`InvalidConfirmationCodeError`/`NetworkError`) usa o `error` do
`useConfirmAccount`, exibido no mesmo slot visual — só um dos dois
existe por vez (submit com Zod inválido nem chega a chamar a API).

Comportamento:
- Ao montar: se `autoResendOnEnter`, chama `resend({ email })`
  imediatamente (dispara o primeiro código pra quem entrou via login,
  já que nesse caminho não houve nenhum `register` recente disparando o
  email automaticamente) — cobre US8 da spec.
- Digitar um dígito avança o foco pro próximo input; `Backspace` num
  input vazio volta o foco pro anterior. Cada input aceita só 1
  caractere numérico (`maxLength={1}`, diferente do protótipo original
  que usa `maxlength="6"` em cada campo — correção de bug de
  transcrição do `.dc.html`, não uma escolha de design).
- Submit: junta os dígitos, valida com `confirmationCodeSchema`; se
  inválido, seta `clientError` e não chama a API (US6). Se válido,
  chama `confirm({ email, code })`; sucesso → `onConfirmed(email)`.
- Erro de `confirm` (400) exibe a mensagem de `InvalidConfirmationCodeError`
  **sem** limpar `digits` e **sem** reiniciar `useResendCooldown` (US3) —
  o cooldown segue seu próprio ciclo, independente do resultado do submit.
- `secondsLeft > 0`: mostra contador (`M:SS`) rotulado como cooldown de
  reenvio + botão "Confirmar código" (spinner/"Confirmando…" enquanto
  `isLoading` do `useConfirmAccount`).
- `secondsLeft === 0` (`isExpired`): desabilita os 6 inputs, troca o
  botão por "Reenviar e-mail" → ao clicar, chama `resend({ email })`;
  sucesso limpa `digits`, foca o primeiro input, limpa `clientError`/
  `error` de confirmação e chama `restart()` do cooldown (US4/US5).
- "← Voltar" chama `onBack()` direto, sem chamar nenhuma API (US9,
  decisão 4 da spec — diferente do protótipo, que voltava pro modo
  "Criar conta").
- Acessibilidade: cada input do código recebe `aria-label` própria
  (`"Dígito 1 do código"` … `"Dígito 6 do código"` — o design não tem
  `<label>` visível por campo, só o grid de 6 caixas) e o container de
  erro usa `role="alert"`, mesmo padrão já usado no resto do `LoginForm`.

**`LoginForm.tsx`** (reescrito)

Troca o atual `authMode: 'login' | 'signup'` por uma máquina de 3
telas:

```ts
type Screen = 'login' | 'signup' | 'confirmation'

const [screen, setScreen] = useState<Screen>('login')
const [confirmationEmail, setConfirmationEmail] = useState('')
const [autoResendOnEnter, setAutoResendOnEnter] = useState(false)
const [justConfirmedEmail, setJustConfirmedEmail] = useState<string | null>(null)
```

Transições:
- `SignupForm` (sucesso do cadastro) → `onRegistered(email)`:
  `setConfirmationEmail(email); setAutoResendOnEnter(false); setScreen('confirmation')`
  (US1 — o próprio `register` já disparou o primeiro código via
  Cognito, não precisa de resend automático aqui).
- `LoginModeForm` (401 `user-not-confirmed`, clique em "Confirmar
  cadastro") → `onNeedsConfirmation(email)`:
  `setConfirmationEmail(email); setAutoResendOnEnter(true); setScreen('confirmation')`
  (US7/US8).
- `ConfirmationForm.onConfirmed(email)`:
  `setJustConfirmedEmail(email); setScreen('login')` (US2).
- `ConfirmationForm.onBack()`: `setScreen('login')` (US9).

O toggle "Entrar"/"Criar conta" (`seg`) só é renderizado quando
`screen !== 'confirmation'` — a tela de confirmação ocupa o card
inteiro, sem o seletor de modo (mesmo layout do protótipo,
`isOtp` esconde o `seg` equivalente).

`LoginModeForm` ganha:
- prop `justConfirmedEmail: string | null` → `defaultValues` do
  `useForm` (`email: justConfirmedEmail ?? ''`) e renderiza o banner
  "Email confirmado. Sua conta está ativa — entre com seus dados."
  quando não-nulo (US2, mesma copy do design,
  `23-login-email-confirmado.png`).
- prop `onNeedsConfirmation: (email: string) => void` — o botão
  "Confirmar cadastro" (novo, só aparece junto do erro
  `AccountNotConfirmedError`) chama `onNeedsConfirmation(watch('email'))`
  (precisa de `watch` do RHF pra pegar o email já digitado no campo).

`SignupForm` ganha prop `onRegistered: (email: string) => void` no
lugar do atual `onDone: () => void` — a tela de "Conta criada! Aguarde
a aprovação..." (com o botão "Voltar para o login") é removida por
completo; sucesso do `useRegister` navega direto pra
`ConfirmationForm`, sem tela intermediária.

## Mapeamento de erros

| Chamada | Status/`type` | Erro tipado | Mensagem exibida |
|---|---|---|---|
| `POST /auth/confirm` | `400` (`invalid-confirmation-code` ou `expired-confirmation-code`) | `InvalidConfirmationCodeError` | "Código inválido ou expirado. Confira o email ou solicite um novo código." |
| `POST /auth/confirm` | outro `!ok` | `UnknownAuthError` | "Ocorreu um erro inesperado. Tente novamente." (já existente) |
| `POST /auth/confirm` / `POST /auth/resend-confirmation` | erro de rede | `NetworkError` | "Não foi possível conectar à API. Verifique sua conexão." (já existente) |
| `POST /auth/resend-confirmation` | `!ok` (qualquer, já que 400/409 de negócio não existem nesse endpoint) | `UnknownAuthError` | idem acima |
| `POST /auth/login` | `401` `user-not-confirmed` | `AccountNotConfirmedError` (rename) | "Confirme seu cadastro pelo código enviado por e-mail antes de entrar." + botão "Confirmar cadastro" |
| Client, submit com código incompleto | — (não chega a chamar API) | `confirmationCodeSchema` (Zod) | "Digite os 6 dígitos do código." |

## Recursos AWS

Nenhum. Os três endpoints (`/auth/confirm`, `/auth/resend-confirmation`,
`/auth/login`) já existem e não mudam de contrato — FEAT-35 (backend)
está concluída. Nenhuma variável de ambiente nova, nenhum recurso
Terraform.

## Testes

Mesmo padrão de `LoginForm.test.tsx` (Vitest + RTL + MSW, `server.use`
por teste, `problem(status, type)` helper já existente). Dois arquivos:

- `LoginForm.test.tsx` — testes existentes de login/signup ajustados
  (a mensagem/tela pós-cadastro muda de "Aguarde a aprovação..." pra
  navegar pra confirmação; o teste de `user-not-confirmed` passa a
  checar a nova mensagem + botão "Confirmar cadastro"; teste de
  "voltar da confirmação" se move pro novo arquivo). Novas constantes
  `CONFIRM_URL`/`RESEND_URL` ao lado de `LOGIN_URL`/`REGISTER_URL`.
- `ConfirmationForm.test.tsx` (novo) — cobre isoladamente: preencher os
  6 dígitos com avanço de foco automático, Backspace voltando o foco,
  submit bloqueado com código incompleto (client, API não chamada),
  código correto chama `onConfirmed`, código incorreto mostra erro sem
  limpar dígitos nem reiniciar o contador (`vi.useFakeTimers()` pra
  avançar o relógio e checar que o `secondsLeft` exibido não voltou a
  60), contador chegando a zero desabilita os inputs e troca pro botão
  de reenvio, reenvio limpa/reabilita os campos e reinicia o contador,
  `autoResendOnEnter=true` dispara `resend` no mount sem interação do
  usuário, "← Voltar" chama `onBack` sem chamar nenhuma API, erro de
  rede em ambas as chamadas.
- `useResendCooldown.test.ts` (novo, se o hook for extraído como
  testável isoladamente) — cobre a contagem regressiva e o `restart()`
  com `vi.useFakeTimers()`, sem precisar montar componente.

## Decisões técnicas e trade-offs

1. **Sem rota nova.** A tela de confirmação continua como um terceiro
   estado de componente dentro de `LoginForm`, replicando a decisão já
   tomada nas FEAT-14/21 (login/signup também não têm rotas próprias).
   Trade-off aceito: F5 na tela de confirmação perde o estado e volta
   pro login (já coberto em "Fora do escopo" da spec).
2. **Rename `AccountPendingApprovalError` → `AccountNotConfirmedError`.**
   Aproveita que os únicos 4 arquivos que tocam essa classe já estão
   sendo alterados nesta feature — adiar o rename só acumularia uma
   inconsistência de nome que confundiria leitura futura.
3. **`useResendCooldown` como hook próprio, não estado inline no
   componente.** Isola o `setInterval` (fácil de esquecer o cleanup) e
   fica testável sem montar UI — mesmo racional de `useAuthSession`/
   `useSessionBootstrap` já existentes na feature.
4. **Correção do `maxLength` por dígito** (`1`, não `6` como no
   protótipo `.dc.html`) — bug de transcrição do protótipo, não uma
   escolha de design; sinalizar no design system seria um débito à
   parte (perguntar ao usuário se quer registrar, não decidido aqui).
5. **`confirmationCodeSchema` como Zod solto (`.safeParse` manual),
   não um `useForm` completo** — os 6 inputs de dígito não mapeiam bem
   pro modelo padrão de campo único do RHF; manter Zod como fonte da
   regra "6 dígitos" ainda cumpre a regra da constitution sem forçar um
   formulário RHF artificial.

## Pontos a confirmar antes do `/tasks`

- Nome do componente novo: `ConfirmationForm` (proposto) — ok, ou
  prefere algo como `OtpForm`/`ConfirmAccountForm`?
- Rename de `AccountPendingApprovalError` pra `AccountNotConfirmedError`
  (decisão 2 acima) — confirma o rename ou prefere manter o nome atual
  e só trocar a mensagem?
- Bug do `maxLength="6"` por dígito no protótipo (decisão 4): corrijo
  silenciosamente na implementação (é claramente um bug, não uma
  escolha), ou registra como item de ajuste no design system
  (`frontend/design-system/`) separadamente?
