# Plan — FEAT-28: Membros da conta e convites

## Camadas afetadas

Nova feature `features/members/` (primeira a consumir `/members`),
nova pasta compartilhada `lib/auth/` (primeira consumidora de `GET
/auth/me`, hoje implementado em `authApi.me()` sem nenhum uso — mesmo
racional de "promover pra `lib/` só quando existe consumo real" já
aplicado a `lib/categories/`), dois componentes genéricos novos em
`components/` (toast e overlay de processamento — resolvendo os
débitos técnicos do backlog), nova rota `routes/MembersPage.tsx`,
`app/router.tsx` e `components/nav/navConfig.ts`. Um ajuste em
`styles/modernist/modernist.css` (3 classes do `.dc.html` ainda não
vendorizadas). Não toca nenhuma feature existente além disso.

| Arquivo | O que muda |
|---|---|
| `lib/auth/currentUserApi.ts` (novo) | `currentUserApi.getCurrentUser(token)` → `GET /auth/me`; tipo `CurrentUser` (`{ userId, email, name }`) — API própria, não importa `features/auth/api/authApi.ts` (mesmo racional de `lib/categories/categoriesReadApi.ts` não importar `features/categories/api/`) |
| `lib/auth/currentUserErrors.ts` (novo) | `SessionExpiredError`, `NetworkError`, `UnknownCurrentUserError` (mesmo padrão minimalista de `lib/categories/categoryErrors.ts`) |
| `lib/auth/useCurrentUser.ts` (novo) | `useCurrentUser()` → `{ data, isLoading, error }` (mesmo esqueleto de `lib/categories/useCategories.ts`) |
| `components/Toast.tsx` (novo) | Componente genérico de toast — `{ message: string \| null, onDismiss: () => void }`, auto-dismiss interno (~3.2s) |
| `components/ProcessingOverlay.tsx` (novo) | Overlay genérico de tela cheia — `{ label: string }`, cobre o ancestral `position: relative` mais próximo |
| `features/members/api/membersApi.ts` (novo) | `getMembers`, `inviteMember`, `updateMemberRole`, `removeMember`; tipos `MemberRole`, `MemberStatus`, `MemberItem` |
| `features/members/errors/memberErrors.ts` (novo) | `SessionExpiredError`, `NetworkError`, `ValidationError`, `ForbiddenError`, `NotFoundError`, `ConflictError`, `CannotModifyTitularError`, `CannotRemoveTitularError`, `UnknownMemberError` |
| `features/members/hooks/useMembers.ts` (novo) | Lista (`GET /members`) — mesmo esqueleto de `useCategories`, mas feature-scoped (só esta feature consome por ora) |
| `features/members/hooks/useInviteMember.ts` (novo) | Comando (`POST /members`) — mesmo esqueleto de `useRegisterCategory` |
| `features/members/hooks/useUpdateMemberRole.ts` (novo) | Comando (`PUT /members/{id}`) — mesmo esqueleto de `useUpdateCategory`, instanciado por linha (ver `MemberRow`) |
| `features/members/hooks/useRemoveMember.ts` (novo) | Comando (`DELETE /members/{id}`) — mesmo esqueleto de `useDeleteCategory` |
| `features/members/utils/roleLabels.ts` (novo) | Mapas `role → label` ("Leitura"/"Lançar"/"Total"/"Titular") e `role → descrição` (os 3 textos do backend FEAT-20) |
| `features/members/components/MemberList.tsx` (novo) | Linha do Titular + `sc-for` de `MemberRow`; recebe `isViewerTitular`/`currentUserEmail` |
| `features/members/components/MemberRow.tsx` (novo) | Uma linha de membro; dono do `useUpdateMemberRole` (seletor otimista com rollback) e do gatilho de remoção |
| `features/members/components/MemberRemoveDialog.tsx` (novo) | Confirmação de remoção — mesmo padrão de `CategoryDeleteDialog` |
| `features/members/components/InviteMemberDialog.tsx` (novo) | Popup de convite (e-mail + seletor de papel + descrição) com `ProcessingOverlay` durante o `POST` |
| `routes/MembersPage.tsx` (novo) | Orquestra `useMembers` + `useCurrentUser`, título + botão "+ Convidar pessoa" (só Titular), `MemberList`, `InviteMemberDialog`, `Toast` |
| `app/router.tsx` | Nova rota `{ path: 'members', element: <MembersPage /> }` |
| `components/nav/navConfig.ts` | Novo item `{ id: 'members', label: 'Membros', icon: Users, to: '/members', status: 'active' }`, entre `categories` e `settings` (mesma ordem do `.dc.html`) |
| `styles/modernist/modernist.css` | Novas classes `.je-spin`/`.je-indet`/`.je-toast` (+ `@keyframes` correspondentes) — só do `.dc.html`, não do bundle base |

