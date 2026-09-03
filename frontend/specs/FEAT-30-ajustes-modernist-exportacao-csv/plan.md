# Plan — FEAT-30: Ajustes (migrar para Modernist + exportação CSV)

## Camadas afetadas

Frontend apenas (SPA React). Nenhuma mudança de backend — consome `GET
/transactions/export` (já em produção, backend FEAT-25) e reaproveita
`POST /auth/logout` (já em produção, FEAT-01/FEAT-12) sem alteração de
contrato. Toca três áreas: a página `SettingsPage`, uma feature nova
`features/settings/`, e o rodapé de conta/logout da casca de navegação
(`components/nav/`) — que a FEAT-15 deixou de fora deliberadamente.

| Arquivo | O que muda |
|---|---|
| `features/settings/api/settingsApi.ts` (novo) | `settingsApi.exportTransactionsCsv(token)` → `GET /transactions/export`, devolve `Blob`; constante `EXPORT_FILENAME = 'transacoes.csv'` |
| `features/settings/errors/settingsErrors.ts` (novo) | `SessionExpiredError`, `NetworkError`, `UnknownExportError` (mesmo padrão de `reportsErrors.ts`) |
| `features/settings/hooks/useExportTransactions.ts` (novo) | `useExportTransactions()` → `{ exportCsv, isExporting, error, success }`, mesmo esqueleto de `useInviteMember.ts` |
| `lib/downloadFile.ts` (novo) | `downloadBlob(blob, filename)` — utilitário genérico de download client-side, reaproveitável por qualquer feature futura que precise salvar um arquivo vindo da API |
| `routes/SettingsPage.tsx` (reescrito) | Modernist: título "Ajustes", linha "Exportar dados" / botão "Exportar CSV" (estado ocupado + toast de sucesso + erro inline), `<AppVersion />` mantido; **remove** o botão "Sair" |
| `components/nav/AccountFooter.tsx` (novo) | Bloco "Sua conta / Sair" (avatar "VC", ação de logout) — compartilhado entre `DesktopSidebar` e `NavMoreSheet` |
| `components/nav/DesktopSidebar.tsx` | Adiciona `<AccountFooter />` após a lista de itens, com o divisor superior do protótipo |
| `components/nav/NavMoreSheet.tsx` | Adiciona `<AccountFooter />` ao final do painel "Mais", fechando o painel antes de navegar pro login |
| `components/nav/navConfig.ts` | Item `settings`: `label: 'Configurações'` → `'Ajustes'` (o protótipo rotula o item de menu como "Ajustes", não só o título da página), `status: 'placeholder'` → `'active'` |
| `app/router.tsx` | Sem mudança — rota `settings` já aponta pra `SettingsPage` |

Não muda: `backend` (tudo já implementado), `features/auth/hooks/
useLogout.ts` (reaproveitado sem alteração, tanto pelo `AccountFooter`
quanto — até esta feature remover — por `SettingsPage`), qualquer outra
feature.

## Contratos técnicos

### `features/settings/api/settingsApi.ts`

```ts
export const EXPORT_FILENAME = 'transacoes.csv'

async function exportTransactionsCsv(token: string): Promise<Blob> {
  const response = await safeFetch(() =>
    httpClient.get('/transactions/export', {
      headers: { Authorization: `Bearer ${token}` },
    }),
  )
  assertOk(response) // 401 → SessionExpiredError; !ok → UnknownExportError
  return response.blob()
}

export const settingsApi = { exportTransactionsCsv }
```
Mesmo padrão `safeFetch`/`assertOk` de `reportsApi.ts`/`membersApi.ts`
— sem checagem de `400` dedicada (o client nunca envia filtro, decisão
3 da spec, então não é um cenário esperado em uso normal).

### `lib/downloadFile.ts`

```ts
export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = filename
  document.body.appendChild(link)
  link.click()
  document.body.removeChild(link)
  URL.revokeObjectURL(url)
}
```
Fica em `lib/` (não em `features/settings/`) por já nascer genérico —
qualquer chamada futura de download de arquivo (ex.: outro export)
reaproveita sem duplicar. Testes mockam `URL.createObjectURL`/
`revokeObjectURL` (não implementados por padrão no ambiente jsdom do
Vitest) e espionam `HTMLAnchorElement.prototype.click`.

