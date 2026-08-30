# Plan — FEAT-21: Cadastro real

## Camadas afetadas

Só `frontend/app/src/features/auth/` e as rotas que dependem dela —
nenhuma outra feature é tocada. Segue a organização já estabelecida
(feature-based, `api/` sem camada extra, `errors/` tipados,
`utils/` de formatação pura testada isoladamente — mesmo padrão de
`features/expenses/utils/currency.ts`).

| Camada | O que muda |
|---|---|
| `schemas/` | `signupSchema.ts` é **substituído** por `registerSchema.ts` (campos novos + validação espelhando o backend) |
| `utils/` (novo) | `phoneMask.ts` (máscara + extração de dígitos) e `cpf.ts` (máscara + extração de dígitos + validação de dígito verificador) |
| `api/` | `authApi.ts` ganha `register()`; `login()` passa a expor o `type` do erro 401 (não só o status) |
| `errors/` | `authErrors.ts` ganha `EmailAlreadyExistsError`, `CpfAlreadyExistsError`, `RegisterValidationError`, `AccountPendingApprovalError`; `InvalidCredentialsError` continua só para `invalid-credentials` |
| `hooks/` | `useLogin.ts` mapeia o novo `AccountPendingApprovalError` (sem mudar assinatura); novo `useRegister.ts` |
| `components/` | `SignupForm` (dentro de `LoginForm.tsx`) reescrito: campos novos, máscaras, chamada real, estado de sucesso |
| `routes/` | `SignupComingSoonPage.tsx` (+ teste) removida; `app/router.tsx` perde a rota `/cadastro-em-breve` |

Nenhuma mudança em `lib/httpClient.ts` — `/auth/register` não precisa de
tratamento especial (não está em `AUTH_INTERCEPTOR_EXCLUDED_PATHS`,
mas também não deveria disparar refresh: como o cadastro nunca roda
autenticado, não há token pra mandar, então o interceptor de 401 é
inofensivo — um 401 nesta rota nunca acontece hoje, e se acontecesse
cairia no fallback padrão sem token, sem refresh, sem loop).

## Contratos técnicos

### `authApi.register`

```ts
interface RegisterPayload {
  email: string
  password: string
  name: string
  phoneNumber: string // só dígitos, 10-11 chars — já normalizado pelo form
  cpf: string          // só dígitos, 11 chars — já normalizado pelo form
}

interface RegisterResponse {
  userId: string
  email: string
  name: string
  phoneNumber: string
  cpf: string
}

async function register(payload: RegisterPayload): Promise<RegisterResponse>
```

Implementação segue o mesmo formato de `login`/`me` (`safeFetch` +
`assertOk` adaptado): distingue por status/`type` do corpo:

```ts
async function register(payload: RegisterPayload): Promise<RegisterResponse> {
  const response = await safeFetch(() => httpClient.post('/auth/register', payload))
  if (!response.ok) {
    const problem = await response.json().catch(() => null)
    if (response.status === 409 && problem?.type?.endsWith('email-already-exists')) {
      throw new EmailAlreadyExistsError()
    }
    if (response.status === 409 && problem?.type?.endsWith('cpf-already-exists')) {
      throw new CpfAlreadyExistsError()
    }
    if (response.status === 400) {
      throw new RegisterValidationError()
    }
    throw new UnknownAuthError()
  }
  return response.json() as Promise<RegisterResponse>
}
```

### `authApi.login` — leitura do `type` no 401

Hoje `assertOk` lança `InvalidCredentialsError` para **qualquer** 401.
Passa a inspecionar o corpo (RFC 9457) só no caminho de erro — sem
mudar o formato de retorno de sucesso:

```ts
async function login(credentials: LoginCredentials): Promise<LoginResponse> {
  const response = await safeFetch(() => httpClient.post('/auth/login', credentials))
  if (response.status === 401) {
    const problem = await response.json().catch(() => null)
    if (problem?.type?.endsWith('user-not-confirmed')) {
      throw new AccountPendingApprovalError()
    }
    throw new InvalidCredentialsError()
  }
  if (!response.ok) {
    throw new UnknownAuthError()
  }
  return response.json() as Promise<LoginResponse>
}
```