Não muda: `backend` (tudo já implementado), `features/auth/api/authApi.ts`
(já tinha `me()`, só ganha um consumidor via `lib/auth/`),
`features/categories`, `features/transactions`, `features/summary`,
`features/reports`.

## Contratos técnicos

### `lib/auth/currentUserApi.ts`

```ts
export interface CurrentUser {
  userId: string
  email: string
  name: string
}

async function getCurrentUser(token: string): Promise<CurrentUser> {
  const response = await safeFetch(() =>
    httpClient.get('/auth/me', { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertOk(response) // 401 → SessionExpiredError; !ok → UnknownCurrentUserError
  return response.json() as Promise<CurrentUser>
}

export const currentUserApi = { getCurrentUser }
```

### `lib/auth/useCurrentUser.ts`

```ts
interface UseCurrentUserResult {
  data: CurrentUser | null
  isLoading: boolean
  error: Error | null
}
export function useCurrentUser(): UseCurrentUserResult { /* mesmo esqueleto de useCategories */ }
```

### `components/Toast.tsx`

```tsx
interface ToastProps {
  message: string | null
  onDismiss: () => void
}
// useEffect: quando `message` muda pra não-null, agenda
// setTimeout(onDismiss, 3200); limpa o timer no cleanup/unmount ou se
// `message` mudar de novo antes de disparar. `position: fixed; bottom:
// 20px; right: 20px` (adaptado do `position: absolute` do `.dc.html`,
// que só funciona lá porque o protótipo tem um container relative
// cobrindo a tela inteira — `fixed` é a forma correta de obter o
// mesmo resultado visual num app real). Ícone de check + texto, classe
// `.je-toast` (anima entrada). Retorna `null` quando `message` é
// `null` (nada renderizado, nem espaço reservado).
```

### `components/ProcessingOverlay.tsx`

```tsx
interface ProcessingOverlayProps {
  label: string
}
// `position: absolute; inset: 0; z-index: 5` — precisa que o
// ancestral direto tenha `position: relative` (responsabilidade do
// consumidor, ex.: InviteMemberDialog adiciona isso inline no próprio
// `.dialog`, só quando usa o overlay — outros dialogs sem overlay
// continuam sem `position: relative`, sem efeito colateral).
// `.je-spin` (spinner grande) + `label` (texto uppercase, mesmo
// estilo do `.dc.html`) + barra `.je-indet` (progresso indeterminado).
```

### `features/members/api/membersApi.ts`

```ts
export type MemberRole = 'Leitura' | 'Lancar' | 'Total' | 'Titular'
export type MemberStatus = 'ConvitePendente' | 'Ativo'

export interface MemberItem {
  id: string
  email: string
  role: MemberRole
  status: MemberStatus
  createdAt: string
}

export interface InviteMemberPayload {
  email: string
  role: Exclude<MemberRole, 'Titular'>
}

async function getMembers(token: string): Promise<{ items: MemberItem[] }>
async function inviteMember(token: string, payload: InviteMemberPayload): Promise<MemberItem>
async function updateMemberRole(token: string, id: string, role: Exclude<MemberRole, 'Titular'>): Promise<MemberItem>
async function removeMember(token: string, id: string): Promise<void>