### `features/settings/hooks/useExportTransactions.ts`

```ts
interface UseExportTransactionsResult {
  exportCsv: () => Promise<void>
  isExporting: boolean
  error: Error | null
  success: boolean
}

export function useExportTransactions(): UseExportTransactionsResult {
  // mesmo esqueleto de useInviteMember.ts: setIsExporting(true) +
  // setError(null) + setSuccess(false) no início; no catch, além de
  // setError(err), limpa a sessão quando err instanceof
  // SessionExpiredError (useAuthStore.getState().clearSession()); no
  // sucesso, downloadBlob(blob, EXPORT_FILENAME) + setSuccess(true);
  // setIsExporting(false) no finally
}
```

### `routes/SettingsPage.tsx`

```tsx
export function SettingsPage() {
  const { exportCsv, isExporting, error, success } = useExportTransactions()
  const [toastMessage, setToastMessage] = useState<string | null>(null)

  useEffect(() => {
    if (success) setToastMessage('Transações exportadas.')
  }, [success])

  // título "Ajustes" + linha "Exportar dados" / botão:
  //   <button disabled={isExporting} onClick={exportCsv}>
  //     {isExporting ? 'Exportando...' : 'Exportar CSV'}
  //   </button>
  // erro inline (role="alert") quando `error` !== null
  // <AppVersion />
  // <Toast message={toastMessage} onDismiss={() => setToastMessage(null)} />
}
```
Mesmo idioma de `useEffect` + `Toast` de `MembersPage.tsx` (`success`
como gatilho, não callback de prop — aqui não há dialog intermediário).

### `components/nav/AccountFooter.tsx`

```tsx
interface AccountFooterProps {
  onBeforeLogout?: () => void // NavMoreSheet fecha o painel antes de navegar
}

export function AccountFooter({ onBeforeLogout }: AccountFooterProps) {
  const { logout } = useLogout()
  const navigate = useNavigate()

  async function handleLogout() {
    onBeforeLogout?.()
    await logout()
    navigate('/login', { replace: true })
  }

  // avatar "VC" (mesmo tile 2 letras do protótipo — abreviação de
  // "Você", mesma convenção já usada em createdByLabel, não iniciais
  // do nome real do usuário) + rótulo "Sua conta" + <button
  // onClick={handleLogout}>Sair</button> (button real, não <div
  // onClick> como no protótipo — acessibilidade)
}
```
Reaproveita `useLogout()` sem nenhuma mudança — a única diferença do
código de hoje (`SettingsPage.tsx` atual) é onde o botão é renderizado.

### `components/nav/DesktopSidebar.tsx` / `NavMoreSheet.tsx`

`DesktopSidebar`: `<AccountFooter />` logo após o `.map(NAV_TREE)`,
sem prop (`useNavigate` já fecha/navega sozinho, não há painel pra
fechar). `NavMoreSheet`: `<AccountFooter onBeforeLogout={() =>
onOpenChange(false)} />` no fim do painel, mesmo `<div style={{
display:flex, flexDirection:'column', gap:'4px' }}>` que já lista
`MORE_ITEMS`.

## Decisões técnicas

1. **`features/settings/` como feature nova**, mesmo racional das
   demais (`reports`, `members`) levarem o nome do próprio recurso de
   negócio — mesmo a chamada de API sendo `/transactions/export`, o
   conceito de negócio aqui é "Ajustes", não uma ação de
   `features/transactions/`.
2. **`downloadBlob` em `lib/`, não em `features/settings/`** — é
   mecânica genérica do navegador (Blob → arquivo salvo), sem nenhuma
   regra de negócio; outra feature que precisar exportar algo no
   futuro reaproveita direto.
3. **Toast de sucesso, erro inline** — mesmo padrão que `MembersPage`
   já estabeleceu na FEAT-28 (`Toast` só em sucesso, nunca em erro).
   Corrige a spec original, que hesitava sobre esse componente por
   engano (ver histórico do `/specify`) — `Toast`/`ProcessingOverlay`
   já existem desde a FEAT-28, resolvendo os dois débitos técnicos que
   o backlog ainda lista como abertos (`frontend/docs/backlog.md`
   desatualizado nesse ponto, fora do escopo desta feature corrigir).
