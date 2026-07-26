# Plan — FEAT-01: Setup inicial do frontend + tela de login

Referência: [`spec.md`](./spec.md). Decisão de armazenamento de token
(memória, não `localStorage`) já validada com o usuário — justificativa
completa nas seções abaixo.

`frontend/docs/constitution.md` e `frontend/CLAUDE.md` ainda não existem
(este é o primeiro FEAT do contexto). Este plan.md estabelece a baseline
arquitetural que vira `frontend/CLAUDE.md`.

## Stack e ferramentas

| Camada | Escolha |
|---|---|
| Build tool | Vite + React + TypeScript |
| Roteamento | `react-router-dom` |
| Estado global (sessão) | Zustand (store em memória, sem `persist` middleware) |
| Formulário | React Hook Form |
| Validação de schema | Zod (`zodResolver`) |
| UI | shadcn/ui + Tailwind CSS |
| Gráficos | Tremor — **não instalado neste FEAT**; adicionar como dependência apenas na primeira feature que renderizar um gráfico |
| Testes | Vitest + React Testing Library + MSW (mock da API HTTP) |
| HTTP client | `fetch` nativo, encapsulado (sem Axios — evita dependência extra sem necessidade clara) |
| Package manager | npm |

## Estrutura de pastas: Feature-based (padrão "Bulletproof React")