export const membersApi = { getMembers, inviteMember, updateMemberRole, removeMember }
```

Mesmo padrão `safeFetch`/`extractErrorCode` de `categoriesWriteApi.ts`
pra disambiguar `409`/`422` pelo sufixo do `type` RFC 9457
(`member-already-exists` → `ConflictError`; `cannot-modify-titular` →
`CannotModifyTitularError`; `cannot-remove-titular` →
`CannotRemoveTitularError`).

### `features/members/hooks/*`

```ts
useMembers(): { items: MemberItem[], isLoading, error }              // GET
useInviteMember(): { inviteMember: (payload) => Promise<void>, isLoading, error, success, data: MemberItem | null }  // POST
useUpdateMemberRole(id: string): { updateRole: (role) => Promise<void>, isLoading, error, success, data: MemberItem | null }  // PUT
useRemoveMember(): { removeMember: (id) => Promise<void>, isLoading, error, success }  // DELETE
```
Os três últimos seguem exatamente `useRegisterCategory`/
`useUpdateCategory`/`useDeleteCategory` (comando + `isLoading`/`error`/
`success`).

### `features/members/utils/roleLabels.ts`

```ts
export const ROLE_LABEL: Record<MemberRole, string> = {
  Leitura: 'Leitura', Lancar: 'Lançar', Total: 'Total', Titular: 'Titular',
}
export const ROLE_DESCRIPTION: Record<Exclude<MemberRole, 'Titular'>, string> = {
  Leitura: 'Pode visualizar despesas e relatórios, sem editar nada.',
  Lancar: 'Pode visualizar e lançar novas despesas.',
  Total: 'Pode visualizar, lançar despesas e criar categorias e orçamentos. Não pode gerenciar outros membros.',
}
```

### `features/members/components/MemberList.tsx`

```tsx
interface MemberListProps {
  titular: MemberItem | null
  others: MemberItem[]
  isViewerTitular: boolean
  currentUserEmail: string | null
  onRoleChanged: (updated: MemberItem) => void
  onRemoved: (id: string) => void
}
```
Linha do Titular sempre renderizada à parte (tag "Titular", descrição
fixa); `(você)` anexado quando `titular.email === currentUserEmail`.
`others.map(m => <MemberRow key={m.id} member={m} readOnly={!isViewerTitular} isMe={m.email === currentUserEmail} onRoleChanged={onRoleChanged} onRemoveRequested={setRemoveTarget} />)`.

### `features/members/components/MemberRow.tsx`

```tsx
interface MemberRowProps {
  member: MemberItem
  readOnly: boolean
  isMe: boolean
  onRoleChanged: (updated: MemberItem) => void
  onRemoveRequested: (member: MemberItem) => void
}
```
`const { updateRole, isLoading, error, success, data } = useUpdateMemberRole(member.id)`.
Estado local `optimisticRole` inicializado com `member.role`,
sincronizado via `useEffect` quando `member.role` (prop) muda
(atualização externa). Ao selecionar uma opção do `.seg`: seta
`optimisticRole` na hora (feedback imediato) e chama `updateRole(novoRole)`.
`useEffect` em `success`/`data`: propaga `onRoleChanged(data)` pro pai.
`useEffect` em `error`: reverte `optimisticRole` pra `member.role` e
mostra mensagem inline na linha (decisão/US6). `readOnly` esconde o
`.seg` (mostra só `ROLE_LABEL[member.role]` como texto) e o botão de
remover (decisão 1).

### `features/members/components/MemberRemoveDialog.tsx`

Idêntico a `CategoryDeleteDialog` na estrutura (`useRemoveMember`,
`useEffect` em `success`→`onRemoved`, tratamento de `NotFoundError`
como sucesso silencioso — outra sessão do Titular já removeu),
trocando o texto pra "Remover membro" / `Tem certeza que deseja
remover "{email}" da conta? Essa ação não pode ser desfeita.`.

### `features/members/components/InviteMemberDialog.tsx`

```tsx
interface InviteMemberDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onInvited: (member: MemberItem) => void
}
```
Estado local de formulário (`email`, `role`, inicial `'Lancar'` —
decisão 6). `const { inviteMember, isLoading, error, success, data } = useInviteMember()`.
`isLoading` → renderiza `<ProcessingOverlay label="Enviando convite" />`
sobre o conteúdo do `.dialog` (que ganha `style={{ position: 'relative' }}`
só neste dialog). `success`/`data` → `onInvited(data)` + fecha. `error`
→ mensagem inline (decisão: sem toast em erro). Botões
"Cancelar"/"Enviar convite" com `disabled={isLoading}`.

### `routes/MembersPage.tsx`

```tsx
const { items, isLoading: membersLoading, error: membersError } = useMembers()
const { data: currentUser, isLoading: userLoading, error: userError } = useCurrentUser()
const isLoading = membersLoading || userLoading
const error = membersError ?? userError

const titular = items.find(m => m.role === 'Titular') ?? null
const others = items.filter(m => m.role !== 'Titular')
const isViewerTitular = !!currentUser && !!titular && currentUser.email === titular.email

const [inviteOpen, setInviteOpen] = useState(false)
const [toastMessage, setToastMessage] = useState<string | null>(null)
const [localOthers, setLocalOthers] = useState<MemberItem[]>([])
useEffect(() => setLocalOthers(others), [items]) // mesmo padrão de sincronização de CategoriesPage

function handleInvited(member: MemberItem) {
  setLocalOthers(prev => [...prev, member])
  setInviteOpen(false)
  setToastMessage(`Convite enviado para ${member.email}.`)
}
```
Cabeçalho: "Membros da conta" + botão "+ Convidar pessoa" **só quando
`isViewerTitular`** (decisão 1). `<MemberList titular others={localOthers} .../>`,
`<InviteMemberDialog .../>`, `<Toast message={toastMessage} onDismiss={() => setToastMessage(null)} />`.

## Decisões técnicas

1. **`lib/auth/` como pasta nova**, espelhando `lib/categories/`: API
   própria (não importa `features/auth/api/authApi.ts`) — mesma regra
   já seguida por `lib/categories/categoriesReadApi.ts` não importar
   `features/categories/api/`, mesmo custo de pequena duplicação
   (poucas linhas) aceito em troca de manter a regra "feature nunca
   importa de outra feature" sem exceção.
2. **`useMembers`/`useInviteMember`/`useUpdateMemberRole`/
   `useRemoveMember` ficam em `features/members/`, não em `lib/`** — só
   esta feature consome `/members` por ora (mesmo racional já usado
   repetidamente: sobe pra `lib/` só quando uma segunda feature
   precisar).
3. **`Toast`/`ProcessingOverlay` em `components/` (compartilhado, não
   feature-scoped)** — resolvem os 2 débitos técnicos do backlog como
   componentes genéricos de propósito (não específicos de "convite"),
   prontos pra outras telas reaproveitarem quando quiserem (decisão 2
   da spec deixa explícito que isso não inclui retroaplicar em telas
   já existentes nesta mesma leva).
4. **`MemberRow` como componente próprio** (não inline dentro de
   `MemberList`) — necessário pra `useUpdateMemberRole(member.id)` ser
   chamado uma vez por linha, respeitando Regras de Hooks (cada linha
   precisa da própria instância do hook pro estado otimista/rollback
   ser independente por membro).
5. **Seletor de papel otimista com rollback**, replicando a UX do
   `.dc.html` (troca reflete na hora) sem esperar a resposta da API,
   revertendo só em caso de erro — trade-off aceito porque a operação é
   idempotente e de baixo risco (reversível a qualquer momento pelo
   próprio Titular).
6. **`Toast` com `position: fixed`, não `absolute`** — adaptação do
   `.dc.html` (que só funciona `absolute` porque o protótipo tem um
   container `relative` cobrindo a tela inteira, artefato do harness
   de demonstração); `fixed` é a forma correta de obter o mesmo
   resultado visual (canto inferior direito da viewport) num app real
   com roteamento client-side.
7. **Botão de remover reaproveita `.btn` + ícone `Trash2` (lucide-react)**,
   não `.btn-icon` (classe do `.dc.html` nunca vendorizada) — mesmo
   padrão já usado em `CategoryList.tsx` pro botão de excluir categoria.
8. **Novo item de menu "Membros" entre "Categorias" e "Configurações"**
   (`navConfig.ts`), mesma ordem do sidebar do `.dc.html`.

## Recursos AWS

Nenhum. Esta feature só consome `GET`/`POST`/`PUT`/`DELETE /members` e
`GET /auth/me` (ambos já em produção, backend FEAT-20 e endpoint já
existente) — nenhuma infraestrutura nova.

## Mapeamento de erros

### `lib/auth/currentUserApi.ts`

| Condição | Exceção | Mensagem |
|---|---|---|
| `401` | `SessionExpiredError` | "Sua sessão expirou. Faça login novamente." — limpa a sessão |
| falha de rede | `NetworkError` | "Não foi possível conectar à API. Verifique sua conexão." |
| outro status | `UnknownCurrentUserError` | "Ocorreu um erro inesperado. Tente novamente." |

### `features/members/api/membersApi.ts`

| Origem | Condição | Exceção | Mensagem |
|---|---|---|---|
| qualquer chamada | `401` | `SessionExpiredError` | "Sua sessão expirou. Faça login novamente." — limpa a sessão |
| qualquer chamada | falha de rede | `NetworkError` | "Não foi possível conectar à API. Verifique sua conexão." |
| `POST`/`PUT` | `400` | `ValidationError` | "Preencha um e-mail e um nível de acesso válidos." |
| `POST`/`PUT`/`DELETE` | `403` | `ForbiddenError` | "Seu nível de acesso não permite esta ação." (defensivo — decisão 1 já esconde a UI que levaria aqui) |
| `PUT`/`DELETE` | `404` | `NotFoundError` | "Membro não encontrado." (tratado como sucesso silencioso em `MemberRemoveDialog`, mesmo padrão de `CategoryDeleteDialog`) |
| `POST` | `409` (`member-already-exists`) | `ConflictError` | "Este e-mail já é membro desta conta." |
| `PUT` | `422` (`cannot-modify-titular`) | `CannotModifyTitularError` | "O papel do Titular não pode ser alterado." (defensivo) |
| `DELETE` | `422` (`cannot-remove-titular`) | `CannotRemoveTitularError` | "O Titular da conta não pode ser removido." (defensivo) |
| qualquer chamada | outro status | `UnknownMemberError` | "Ocorreu um erro inesperado. Tente novamente." |

## Pontos a confirmar antes do `/tasks`

1. **Escopo maior que o usual**: esta feature introduz 2 componentes
   genéricos novos (`Toast`/`ProcessingOverlay`) além da feature em si
   — mais arquivos que uma FEAT típica recente (26/27), mas já é o
   resultado esperado da decisão 2 da spec (resolver os débitos
   técnicos agora). Confirmar que o tamanho do checklist resultante
   (provavelmente 20+ tasks) está de acordo com o esperado antes de eu
   gerar o `tasks.md`.
2. **`useMembers` não tem `refetch`** (mesmo racional de `useReports`
   na FEAT-27: as próprias mutações atualizam o estado local da página
   via `onRoleChanged`/`onRemoved`/`onInvited`, sem precisar recarregar
   a lista inteira) — confirmar que este é o comportamento esperado
   (ex.: se dois Titulares da mesma conta estivessem editando a lista
   ao mesmo tempo em abas diferentes, um não veria a mudança do outro
   sem recarregar a página; cenário considerado raro o suficiente pra
   não justificar polling ou WebSocket).
