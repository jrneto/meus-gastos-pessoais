# Plano técnico — FEAT-29: Permissões por role na UI

Referência: `spec.md` desta pasta. Sem nenhuma mudança de contrato de
wire — a matriz de autorização já está em produção desde o backend
FEAT-20/FEAT-22; este plano só cobre a camada frontend.

## Camadas afetadas (feature-based)

| Camada | O que muda |
|---|---|
| `lib/permissions/` (**novo módulo**) | Resolução compartilhada de "qual é o meu papel na conta ativa" + funções puras da matriz de autorização — consumido por `features/transactions` e `features/categories` sem violar a regra "uma feature nunca importa de dentro de outra" |
| `features/transactions/errors` | Nova classe `ForbiddenError` |
| `features/transactions/api` | `transactionsApi.ts` passa a mapear 403 → `ForbiddenError` nas 3 chamadas de escrita (`registerTransaction`, `updateTransaction`, `deleteTransaction`) |
| `features/transactions/components` | `TransactionDetailDialog` ganha prop `canManage`, controlando "Editar"/"Excluir" |
| `features/categories/errors` | Nova classe `ForbiddenError` |
| `features/categories/api` | `categoriesWriteApi.ts` passa a mapear 403 → `ForbiddenError` em `createCategory`/`updateCategory`/`deleteCategory` |
| `features/categories/components` | `CategoryList` ganha prop `canWrite`, controlando os ícones de editar/excluir de cada linha |
| `routes/TransactionsListPage.tsx` | Consome `useMyRole()`; controla os botões "+ Nova despesa"/"+ Nova receita" e computa `canManage` por transação ao abrir o detalhe |
| `routes/CategoriesPage.tsx` | Consome `useMyRole()`; controla o botão "+ Nova categoria" e repassa `canWrite` pra `CategoryList` |

Sem mudança em `features/members/` (já resolvido na FEAT-28, com regra
própria restrita ao Titular) nem em nenhuma tela sem ação de escrita
(Dashboard, Relatórios).

## Novo módulo `lib/permissions/`

Mesmo racional já usado no projeto para dado lido por mais de uma
feature: `lib/categories/` (leitura de categorias, consumida por
`features/transactions` e `features/categories`) e `lib/auth/`
(`useCurrentUser`, que já duplica intencionalmente `GET /auth/me` de
`features/auth/api/authApi.ts#me` só pra evitar import cruzado entre
features). Este módulo segue o mesmo padrão: duplica um cliente mínimo
de `GET /members` (só os campos usados) em vez de reaproveitar
`features/members/api/membersApi.ts#getMembers` — replicar é a mesma
troca já aceita no projeto (menor acoplamento entre features, ao custo
de um cliente HTTP a mais).

```
lib/permissions/
├── types.ts              # MemberRole (mesmo union já usado em features/members)
├── membershipReadApi.ts  # GET /members mínimo (id/email/role), mesmo padrão de lib/categories/categoriesReadApi.ts
├── permissionErrors.ts   # NetworkError, SessionExpiredError, UnknownPermissionError
├── useMyRole.ts           # hook: cruza GET /auth/me (lib/auth/useCurrentUser) + GET /members por e-mail
└── rules.ts                # funções puras da matriz de autorização
```

### `lib/permissions/types.ts`

```ts
export type MemberRole = 'Leitura' | 'Lancar' | 'Total' | 'Titular'
```

### `lib/permissions/membershipReadApi.ts`

```ts
export interface MembershipItem {
  email: string
  role: MemberRole
}

async function getMembers(token: string): Promise<{ items: MembershipItem[] }>
export const membershipReadApi = { getMembers }
```
Mesmo tratamento de erro de `lib/categories/categoriesReadApi.ts`:
`NetworkError` (falha de rede), `SessionExpiredError` (401),
`UnknownPermissionError` (qualquer outro `!response.ok`) — `GET
/members` nunca responde 403 pra nenhum papel (matriz da FEAT-20), então
não há branch de 403 aqui.

### `lib/permissions/useMyRole.ts`

```ts
interface UseMyRoleResult {
  role: MemberRole | null
  userId: string | null
  isLoading: boolean
  error: Error | null
}

export function useMyRole(): UseMyRoleResult
```
Implementação: chama `useCurrentUser()` (já existente,
`lib/auth/useCurrentUser.ts`) e `membershipReadApi.getMembers(token)` em
paralelo (mesmo padrão de `MembersPage.tsx`, que já faz exatamente essa
combinação hoje, mas inline na página). `role` é o `role` do item de
`GET /members` cujo `email` bate com o `email` de `GET /auth/me`;
`userId` é o `userId` de `GET /auth/me`. `isLoading` é `true` enquanto
qualquer uma das duas chamadas está pendente; `error` é o primeiro erro
não nulo entre as duas. `SessionExpiredError` de qualquer uma delas
limpa a sessão (`useAuthStore.getState().clearSession()`), mesmo padrão
já usado em `useMembers`/`useCurrentUser`.

