# Plano técnico — FEAT-14: Migração para o design system Modernist (Login)

## Camadas afetadas

Só o contexto frontend, dentro de `frontend/app/src/`. Nenhuma camada do
backend é tocada.

| Área | O que muda |
| --- | --- |
| `routes/LoginPage.tsx` | Reescrito: wordmark "jrn.", container com a classe raiz de escopo do Modernist, sem classes Tailwind |
| `routes/` (novo) `SignupComingSoonPage.tsx` | Página fake de destino do modo "Criar conta" |
| `features/auth/components/LoginForm.tsx` | Reescrito: segmentado Entrar/Criar conta, campos `.field`/`.input`, botão `.btn`, dois submits (login real / signup fake) |
| `features/auth/schemas/` (novo) `signupSchema.ts` | Schema Zod só para o formulário fake de cadastro (nome, e-mail, senha) — não usado por nenhuma API |
| `features/auth/schemas/loginSchema.ts` | **Sem alteração** — continua a única fonte de validação do login real |
| `features/auth/hooks/useLogin.ts`, `api/authApi.ts`, `errors/authErrors.ts`, `store/authStore.ts` | **Sem alteração** — reaproveitados como estão |
| `app/router.tsx` | Nova rota pública `/cadastro-em-breve` (fora de `ProtectedRoute`, ao lado de `/login`) |
| `styles/modernist/` (novo, dentro de `app/src/`) | Stylesheet vendorizada e **escopada** do design system Modernist |
| `package.json` (app) | Nova dependência de fonte (`@fontsource-variable/archivo`), mesmo padrão já usado para Geist |
| `frontend/docs/constitution.md` | Atualiza a seção "Stack" para descrever a UI em transição e referenciar `frontend/design-system/` como fonte dos tokens |

Nenhum componente em `components/ui/` (shadcn/ui) é alterado ou
removido. Nenhuma outra rota protegida muda.

## Decisão técnica: como isolar o Modernist sem afetar o resto do app

O app é uma SPA de página única (`index.html`) com um `<body>`
compartilhado por todas as rotas via `react-router-dom` (sem full
reload). Isso cria dois riscos se a stylesheet original de
`frontend/design-system/_ds/.../styles.css` for importada como está:

1. Ela define `:root { --radius-sm: 0px; --radius-md: 0px; ... }` — a
   mesma família de nomes de token que o Tailwind/shadcn já define em
   `index.css` (`--radius-sm/md/lg`, `--color-*`, etc.). Importar os dois
   arquivos globalmente faz um deles vencer, quebrando estilos das telas
   shadcn/ui fora do Login.
2. Ela também estiliza seletores globais reais (`body`, `h1`..`h6`, `*`,
   `a`, `img`, `:focus-visible`) sem nenhum escopo — aplicaria a
   qualquer outra tela do app, mesmo depois de o visitante navegar para
   fora do Login (React Router não recarrega a página, então o `<style>`
   carregado continua ativo).

**Decisão:** vendorizar uma cópia adaptada da stylesheet dentro do
próprio projeto (`frontend/app/src/styles/modernist/modernist.css`),
reescrevendo apenas os seletores realmente globais para viverem sob uma
classe de escopo `.ds-modernist`, aplicada como wrapper único no topo de
`LoginPage` (e reaproveitada por `SignupComingSoonPage`, que herda o
mesmo visual por estar no mesmo fluxo de autenticação):

- `:root { --color-bg: ...; --radius-md: 0px; ... }` → `.ds-modernist { --color-bg: ...; --radius-md: 0px; ... }`
  (os tokens deixam de ser globais; só resolvem dentro do wrapper)
- `body { ... }`, `h1..h6 { ... }`, `a { ... }`, `:focus-visible { ... }`,
  `::selection { ... }`, `*, *::before, *::after { box-sizing: border-box }`
  → prefixados com `.ds-modernist` (ex.: `.ds-modernist h1 { ... }`,
  `.ds-modernist :focus-visible { ... }`)