`.endsWith(...)` em vez de igualdade exata porque o `type` é uma URL
completa (`https://gastosapp.dev/errors/user-not-confirmed`) — mesmo
padrão que `categoriesWriteApi` já usa pra ler `problem.type` (ver
`features/categories/api/categoriesWriteApi.ts`).

### Erros novos (`errors/authErrors.ts`)

```ts
export class EmailAlreadyExistsError extends Error {
  constructor() {
    super('Este email já está cadastrado.')
    this.name = 'EmailAlreadyExistsError'
  }
}

export class CpfAlreadyExistsError extends Error {
  constructor() {
    super('Este CPF já está cadastrado.')
    this.name = 'CpfAlreadyExistsError'
  }
}

export class RegisterValidationError extends Error {
  constructor() {
    super('Não foi possível concluir o cadastro. Verifique os dados informados.')
    this.name = 'RegisterValidationError'
  }
}

export class AccountPendingApprovalError extends Error {
  constructor() {
    super('Sua conta ainda não foi aprovada. Aguarde a confirmação do administrador e tente novamente.')
    this.name = 'AccountPendingApprovalError'
  }
}
```

### `utils/cpf.ts` (novo)

```ts
export function maskCpf(digits: string): string       // "12345678909" -> "123.456.789-09" (progressivo, até 11 dígitos)
export function extractDigits(value: string): string  // remove tudo que não é dígito, limita a `maxLen`
export function isValidCpf(digits: string): boolean    // algoritmo oficial de dígito verificador + rejeita sequência repetida
```

`isValidCpf` replica exatamente a regra de
`backend/src/GastosApp.Domain/Users/Cpf.cs` (mesmo algoritmo, mesma
rejeição de `00000000000`...`99999999999`) — validação client-side é
só UX antecipada, a fonte de verdade continua o backend.

### `utils/phoneMask.ts` (novo)

```ts
export function maskPhone(digits: string): string // "11999998888" -> "(11) 99999-8888" (progressivo, 10 ou 11 dígitos)
```

Reaproveita `extractDigits` de `utils/cpf.ts` (ou um `utils/digits.ts`
compartilhado entre os dois — decisão de nomeação fica pro `/tasks`,
sem impacto de contrato).

### `schemas/registerSchema.ts` (substitui `signupSchema.ts`)

```ts
export const registerSchema = z.object({
  name: z.string().trim().min(2, '...').max(150, '...'),
  phoneDigits: z.string().regex(/^\d{10,11}$/, 'Telefone deve ter 10 ou 11 dígitos.'),
  cpfDigits: z.string().regex(/^\d{11}$/, '...').refine(isValidCpf, 'CPF inválido.'),
  email: z.string().email('Informe um email válido.'),
  password: z.string().min(8, 'A senha deve ter no mínimo 8 caracteres.'),
})
```

Os campos armazenam **só dígitos** no estado do formulário
(`phoneDigits`/`cpfDigits`); a máscara é aplicada na exibição do
`<input>` (`value={maskPhone(phoneDigits)}`), com o `onChange`
extraindo dígitos do valor digitado antes de gravar no form state —
mesmo padrão já usado por `features/expenses/utils/currency.ts` +
`ExpenseForm` pro campo de valor monetário (formata pra exibição,
guarda o dado cru).

## Decisões técnicas

1. **Sem toast/overlay de processamento novos.** O design mostra um
   overlay de processamento (`04-login-processando.png`) e toasts
   (`09-toast-despesa-lancada.png`), mas **nenhum dos dois existe hoje
   no código** — só o padrão já usado em `LoginModeForm`/`SignupForm`
   (botão com label em gerúndio + `disabled` durante o loading, erro
   inline com `role="alert"`). Introduzir um componente de toast
   genérico é decisão maior de UI compartilhada, fora do escopo desta
   feature (fica como sugestão de débito técnico ao final, não decidido
   aqui). A confirmação de "conta criada, pendente de aprovação" usa o
   **mesmo padrão inline** já existente (um `<p>` de sucesso, mesma
   posição onde hoje aparece o erro), só que com token de cor neutro/
   verde do Modernist em vez do `--color-accent-700` de erro.
2. **`registerSchema` substitui `signupSchema`** em vez de conviver com
   ele — o formulário fake não existe mais depois desta feature, então
   não há motivo pra manter os dois.
