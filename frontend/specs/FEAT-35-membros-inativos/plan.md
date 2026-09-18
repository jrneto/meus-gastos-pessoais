# Plano técnico — FEAT-35: Membros inativos

## Camadas afetadas

Tudo dentro de `frontend/app/src/features/members/` + o ponto de
orquestração em `routes/MembersPage.tsx`. Nenhuma outra feature é
tocada (o contrato de `/transactions`/`/categories` não muda).

| Arquivo | Mudança |
|---|---|
| `api/membersApi.ts` | `MemberStatus` ganha `"Inativo"`; `assertUpdateOk`/`assertRemoveOk` mapeiam os 2 novos códigos 422 |
| `errors/memberErrors.ts` | 2 classes novas: `CannotModifyInactiveMemberError`, `MemberAlreadyInactiveError` |
| `hooks/useMembers.ts` | ganha `refetch()` + `isRefetching` (recarrega a lista sem acionar o `isLoading` de carga inicial) |
| `utils/statusLabels.ts` (novo) | `MEMBER_STATUS_LABEL`, mesmo padrão de `utils/roleLabels.ts` |
| `components/MemberRow.tsx` | trata `status === 'Inativo'` como somente-leitura (mesma renderização hoje usada por `readOnly`), label de status via `MEMBER_STATUS_LABEL` |
| `components/MemberRemoveDialog.tsx` | só o texto de confirmação muda (sem mudança estrutural) |
| `routes/MembersPage.tsx` | `handleRemoved` chama `refetch()` em vez de filtrar `localOthers` localmente; exibe um spinner discreto (`isRefetching`) sem esconder a lista |
| `components/InviteMemberDialog.tsx` | **sem mudança de código** — o backend já libera reconvite de e-mail `Inativo`; só ganha cobertura de teste nova (US8) |
| `hooks/useRemoveMember.ts`, `hooks/useUpdateMemberRole.ts` | **sem mudança de código** — já propagam qualquer `Error` typado que `membersApi` lançar |

## Contratos técnicos detalhados

### `api/membersApi.ts`

```ts
export type MemberStatus = 'ConvitePendente' | 'Ativo' | 'Inativo'
```

`assertUpdateOk` (PUT), novo ramo dentro do `if (response.status === 422)`:
```ts
if (response.status === 422) {
  const code = await extractErrorCode(response)
  if (code === 'cannot-modify-titular') throw new CannotModifyTitularError()
  if (code === 'cannot-modify-inactive-member') throw new CannotModifyInactiveMemberError()
  throw new UnknownMemberError()
}
```

`assertRemoveOk` (DELETE), mesmo padrão:
```ts
if (response.status === 422) {
  const code = await extractErrorCode(response)
  if (code === 'cannot-remove-titular') throw new CannotRemoveTitularError()
  if (code === 'member-already-inactive') throw new MemberAlreadyInactiveError()
  throw new UnknownMemberError()
}
```

### `errors/memberErrors.ts`

```ts
export class CannotModifyInactiveMemberError extends Error {
  constructor() {
    super('Não é possível alterar o papel de um membro inativo.')
    this.name = 'CannotModifyInactiveMemberError'
  }
}

export class MemberAlreadyInactiveError extends Error {
  constructor() {
    super('Este membro já está inativo.')
    this.name = 'MemberAlreadyInactiveError'
  }
}
```

Mensagens espelham `detail` do `ProblemDetails` do backend (ver
`spec.md` › "Contrato consumido") — mesmo padrão já usado por
`CannotModifyTitularError`/`CannotRemoveTitularError`.

### `hooks/useMembers.ts`

Hoje é um único `useEffect` que busca e seta `items`/`isLoading`/
`error`. Passa a expor `refetch`, inspirado no mesmo padrão já usado em
`useTransactionsQuery.ts` (`fetchPage` reaproveitado por `refetch`):

```ts
interface UseMembersResult {
  items: MemberItem[]
  isLoading: boolean
  isRefetching: boolean
  error: Error | null
  refetch: () => void
}
```

Internamente, a busca vira uma função nomeada (`fetchMembers`) chamada
tanto pelo `useEffect` de montagem (`showLoading: true`) quanto por
`refetch()` (`showLoading: false`) — **decisão central**: `refetch()`
não liga o `isLoading` de carga inicial, liga `isRefetching` em vez
disso. Sem essa separação, `MembersPage` (que hoje esconde a lista
inteira enquanto `isLoading` é `true`) piscaria a tela inteira toda vez
que alguém remove um membro, só para buscar de novo os mesmos dados na
maioria das vezes — regressão de UX desnecessária. A lista antiga
permanece visível (e interativa) durante o `refetch`, com um spinner
discreto sinalizando a atualização em andamento (ver
`components/MemberRow.tsx`/`routes/MembersPage.tsx` abaixo); erro de
uma `refetch` ainda atualiza `error` normalmente.