- Classes de componente já namespaced por natureza (`.btn`, `.input`,
  `.field`, `.seg`, `.seg-opt`) são copiadas como estão — não colidem com
  nada do shadcn/ui (que usa nomes diferentes) e só têm efeito dentro do
  wrapper por dependerem das variáveis de `.ds-modernist`
- Só as classes/seleções realmente usadas pelo Login são portadas:
  reset base, tipografia, `.btn`/`.btn-primary`/`.btn-block`,
  `.field`/`.input`, `.seg`/`.seg-opt`. Classes não usadas nesta feature
  (`.card`, `.tag`, `.table`, `.dialog*`, `.nav`) **não** são portadas
  agora — entram quando a tela que as usa for migrada, para não vender
  CSS morto sem uso real e sem tela de referência para conferir
- `frontend/design-system/` permanece como está (pasta de referência de
  design, não editada) — `modernist.css` é uma cópia adaptada, não um
  symlink/import direto dela

`modernist.css` é importado apenas nos arquivos que renderizam dentro do
wrapper (`LoginPage.tsx`, `SignupComingSoonPage.tsx`), via
`import '@/styles/modernist/modernist.css'` no topo do componente —
Vite trata isso como CSS global de módulo (sem CSS Modules), então o
arquivo entra no bundle assim que uma dessas rotas for visitada, mas o
efeito visual fica contido pelo prefixo `.ds-modernist`.

## Decisão técnica: fonte Archivo

O guia do Modernist carrega a fonte via `@import
url('https://fonts.googleapis.com/css2?family=Archivo...')` (dependência
de rede em runtime). O projeto já evita isso para Geist, usando
`@fontsource-variable/geist` (fonte empacotada localmente, sem chamada
externa). Mesma convenção aqui: adicionar `@fontsource-variable/archivo`
(pesos 400/600/800) como dependência e importá-la em `modernist.css` no
lugar do `@import` do Google Fonts.

**Ponto a confirmar antes do `/tasks`:** verificar durante a implementação
se `@fontsource-variable/archivo` existe no registro npm com os pesos
necessários; se não existir nesse formato, cair para
`@fontsource/archivo` (pacote por peso estático) como alternativa.

## Contratos de componentes (frontend)

### `LoginForm.tsx` — novo estado e comportamento

```ts
type AuthMode = 'login' | 'signup'

// estado local (useState), sem Zustand — é só UI, não é sessão
const [authMode, setAuthMode] = useState<AuthMode>('login')
```

- Modo `login` (default): usa `loginSchema`/`useLogin` **sem nenhuma
  alteração de contrato** — mesmo `react-hook-form` + `zodResolver`,
  mesmo `login(data)`, mesmo tratamento de `error` já existente
- Modo `signup`: formulário próprio com `useForm` + `zodResolver(signupSchema)`
  (campos Nome, E-mail, Senha); `onSubmit` **não** chama `authApi`/`useLogin`
    — apenas `navigate('/cadastro-em-breve')` (via `useNavigate` de
    `react-router-dom`, já dependência do projeto)
- Trocar o segmentado (`setAuthMode`) reseta qualquer erro do modo
  `login` visível (evita mostrar erro de login obsoleto ao voltar do
  modo cadastro) e não dispara nenhuma chamada de rede
- Markup usa `.seg`/`.seg-opt` com dois `<input type="radio" name="authmode">`
  ocultos (mesma técnica do protótipo: `sc-if`/rádio controla estado,
  aqui trocado por `onChange` + estado React), `.field`/`.input` para os
  campos, `.btn.btn-primary.btn-block` para o botão de submit (rótulo
  "Entrar" ou "Criar conta" conforme `authMode`)

### `signupSchema.ts` (novo)

```ts
export const signupSchema = z.object({
  name: z.string().min(1, 'Informe seu nome.'),
  email: z.string().email('Informe um email válido.'),
  password: z.string().min(8, 'A senha deve ter no mínimo 8 caracteres.'),
})
export type SignupFormData = z.infer<typeof signupSchema>
```

Mesmas regras de formato do login (consistência de UX), mas este schema
não é validado contra nenhuma API — existe só para dar feedback de
formulário coerente antes de navegar para a página fake.

### `LoginPage.tsx`