3. **Validação de CPF vive só no frontend, duplicada da regra do
   backend** (não há pacote compartilhado entre os dois contextos,
   por design do monorepo — ver `/CLAUDE.md` raiz, "não existe
   infraestrutura compartilhada entre contextos"). Duplicação aceita
   conscientemente, mesmo trade-off que qualquer validação client-side
   espelhando regra de servidor.
4. **Sem estado de "processando" com overlay de tela cheia** — o botão
   ocupado (spinner + label + `disabled`) já é suficiente e é o padrão
   atual do projeto; overlay fica pro dia em que ele for
   introduzido de fato (débito técnico, ver abaixo).
5. **`me()` (GET /auth/me) não muda nesta feature** — já retorna `name`
   desde antes; `phoneNumber`/`cpf` no retorno de `me()` não são usados
   por nenhuma tela ainda (perfil/edição é FEAT-26 backend, "fora do
   escopo" também lá). Fica de fora daqui.

## Recursos AWS

Nenhum. Consome endpoints já publicados (`POST /auth/register`, `POST
/auth/login`), sem infraestrutura nova.

## Mapeamento de erros

| Origem | Condição | Exceção lançada | Mensagem exibida |
|---|---|---|---|
| `POST /auth/register` | rede indisponível | `NetworkError` (já existe) | "Não foi possível conectar à API..." |
| `POST /auth/register` | 400 `validation-error` | `RegisterValidationError` (novo) | "Não foi possível concluir o cadastro. Verifique os dados informados." |
| `POST /auth/register` | 409 `email-already-exists` | `EmailAlreadyExistsError` (novo) | "Este email já está cadastrado." |
| `POST /auth/register` | 409 `cpf-already-exists` | `CpfAlreadyExistsError` (novo) | "Este CPF já está cadastrado." |
| `POST /auth/register` | outro erro (inclui 500) | `UnknownAuthError` (já existe) | "Ocorreu um erro inesperado. Tente novamente." |
| `POST /auth/login` | 401 `user-not-confirmed` | `AccountPendingApprovalError` (novo) | "Sua conta ainda não foi aprovada..." |
| `POST /auth/login` | 401 `invalid-credentials` (ou `type` ausente) | `InvalidCredentialsError` (já existe, sem mudança) | "Email ou senha inválidos." |

## Débitos técnicos identificados (perguntar ao usuário antes de anotar)

Durante este `/plan` surgiram dois itens fora do escopo da FEAT-21 que
o design já assume em telas futuras do backlog:

1. Um **componente de toast genérico** (Modernist) — usado em pelo
   menos 3 telas do design (`09-toast-despesa-lancada.png`,
   `17-toast-convite-enviado.png`, e o próprio cadastro, se quisermos
   ficar 100% fiel ao design em vez do padrão inline atual).
2. Um **overlay de processamento de tela cheia** para ações
   assíncronas mais longas (`04-login-processando.png`,
   `08-salvando-despesa-loading.png`, `16-enviando-convite-loading.png`).

Ambos aparecem repetidamente no backlog (FEAT-24 nova receita, FEAT-28
membros/convite) — pode valer a pena resolver uma vez, de forma
genérica, antes dessas FEATs, em vez de cada uma reinventar. Registro
aqui só para decisão do usuário — não vira item do `backlog.md` sem
confirmação.

## Pontos confirmados com o usuário

1. **Erro 400 genérico** — confirmado: `RegisterValidationError` fica
   com mensagem genérica ("Verifique os dados informados"), sem tentar
   mapear pra um campo específico. O backend não retorna código por
   campo, e a validação client-side já cobre os casos comuns antes do
   submit.
2. **Sem toast/overlay novos nesta feature** — confirmado: usa o padrão
   inline já existente (mensagem de sucesso/erro + botão com spinner).
   Os dois itens (toast genérico, overlay de processamento) foram
   registrados em `frontend/docs/backlog.md` como débitos técnicos,
   para revisitar antes das FEATs que também assumem esses componentes
   no design (FEAT-24, FEAT-28).
3. Nomenclatura `utils/cpf.ts` + `utils/phoneMask.ts` fica em aberto,
   decidida no `/tasks` sem impacto de contrato.