```ts
async function fetchMembers(showLoading: boolean): Promise<void> {
  if (showLoading) setIsLoading(true)
  else setIsRefetching(true)
  setError(null)
  try {
    const result = await membersApi.getMembers(token ?? '')
    setItems(result.items)
  } catch (err) {
    if (err instanceof SessionExpiredError) useAuthStore.getState().clearSession()
    setError(err as Error)
  } finally {
    if (showLoading) setIsLoading(false)
    else setIsRefetching(false)
  }
}

function refetch(): void {
  fetchMembers(false)
}
```

`useEffect(..., [token])` **permanece exatamente como está** — não é
escopo desta feature mexer no acoplamento hook↔token (débito já
registrado em `frontend/docs/backlog.md`, resolvido na camada de
transporte por `lib/httpClient.ts`, não aqui).

### `utils/statusLabels.ts` (novo arquivo)

```ts
import type { MemberStatus } from '../api/membersApi'

export const MEMBER_STATUS_LABEL: Record<MemberStatus, string> = {
  ConvitePendente: 'Convite pendente',
  Ativo: 'Ativo',
  Inativo: 'Inativo',
}
```

Substitui a expressão hoje hardcoded em `MemberRow.tsx`
(`member.status === 'ConvitePendente' ? 'Convite pendente' : 'Ativo'`,
que hoje rotula qualquer não-`ConvitePendente` como `'Ativo'` —
inclusive um futuro `'Inativo'`, o que seria um rótulo errado sem essa
mudança).

### `components/MemberRow.tsx`

Nova variável derivada, sem mudar a assinatura de props:
```ts
const isEffectivelyReadOnly = readOnly || member.status === 'Inativo'
```
Usada nos dois pontos que hoje checam só `readOnly`: o bloco que decide
entre seletor de papel/texto do papel, e o bloco que decide se mostra o
botão de remover. Um membro `Inativo` sempre renderiza como se fosse
`readOnly` (papel como texto, sem botão de remover), **mesmo para quem
é Titular** — não há ação de escrita que faça sentido para ele.

Subtítulo de status passa de:
```ts
{member.status === 'ConvitePendente' ? 'Convite pendente' : 'Ativo'}
```
para:
```ts
{MEMBER_STATUS_LABEL[member.status]}
```

Nenhuma mudança no tratamento de erro/otimismo do seletor de papel
(`useUpdateMemberRole`) — a reversão em caso de erro e a exibição de
`error.message` já existem hoje; só passam a mostrar a mensagem
específica de `CannotModifyInactiveMemberError` quando for o caso (US5
já satisfeita sem tocar nessa lógica).

### `components/MemberRemoveDialog.tsx`

Só o texto muda — de:
> Tem certeza que deseja remover "{email}" da conta? Essa ação não pode
> ser desfeita.

para (texto final, confirmado com o usuário):
> Tem certeza que deseja remover "{email}" da conta? A pessoa perde
> acesso imediatamente. Se ela já tiver lançamentos registrados, o
> histórico é mantido e o vínculo fica marcado como inativo em vez de
> apagado.

Diferente do rascunho anterior, esse texto não só evita prometer
apagamento definitivo — ele **explica a regra de negócio real** (FEAT-41
do backend) de forma genérica, sem precisar saber de antemão qual dos
dois desfechos vai ocorrer (o frontend não tem como saber isso antes da
chamada, ver `spec.md` › "Fora do escopo"). Continua com duas frases
curtas, mesmo tom direto dos outros dialogs de confirmação do projeto
(`CategoryDeleteDialog`/`TransactionDeleteDialog`).

Nenhuma mudança estrutural: o bloco `otherError` (qualquer erro que não
seja `NotFoundError`) já exibe `error.message` genericamente — passa a
mostrar a mensagem de `MemberAlreadyInactiveError` automaticamente
assim que essa classe existir, sem código novo no componente. `onRemoved`
continua dependendo dos mesmos dois gatilhos de hoje (sucesso e
`NotFoundError`) — **não** passa a disparar também em
`MemberAlreadyInactiveError` (esse é um erro real, exibido ao usuário
com o dialog aberto; não é "operação equivalente a sucesso" como o 404
legado já tratado).

### `routes/MembersPage.tsx`

```ts
function handleRemoved() {
  refetch() // de useMembers(); localOthers se resincroniza pelo
            // useEffect(() => setLocalOthers(others), [items]) já existente
  setRemoveTarget(null)
}
```