```tsx
export function LoginPage() {
  // mesmo useAuthSession + redirect reativo de hoje, inalterado
  return (
    <div className="ds-modernist" /* estilos de fundo/tela cheia aqui */>
      <div /* wordmark "jrn." + subtítulo "expenses" */ />
      <LoginForm />
    </div>
  )
}
```

Lógica de redirecionamento reativo (`useAuthSession` + `useEffect` +
`navigate('/', { replace: true })`) permanece exatamente como está —
migração é só de marcação/classe.

### `SignupComingSoonPage.tsx` (novo)

- Rota pública `/cadastro-em-breve`, fora de `ProtectedRoute`
- Conteúdo estático: título explicando que o cadastro ainda não está
  disponível + `.btn.btn-secondary` "Voltar para o login"
  (`<Link to="/login">` ou `navigate('/login')`) — sem chamadas de API,
  sem estado

### `router.tsx`

```tsx
{
  path: '/cadastro-em-breve',
  element: <SignupComingSoonPage />,
},
```
Adicionada como rota irmã de `/login`, antes do bloco `ProtectedRoute`.

## Recursos AWS

**Nenhum.** Esta feature não cria, altera nem afeta nenhum recurso AWS —
é puramente frontend (React/CSS), sem novo endpoint, sem infraestrutura.

## Mapeamento de erros

Sem mudança: `InvalidCredentialsError`, `NetworkError`, `UnknownAuthError`
(`features/auth/errors/authErrors.ts`) continuam sendo o único
mapeamento de erro usado pelo modo `login`. O modo `signup` (fake) não
gera nem trata erros de API, porque nunca chama uma.

## Testes afetados

- `LoginForm.test.tsx`: os testes existentes do fluxo de login real
  (submit válido, credenciais inválidas, validação client-side) devem
  continuar passando após a troca de marcação — ajustar apenas os
  seletores de teste (`getByLabelText`/`getByRole`) se o novo markup
  exigir; **nenhuma asserção de comportamento de login muda**
- Novos casos em `LoginForm.test.tsx` (ou arquivo próprio):
  - alternar para o modo "Criar conta" exibe o campo Nome e troca o
    rótulo do botão, sem disparar `fetch`/`authApi`
  - submeter o modo "Criar conta" navega para `/cadastro-em-breve` sem
    chamar `authApi.login`
- Novo `SignupComingSoonPage.test.tsx`: renderiza o texto de placeholder
  e o link/botão de volta para `/login`
- `router.tsx`: nenhum teste dedicado hoje; se existir teste de rotas,
  incluir a nova rota pública

## Resumo das decisões

1. Migração isolada ao Login; resto do app continua shadcn/ui + Tailwind
   (convivência lado a lado, já validado com o usuário no `/specify`)
2. Modernist é vendorizado como cópia adaptada em
   `frontend/app/src/styles/modernist/modernist.css`, com todo seletor
   global reescrito sob o escopo `.ds-modernist` para não vazar para o
   resto do app nem colidir com tokens do shadcn/ui (`--radius-*` etc.)
3. Só as classes de componente realmente usadas pelo Login são portadas
   agora (`.btn`, `.field`/`.input`, `.seg`); o restante entra junto com
   as telas que as usarem, em specs futuras
4. Fonte Archivo empacotada via `@fontsource-variable/archivo` (mesmo
   padrão do Geist já usado no projeto), não via `@import` de Google
   Fonts em runtime
5. "Criar conta" existe apenas como casca visual: formulário próprio
   (`signupSchema`) que nunca chama API e sempre navega para a rota fake
   `/cadastro-em-breve`

## Pontos a confirmar antes do `/tasks`

- Confirmar que `@fontsource-variable/archivo` existe no npm com os
  pesos 400/600/800 (senão, decidir entre `@fontsource/archivo` estático
  ou manter o `@import` do Google Fonts só dentro do escopo do Login)
- Confirmar o texto exato da página `/cadastro-em-breve` (proposta:
  título "Cadastro em breve", corpo curto explicando que o cadastro
  ainda não está disponível, botão "Voltar para o login") — sem cópia
  definida na spec, pode ser ajustada livremente na implementação