### `lib/permissions/rules.ts`

Funções puras, sem estado — só a matriz da spec, testáveis isoladamente
por Vitest puro (sem RTL/MSW):

```ts
export function canCreateTransaction(role: MemberRole | null): boolean {
  return role === 'Lancar' || role === 'Total' || role === 'Titular'
}

export function canManageTransaction(role: MemberRole | null, isOwn: boolean): boolean {
  if (role === 'Total' || role === 'Titular') return true
  if (role === 'Lancar') return isOwn
  return false
}

export function canWriteCategories(role: MemberRole | null): boolean {
  return role === 'Total' || role === 'Titular'
}
```
`canManageTransaction` cobre editar **e** excluir com a mesma regra —
a matriz nunca diferencia as duas ações (nenhum papel edita sem poder
excluir, ou vice-versa), por isso um único flag (`canManage`) é passado
adiante em vez de dois booleanos redundantes.

## `features/transactions`

### `errors/transactionErrors.ts`
```ts
export class ForbiddenError extends Error {
  constructor() {
    super('Seu nível de acesso não permite esta ação.')
    this.name = 'ForbiddenError'
  }
}
```
Mesma mensagem já usada em `features/members/errors/memberErrors.ts#ForbiddenError`
(espelha o `detail` do `insufficient-permission` do backend).

### `api/transactionsApi.ts`
`assertOk` (criar), `assertUpdateOk` (editar) e `assertDeleteOk`
(excluir) ganham, antes do fallback genérico:
```ts
if (response.status === 403) {
  throw new ForbiddenError()
}
```
`assertQueryOk`/`assertDetailOk` (as duas leituras) não mudam — `GET
/transactions` nunca responde 403 pra nenhum papel.

### `components/TransactionDetailDialog.tsx`
Novo prop `canManage: boolean`. O botão "Excluir" (rodapé esquerdo) e o
botão "Editar" (rodapé direito) só renderizam quando `canManage` é
`true`; "Fechar" continua sempre visível. Quando `canManage` é `false`,
o rodapé fica só com "Fechar" — ajuste de alinhamento (hoje
`justify-content: space-between` pressupõe os dois lados ocupados) fica
para o `/tasks`/implementação.

### `routes/TransactionsListPage.tsx`
```ts
const { role, userId } = useMyRole()
const canCreate = canCreateTransaction(role)
```
- `canCreate` controla a exibição dos dois botões "+ Nova despesa"/"+
  Nova receita" (ambos, mesma regra)
- Ao abrir `TransactionDetailDialog` para uma transação, calcula
  `canManage = canManageTransaction(role, transaction.createdByUserId === userId)`
  e passa como prop

## `features/categories`

### `errors/categoryErrors.ts`
Mesma classe `ForbiddenError` (mesma mensagem), acrescentada ao arquivo
já existente — sem mudar o re-export de `SessionExpiredError`/
`NetworkError`/`UnknownCategoryError` vindos de `lib/categories/categoryErrors.ts`
(esses continuam só leitura, sem 403 possível).

### `api/categoriesWriteApi.ts`
`assertWriteOk` (criar/editar) e `assertDeleteOk` (excluir) ganham o
mesmo branch de 403 → `ForbiddenError`, antes do fallback genérico —
posicionado antes do branch de 422 já existente, mesma ordem dos
demais status HTTP nos dois métodos.

### `routes/CategoriesPage.tsx`
```ts
const { role } = useMyRole()
const canWrite = canWriteCategories(role)
```
`canWrite` controla a exibição do botão "+ Nova categoria" e é passado
como prop pra `CategoryList`.

### `components/CategoryList.tsx`
Novo prop `canWrite: boolean`. O bloco com os dois ícones (`Pencil`/
`Trash2`, editar/excluir) de cada linha só renderiza quando `canWrite`
é `true` — o rótulo de orçamento ao lado (informativo, não é ação de
escrita) continua sempre visível pra qualquer papel.

## Decisões técnicas

1. **Módulo novo em `lib/`, não em nenhuma feature** — tanto
   `features/transactions` quanto `features/categories` precisam do
   próprio papel; a regra de dependência do projeto (`features/*` nunca
   importa de dentro de outra feature) exige que esse dado compartilhado
   suba pra `lib/`, mesmo padrão já usado para categorias e usuário
   atual.
2. **Duplicar `GET /members` em vez de mover
   `features/members/api/membersApi.ts#getMembers` pra `lib/`** — menor
   risco (não mexe em código já entregue/testado da FEAT-28) e segue o
   precedente já aceito no projeto (`lib/auth/currentUserApi.ts` duplica
   `features/auth/api/authApi.ts#me`). Ver "Pontos a confirmar" abaixo.
3. **Um único flag (`canManage`) para editar+excluir de transação** —
   a matriz nunca separa as duas permissões; dois booleanos idênticos
   seriam redundância sem ganho.