Decisão revista com o usuário: Clean Architecture em camadas
(domain/application/infrastructure/presentation) foi descartada por
adicionar complexidade desnecessária para o tamanho do projeto. Em vez
disso, adotamos organização **por feature**, o padrão de fato mais
usado hoje em produção no ecossistema React (referência:
[bulletproof-react](https://github.com/alan2207/bulletproof-react)) —
cada feature de negócio (auth, expenses, etc.) tem sua própria pasta
com api/components/hooks/schemas/store, e só o que é genuinamente
compartilhado entre features vive fora delas.

Projeto Vite isolado em `frontend/app/` (não na raiz de `frontend/`) —
mantém paralelismo com o backend, onde `backend/src/` guarda o código e
`docs/`, `specs/`, `infra/` ficam ao lado, fora da árvore de código:

```
frontend/
├── CLAUDE.md
├── README.md
├── docs/
├── specs/
├── infra/
└── app/                            # projeto Vite (package.json aqui)
    ├── src/
    │   ├── app/                    # bootstrap: main.tsx, App.tsx, providers, router
    │   ├── routes/                 # páginas (rotas), compõem componentes das features
    │   │   ├── LoginPage.tsx
    │   │   └── HomePage.tsx        # placeholder pós-login
    │   ├── features/
    │   │   └── auth/
    │   │       ├── api/
    │   │       │   └── authApi.ts         # login(), me() — chamadas HTTP via lib/httpClient
    │   │       ├── components/
    │   │       │   └── LoginForm.tsx      # RHF + zodResolver + useLogin
    │   │       ├── hooks/
    │   │       │   ├── useLogin.ts
    │   │       │   └── useAuthSession.ts
    │   │       ├── schemas/
    │   │       │   └── loginSchema.ts     # Zod (email + senha ≥ 8)
    │   │       ├── store/
    │   │       │   └── authStore.ts       # Zustand: token, userId, expiresAt
    │   │       └── errors/
    │   │           └── authErrors.ts      # InvalidCredentialsError, NetworkError, etc.
    │   ├── components/
    │   │   ├── ui/                 # shadcn/ui (gerado via CLI) — compartilhado entre features
    │   │   └── ProtectedRoute.tsx  # compartilhado (não pertence só à feature auth)
    │   └── lib/
    │       └── httpClient.ts       # fetch wrapper, usa import.meta.env.VITE_API_BASE_URL
    ├── .env.development             # VITE_API_BASE_URL apontando pra API local (não versionado)
    ├── .env.production              # VITE_API_BASE_URL da API AWS (placeholder, não versionado)
    ├── .env.example                 # documenta as variáveis, sem valores reais (versionado)
    └── vite.config.ts
```

Regra de dependência (simples, não uma cadeia formal de camadas):
`features/*` pode depender de `lib/` e `components/ui/`; `lib/` e
`components/ui/` nunca dependem de `features/*`. Um `ProtectedRoute`
usado por mais de uma feature no futuro vive em `components/`, não
dentro de `features/auth/`. Quando uma segunda feature de negócio
existir (ex.: `features/expenses/`), qualquer coisa reaproveitada por
ambas sobe para `lib/`/`components/` — não fica duplicada nem uma
feature importa de dentro da outra.

## Contratos técnicos

Caminhos abaixo relativos a `frontend/app/src/`.

### `features/auth/schemas/loginSchema.ts`
```ts
export const loginSchema = z.object({
  email: z.string().email(),
  password: z.string().min(8),
})
export type LoginCredentials = z.infer<typeof loginSchema>
```
Fonte única da regra "email válido + senha ≥ 8", usada pelo
`zodResolver` do `LoginForm` — espelha a regra já aplicada no backend.

### `features/auth/errors/authErrors.ts`
```ts
export class InvalidCredentialsError extends Error {}
export class NetworkError extends Error {}
export class UnknownAuthError extends Error {}
```
Mapeamento de resposta HTTP → erro tipado, feito em `authApi.ts`:
- 401 → `InvalidCredentialsError` → `LoginForm` exibe mensagem amigável
  ("Email ou senha inválidos")
- Falha de rede/timeout → `NetworkError` → mensagem genérica de
  conectividade
- Qualquer outro status (5xx, parsing inesperado) → `UnknownAuthError`
  → mensagem genérica de erro

Não há uma classe `ValidationError` separada: como o `LoginForm` só
chama a API depois de passar pelo `loginSchema`, um 400 da API seria
sempre inesperado — tratado como `UnknownAuthError` mesmo, sem
modelagem especial para um caso que não deveria ocorrer.

### `features/auth/api/authApi.ts`
```ts
async function login(credentials: LoginCredentials): Promise<{ accessToken: string; expiresIn: number; userId: string }>
async function me(token: string): Promise<{ userId: string; email: string; name: string }>
```
Chama `POST {VITE_API_BASE_URL}/auth/login` e
`GET {VITE_API_BASE_URL}/auth/me` (header `Authorization: Bearer <token>`)
via `lib/httpClient.ts`; mapeia status HTTP para os erros de
`authErrors.ts`. Funções simples, sem interface/abstração de
repositório — os testes mockam no nível de rede (MSW), não trocando
implementação por injeção de dependência.

### `features/auth/store/authStore.ts` (Zustand)
```ts
interface AuthState {
  token: string | null
  userId: string | null
  expiresAt: number | null   // Date.now() + expiresIn*1000, calculado no login
  setSession: (token: string, userId: string, expiresIn: number) => void
  clearSession: () => void
}
```
Sem `persist` middleware — estado vive só em memória, some ao recarregar
a página (comportamento aceito no MVP, ver `spec.md` § Fora do escopo:
refresh token).

### `features/auth/hooks/useLogin.ts`
Hook simples (sem lib de data-fetching adicional): chama `authApi.login`,
gerencia `isLoading`/`error` locais via `useState`, e em caso de sucesso
chama `authStore.setSession`. `LoginForm` usa esse hook no `onSubmit`.

### `features/auth/hooks/useAuthSession.ts`
Deriva `isAuthenticated` do store: `token !== null && Date.now() < expiresAt`.
Checado sob demanda (ex.: ao montar `ProtectedRoute`, ao navegar) — **sem
timer em background**, já que sem refresh token não há nada de útil a
fazer ao detectar expiração antecipadamente além de esperar a próxima
navegação/render. Reavaliar quando o refresh token existir.

### `components/ProtectedRoute.tsx`
Componente wrapper de rota: se `!isAuthenticated` (via `useAuthSession`),
`<Navigate to="/login" replace />`; caso contrário, renderiza
`children`/`<Outlet />`. Fica em `components/` (não em `features/auth/`)
porque é infraestrutura de roteamento reaproveitável por qualquer
feature que precisar de rota protegida, não uma regra do domínio de
autenticação em si.

## Configuração de ambiente (local vs produção)

- Vite lê variáveis prefixadas `VITE_` de `.env.{mode}` automaticamente
  via `import.meta.env`.
- `.env.development` (usado em `npm run dev`): `VITE_API_BASE_URL=http://localhost:5049`
  (porta do profile `http` em `backend/src/GastosApp.Api/Properties/launchSettings.json`).
- `.env.production` (usado em `npm run build`): `VITE_API_BASE_URL=<URL do API Gateway>` —
  hoje sem valor definitivo (dependência já registrada em `spec.md`);
  deixar como placeholder documentado até o deploy do backend expor a
  URL real.
- **Nenhum `.env*` é versionado** (`.gitignore` cobre `.env*`) — decisão
  confirmada pelo usuário: mesmo sem segredo hoje (só a URL pública da
  API), manter a convenção de nunca versionar `.env*` evita que, no
  futuro, alguém adicione uma variável sensível ali por hábito ("já é
  versionado, deve ser OK"). Só `.env.example` (documentando as chaves
  esperadas, sem valores reais) entra no Git. A URL de produção real
  fica documentada à parte (ex.: `frontend/README.md`), já que não é
  segredo — só não vai em `.env`.

## Segurança e custos AWS
- **Nenhum recurso AWS novo neste FEAT.** Consome a API/Cognito já
  provisionados (`controle-gastos-spa` App Client, User Pool
  `user-pool-gastos-app`) via chamadas HTTP ao backend — não integra
  diretamente com Cognito Hosted UI/OAuth, então `callback_urls` (hoje
  placeholder em `backend/infra/terraform/cognito.tf`) não é relevante
  para este FEAT.
- Deploy do frontend (S3/CloudFront) é infraestrutura futura, fora de
  escopo deste FEAT — quando planejado, segue o mesmo princípio de
  custo zero do backend (só Free Tier, sem recurso cobrado por
  hora/instância ligada) e **exige aprovação explícita do usuário antes
  de qualquer criação de recurso**, conforme `frontend/docs/constitution.md`.
- Nenhum segredo trafega por `VITE_*` — tudo que o Vite expõe com esse
  prefixo fica embutido, em texto público, no bundle JS entregue ao
  navegador.

## Mapeamento de erros

| Cenário | Origem | Erro tipado | UI |
|---|---|---|---|
| Email/senha vazios ou inválidos | Validação Zod (client) | — (erro de formulário do RHF) | Mensagem inline por campo |
| Credenciais incorretas | API 401 | `InvalidCredentialsError` | Alerta "Email ou senha inválidos" acima do formulário |
| Token ausente/expirado ao acessar rota protegida | `useAuthSession` (client) | — | Redirect para `/login` |
| Falha de rede/timeout | `fetch` reject | `NetworkError` | Alerta genérico de conectividade |
| Erro inesperado (5xx, parsing, 400 inesperado) | API | `UnknownAuthError` | Alerta genérico de erro |

## Testes (Vitest + Testing Library + MSW)

Caminhos abaixo relativos a `frontend/app/src/`.

- `features/auth/schemas/loginSchema.test.ts` — regras de validação
  (email inválido, senha curta, campos vazios)
- `features/auth/hooks/useLogin.test.ts` — sucesso (popula store), 401
  (`InvalidCredentialsError`, store não populado), erro de rede
- `features/auth/store/authStore.test.ts` — `setSession`/`clearSession`,
  cálculo de `expiresAt`, `isAuthenticated` antes/depois de expirar
- `features/auth/components/LoginForm.test.tsx` — validação inline,
  submit chamando `useLogin`, exibição de erro 401 (via MSW mockando
  `POST /auth/login`)
- `components/ProtectedRoute.test.tsx` — redireciona sem sessão,
  renderiza filhos com sessão válida

**Critério de conclusão da feature (regra permanente, ver
`frontend/docs/constitution.md`): todos os testes devem estar passando
antes de considerar o FEAT concluído.**

## Decisões confirmadas
- **Organização: feature-based** (padrão bulletproof-react), substituindo
  a proposta inicial de Clean Architecture em camadas — decisão revista
  pelo usuário para reduzir complexidade e seguir o padrão mais comum
  do mercado.
- **State manager: Zustand**, confirmado pelo usuário (ergonomia de
  teste sem Provider).
- **Tremor: instalação adiada**, confirmado pelo usuário — não entra
  como dependência neste FEAT; será adicionado quando a primeira
  feature de gráfico/dashboard for planejada.
- **Nenhum `.env*` versionado**, confirmado pelo usuário — só
  `.env.example` entra no Git, mesmo sem segredo hoje.
- **Segurança/custo AWS**: nível de preocupação alto, mesmo princípio de
  custo zero do backend; qualquer recurso AWS que impacte custo ou
  segurança exige aprovação explícita do usuário antes da implementação
  (regra permanente, ver `frontend/docs/constitution.md`).
- **Testes 100% passando é critério de conclusão de qualquer feature**
  (regra permanente, ver `frontend/docs/constitution.md`).

## Pontos que precisam de confirmação antes do `/tasks`
Nenhum pendente — todas as decisões técnicas deste plano foram
confirmadas.