4. **Sem `ProcessingOverlay` no botão "Exportar CSV"** — decisão 5 da
   spec: ação de um clique só, sem modal, mesmo padrão mais simples de
   `CategoryForm`/`TransactionForm` (rótulo em gerúndio + `disabled`).
5. **`AccountFooter` como componente compartilhado em
   `components/nav/`**, não duplicado em `DesktopSidebar` e
   `NavMoreSheet` — mesma regra de dependência da constitution ("algo
   usado por mais de uma feature/tela sobe pra `components/`").
6. **Avatar "VC" fixo, sem consultar `GET /auth/me`** — o protótipo usa
   "VC" como abreviação de "Você" (mesma convenção já usada em
   `createdByLabel` pro autor da própria conta, FEAT-23), não iniciais
   calculadas a partir do nome real do usuário. Evita adicionar uma
   chamada de API nova à casca de navegação (que fica montada em toda
   sessão) só para um detalhe visual que o protótipo já resolve sem
   dado dinâmico.
7. **Rótulo do item de menu "Ajustes" (não mais "Configurações")** —
   o protótipo (`.dc.html`, bloco `isSet`) rotula o próprio item da
   sidebar como "Ajustes", igual ao título da página. Alinha
   `navConfig.ts` ao mesmo texto, evitando a inconsistência de um menu
   dizendo "Configurações" enquanto a página dentro dele diz "Ajustes".
8. **`status: 'active'` no item `settings`** — o comentário de
   `navConfig.ts` já registra que, desde a FEAT-15, nenhum item deveria
   ficar como placeholder; `settings` era a única exceção remanescente,
   corrigida por esta feature (`status` não tem efeito visual hoje em
   `NavItemRow`, é só metadado — corrigido para não ficar
   inconsistente).
9. **`SettingsPage.test.tsx` reescrito**: remove o teste de "Sair" (o
   comportamento migra para `AccountFooter.test.tsx`, testado a partir
   de `DesktopSidebar`/`NavMoreSheet`), mantém o teste de versão
   (`AppVersion`), adiciona os cenários de exportação (sucesso,
   loading, sessão expirada, erro de rede) via MSW.

## Recursos AWS

Nenhum. Esta feature só consome endpoints já em produção (`GET
/transactions/export` — backend FEAT-25; `POST /auth/logout` — já
existente) — nenhuma infraestrutura nova.

## Mapeamento de erros

| Origem | Condição | Exceção lançada | Tratamento |
|---|---|---|---|
| `GET /transactions/export` | `401` | `SessionExpiredError` (nova) | Limpa a sessão (`clearSession`), mesmo fluxo padrão já existente — o app redireciona pro login |
| `GET /transactions/export` | falha de rede | `NetworkError` (nova) | Mensagem inline: "Não foi possível conectar à API. Verifique sua conexão." |
| `GET /transactions/export` | outro status (não esperado em uso normal, sem filtro enviado) | `UnknownExportError` (nova) | Mensagem inline: "Ocorreu um erro inesperado ao exportar. Tente novamente." |
| `POST /auth/logout` (via `useLogout`) | qualquer falha | — (já tratado, ignorada) | Sessão local é limpa de qualquer forma (comportamento existente, sem mudança) |

## Pontos a confirmar antes do `/tasks`

1. **Correção de decisão 5/6 da spec** (feita neste `/plan`): a spec
   original tratava `ProcessingOverlay`/`Toast` como débitos técnicos
   ainda abertos — na verdade já existem desde a FEAT-28. Já ajustei o
   `spec.md` para refletir isso (uso de `Toast` no sucesso da
   exportação); confirmar se o texto revisado reflete a intenção.
2. **Rótulo do menu "Configurações" → "Ajustes"** (decisão técnica 7) —
   não estava explícito no `spec.md` original nem no `backlog.md`;
   inferido diretamente do protótipo (`.dc.html`, bloco `isSet`).
   Confirmar se esse rename entra no escopo desta feature ou fica de
   fora (mantendo "Configurações" no menu, "Ajustes" só no título da
   página).
3. **Avatar "VC" fixo, sem dado real do usuário** (decisão técnica 6) —
   confirmar que está correto não personalizar o avatar/rótulo com o
   nome do usuário logado (`GET /auth/me`), já disponível via
   `useCurrentUser` (usado em `MembersPage`), mesmo sendo tecnicamente
   simples de obter.