4. **Nenhuma proteção de rota/URL** — mesma decisão já tomada na
   FEAT-28 pra Membros (esconder o botão de entrada é suficiente;
   acesso direto por URL ao formulário fica de fora, conforme "Fora do
   escopo" da spec).
5. **Mensagem do `ForbiddenError` é texto fixo, espelhando o `detail`
   do backend** (`"Seu nível de acesso não permite esta ação."`) — nenhum
   dialog precisa de UI nova pra exibi-la: `TransactionFormDialog`/
   `TransactionForm`, `TransactionDeleteDialog`, `CategoryForm` e
   `CategoryDeleteDialog` já renderizam `error.message` de forma
   genérica pra qualquer erro não tratado especificamente (confirmado
   lendo os quatro componentes) — o novo `ForbiddenError` só precisa
   existir e ser lançado pela camada de API pra já aparecer corretamente.
6. **Nenhum botão de escrita renderiza enquanto `role` é `null`** — as
   três funções de `rules.ts` recebem `MemberRole | null` e retornam
   `false` pra `null`, cobrindo o estado de carregamento (US10) sem
   nenhum flag adicional de "carregando" nos componentes de UI.

## Mapeamento de erros de negócio

| Situação | Status HTTP | Erro no frontend |
|---|---|---|
| Papel sem permissão de escrita em `/transactions` (criar, ou editar/excluir transação alheia com papel `Lancar`, ou qualquer escrita com `Leitura`) | 403 (`insufficient-permission`) | `ForbiddenError` (`features/transactions/errors`) |
| Papel sem permissão de escrita em `/categories` (`Leitura`/`Lancar` em criar, editar ou excluir) | 403 (`insufficient-permission`) | `ForbiddenError` (`features/categories/errors`) |
| Falha ao buscar `GET /members` ou `GET /auth/me` dentro de `useMyRole` | 401 | `SessionExpiredError` (`lib/permissions`) → limpa sessão, mesmo padrão já usado |
| Falha ao buscar `GET /members` ou `GET /auth/me` dentro de `useMyRole` | rede / outro status | `NetworkError`/`UnknownPermissionError` (`lib/permissions`) → mensagem genérica, tela some com o botão de escrita até resolver (role fica `null`) |

Nenhum status novo — os únicos dois status tratados aqui (401, 403) já
existem nos dois endpoints, só não eram mapeados pra uma classe
específica no frontend.

## Recursos AWS

Nenhum — feature 100% frontend, consumindo endpoints HTTP já
existentes e já em produção. Nenhum `.tf` é tocado.

## Testes (Vitest + RTL + MSW)

- `lib/permissions/rules.test.ts` — testes puros das 3 funções, tabela
  completa da matriz (sem RTL/MSW)
- `lib/permissions/membershipReadApi.test.ts` — mesmo padrão de
  `lib/categories/categoriesReadApi.test.ts` (MSW)
- `lib/permissions/useMyRole.test.ts` — mesmo padrão de
  `lib/auth/useCurrentUser.test.ts`/`features/members/hooks/useMembers.test.ts`
  (RTL `renderHook` + MSW), cobrindo cruzamento por e-mail, loading
  combinado e erro combinado
- `features/transactions/api/transactionsApi.test.ts`,
  `features/categories/api/categoriesWriteApi.test.ts` — novo caso de
  403 → `ForbiddenError` nos métodos de escrita
- `features/transactions/components/TransactionDetailDialog.test.tsx` —
  novos casos `canManage=true`/`false`
- `features/categories/components/CategoryList.test.tsx` — novos casos
  `canWrite=true`/`false`
- `routes/TransactionsListPage.test.tsx`,
  `routes/CategoriesPage.test.tsx` — cenários por papel (US1, US3, US4,
  US6, US7, US10), mockando `useMyRole` (ou os dois endpoints via MSW,
  a definir no `/tasks`)

## Pontos a confirmar antes do `/tasks`

1. **Nome do módulo/hook**: `lib/permissions/` + `useMyRole()` — como
   sugestão direta pro caso de uso das duas features consumidoras.
   Alternativa avaliada e descartada: `lib/account/` +
   `useCurrentMembership()` (mais alinhado ao nome do domínio no
   backend, `Membership`, mas menos direto sobre o que o hook resolve
   pra quem vai usá-lo).
2. **Duplicar `GET /members` em `lib/permissions/membershipReadApi.ts`**
   em vez de mover o cliente já existente em
   `features/members/api/membersApi.ts` para `lib/` (o que eliminaria a
   duplicação, mas tocaria código já entregue/testado da FEAT-28) — a
   decisão 2 acima assume a duplicação; sinalizando caso prefira a
   alternativa mais DRY.
3. **Ajuste de alinhamento do rodapé de `TransactionDetailDialog`**
   quando `canManage=false` (hoje o layout pressupõe "Excluir" à
   esquerda e "Editar"/"Fechar" à direita) — fica como detalhe de
   implementação a resolver no `/tasks`, sinalizando aqui por ser a
   única mudança visual desta feature (o restante é só mostrar/esconder
   controles já existentes, sem alterar layout).