`MemberRemoveDialog.onRemoved` continua recebendo `(id: string)` na
assinatura (evita mudança em cascata nos testes existentes do diálogo,
que já verificam `onRemoved` chamado com o id) — `MembersPage` só
ignora o argumento agora, correção que se explica por si (a
reconciliação é sempre "buscar tudo de novo", não "remover só este
id").

Spinner discreto durante `isRefetching`, acima da lista, **sem**
esconder/desabilitar `MemberList` (a lista continua com os dados
antigos, interativa, até o `refetch` resolver):

```tsx
{!isLoading && !error && (
  <>
    {isRefetching && (
      <div
        role="status"
        style={{ display: 'flex', alignItems: 'center', gap: '8px', fontSize: '12px', opacity: 0.6 }}
      >
        <span className="je-spin" style={{ width: '14px', height: '14px', borderWidth: '2px' }} />
        Atualizando...
      </div>
    )}
    <MemberList ... />
  </>
)}
```

Reaproveita a classe `.je-spin` já existente em `modernist.css`
(usada hoje só dentro do `ProcessingOverlay`, tamanho 34px) com um
`style` inline reduzindo pra 14px/2px de borda — não introduz nenhum
componente/token novo. Deliberadamente **não** é o `ProcessingOverlay`
de tela cheia (esse veste a tela inteira e comunica "aguarde, uma ação
está em andamento"; aqui a ação (remover) já terminou e fechou o
dialog — o que resta é uma resincronização em segundo plano, que não
deve bloquear a tela).

## Decisões técnicas

1. **Reconciliar com o backend em vez de deletar localmente por
   otimismo.** Única forma de saber se um `DELETE` resultou em remoção
   de fato ou inativação, já que a resposta (204) é idêntica nos dois
   casos (ver `spec.md` › "Contexto"). Reaproveita o padrão de
   `refetch()` já estabelecido em `useTransactionsQuery.ts` — sem
   introduzir nenhuma lib nova (sem React Query; permanece fora do
   escopo desta feature, já registrado como melhoria estrutural
   separada no backlog).
2. **`refetch()` não ativa o `isLoading` de carga inicial, ativa
   `isRefetching`** — evita piscar a tela inteira a cada remoção. Por
   um instante depois do `DELETE`, a lista ainda mostra o estado antigo
   (ex.: o membro removido ainda aparece como `Ativo`) até a resposta
   do `refetch` chegar — janela pequena (uma chamada HTTP local),
   sinalizada por um spinner discreto acima da lista (ver
   `routes/MembersPage.tsx` acima), sem esconder nem desabilitar o que
   já está na tela.
3. **Status `Inativo` sempre se comporta como somente-leitura**,
   independente de quem está vendo a tela (nem o Titular tem ação
   disponível) — evita depender só da checagem 422 do backend como
   linha de defesa (defesa em profundidade: UI não oferece uma ação
   que sabidamente falha).
4. **Nenhuma mudança em `InviteMemberDialog`/`useInviteMember`** — o
   comportamento novo (reconvite de e-mail `Inativo` aceito) já é
   consequência direta do backend não bloquear mais esse caso; o
   frontend não precisa de nenhum código condicional para isso. US8
   vira só um teste (API e/ou componente) confirmando 201 nesse
   cenário, não uma mudança de comportamento.
5. **`MemberAlreadyInactiveError` não é tratado como sucesso** —
   diferente do 404 legado (`onRemoved` chamado), esse erro fica visível
   pro usuário no dialog aberto. Justificativa: o 404 significa "o
   registro já não existe, nada a fazer" (reconciliação implícita); o
   422 `member-already-inactive` significa "o registro existe, é
   `Inativo`, e essa ação não se aplica" — informação nova que vale a
   pena mostrar antes de fechar o dialog. Na prática só ocorre por
   corrida entre abas/dispositivos, já que a UI (decisão 3) não oferece
   mais o botão de remover para um membro já `Inativo` no carregamento
   normal da tela.

## Recursos AWS

Nenhum. Feature de frontend que só passa a consumir um valor de status
e dois códigos de erro já existentes no contrato do backend (FEAT-41,
já em produção/homologação) — não introduz nem altera nenhum recurso
AWS, nenhuma variável de ambiente nova, nenhuma mudança em
`frontend/infra/`.

## Mapeamento de erros de negócio

| Origem (backend) | HTTP | `type` (sufixo) | Classe frontend | Mensagem exibida |
|---|---|---|---|---|
| `PUT /members/{id}` | 422 | `cannot-modify-inactive-member` | `CannotModifyInactiveMemberError` (nova) | "Não é possível alterar o papel de um membro inativo." |
| `DELETE /members/{id}` | 422 | `member-already-inactive` | `MemberAlreadyInactiveError` (nova) | "Este membro já está inativo." |

Ambos seguem exatamente o mesmo padrão de extração (`extractErrorCode`)
e fallback (`UnknownMemberError` para qualquer 422 não reconhecido) já
usado por `cannot-modify-titular`/`cannot-remove-titular` — nenhuma
mudança nesse mecanismo, só dois `if` novos.

## Pontos que precisavam de confirmação antes do `/tasks` — resolvidos

- **Texto do diálogo de remoção**: confirmado (ver
  `components/MemberRemoveDialog.tsx` acima) — explica a regra real em
  vez de só evitar prometer apagamento definitivo.
- **Spinner durante o `refetch()` pós-remoção**: confirmado — `useMembers`
  ganha `isRefetching`, `MembersPage` mostra um spinner discreto (reaproveita
  `.je-spin` reduzido) acima da lista, sem escondê-la.

Nenhum ponto em aberto — pronto para o `/tasks`.
