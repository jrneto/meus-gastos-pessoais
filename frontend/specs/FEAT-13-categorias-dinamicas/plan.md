# Plan — FEAT-13: Categorias dinâmicas

Referência: [`spec.md`](./spec.md), `frontend/docs/constitution.md` e o
padrão arquitetural já estabelecido em `FEAT-02` a `FEAT-06`
(`features/expenses/`). Reaproveita ao máximo o que já existe (padrão de
hooks/erros/API, `AlertDialog`, `Select`, `ExpenseFormFields` como
componente "burro") em vez de inventar um padrão novo para categorias.

Duas frentes, tratadas como uma única feature (spec já as descreve
juntas): **(A)** feature nova `features/categories/` (CRUD + tela
própria) e **(B)** migração de `features/expenses/` de `category`
(enum) para `categoryId` (referência dinâmica).

## Camadas afetadas

```
frontend/app/src/
├── lib/
│   └── categories/                           # NOVO — só a parte de LEITURA, compartilhada
│       ├── types.ts                           # CategoryItem
│       ├── categoryIcons.ts                   # lista curada {value, label, Icon} — lida por CategoryBadge e por IconPicker (escrita)
│       ├── categoriesReadApi.ts                # getCategories (única chamada usada fora de features/categories)
│       ├── categoryErrors.ts                   # SessionExpiredError, NetworkError, UnknownCategoryError (só os erros do GET)
│       ├── useCategories.ts                    # GET /categories — consumido por categories E expenses
│       └── CategoryBadge.tsx                   # ícone + nome com a cor da categoria
├── features/
│   ├── categories/                           # NOVO — CRUD, 100% exclusivo desta feature
│   │   ├── api/
│   │   │   └── categoriesWriteApi.ts          # createCategory, updateCategory, deleteCategory (usa CategoryItem de lib/categories/types)
│   │   ├── components/
│   │   │   ├── CategoryFormFields.tsx         # nome + color picker (input nativo) + IconPicker
│   │   │   ├── IconPicker.tsx                 # grade de ícones curados (lê lib/categories/categoryIcons)
│   │   │   ├── NewCategoryForm.tsx            # POST — mesmo papel de ExpenseForm
│   │   │   ├── EditCategoryForm.tsx           # PUT — mesmo papel de EditExpenseForm
│   │   │   ├── CategoryList.tsx               # lista + ações editar/excluir por item (usa CategoryBadge de lib/)
│   │   │   ├── CategoryDeleteDialog.tsx        # mesmo papel de ExpenseDeleteDialog
│   │   │   └── CategoryNotFound.tsx            # mesmo papel de ExpenseNotFound
│   │   ├── errors/
│   │   │   └── categoryErrors.ts              # ValidationError, NameConflictError, CategoryInUseError, NotFoundError (só os erros de escrita)
│   │   ├── hooks/
│   │   │   ├── useRegisterCategory.ts
│   │   │   ├── useUpdateCategory.ts
│   │   │   └── useDeleteCategory.ts
│   │   └── schemas/
│   │       └── categorySchema.ts              # nome/cor/icone (Zod)
│   └── expenses/                              # MIGRAÇÃO
│       ├── api/expensesApi.ts                  # category → categoryId em todos os payloads/params
│       ├── components/
│       │   ├── ExpenseFormFields.tsx            # Select de categoria passa a ser prop (categorias vindas de fora, não mais EXPENSE_CATEGORIES)
│       │   ├── ExpenseForm.tsx                  # + useCategories(), guarda de "sem categoria" (US10)
│       │   ├── EditExpenseForm.tsx              # idem
│       │   ├── ExpenseFilters.tsx               # Select de categoria dinâmico via useCategories()
│       │   └── ExpenseList.tsx                  # categoryLabel(value) → <CategoryBadge> resolvido via categoria carregada
│       ├── constants/expenseCategories.ts       # REMOVIDO (dead code)
│       └── schemas/
│           ├── expenseSchema.ts                 # category (enum) → categoryId (string não vazia)
│           └── expenseFilterSchema.ts           # category → categoryId
├── routes/
│   ├── CategoriesPage.tsx                       # NOVO — lista
│   ├── NewCategoryPage.tsx                      # NOVO
│   ├── EditCategoryPage.tsx                     # NOVO
│   └── ExpenseDetailPage.tsx                    # categoryLabel(value) → <CategoryBadge>
├── components/
│   └── nav/navConfig.ts                          # item "categories": disabled → active, to: '/categories'
└── app/
    └── router.tsx                                # + rotas /categories, /categories/new, /categories/:id/edit
```

## Decisões técnicas

- **Sem dependência cruzada `expenses` → `categories`.** A regra da
  constitution (`features/*` nunca importa de dentro de outra feature)
  é respeitada de verdade: só a parte de **leitura** de categoria — o
  que `expenses` de fato precisa (listar para popular `Select`, resolver
  `categoryId → nome/cor/ícone`) — sobe para `lib/categories/`
  (`CategoryItem`, `categoryIcons`, `getCategories`, `useCategories`,
  `CategoryBadge`). O CRUD (criar/editar/excluir categoria, formulários,
  erros de escrita) continua 100% isolado dentro de
  `features/categories/`, que por sua vez consome `lib/categories/`
  como qualquer feature consome `lib/` (mesmo princípio de
  `lib/httpClient`). `expenses` passa a depender só de `lib/`, nunca de
  `features/categories/`.
  **Nota sobre o precedente já existente (`expenses` importando
  `useAuthStore` de `features/auth/store/`)**: decidimos não repetir
  esse padrão aqui — na verdade ele é a mesma falha, só que num caso
  mais desculpável (sessão é infraestrutura tão transversal que, no
  próprio bulletproof-react de referência, ficaria fora de `features/`
  desde o início). Corrigi-lo agora (mover `authStore` para
  `lib/auth/`) tocaria features já mergeadas (FEAT-01/02–06/12) sem
  ganho funcional nesta feature — fica registrado como **dívida técnica
  a revisitar depois**, fora do escopo da FEAT-13.
- **Sem cache/compartilhamento de estado entre chamadas a
  `useCategories()`.** Mesmo padrão hoje usado em `useExpense`/
  `useExpensesQuery` (cada componente busca de novo ao montar, sem
  camada de cache). `ExpenseForm`, `EditExpenseForm`, `ExpenseList`,
  `ExpenseDetailPage`, `ExpenseFilters` e `CategoryList` cada um chama
  `useCategories()` independentemente — múltiplas chamadas
  `GET /categories` por tela é aceito como trade-off (mesmo padrão já
  aceito no restante do app), sem introduzir React Query ou store
  global só para isto.
- **Sem tela de detalhe de categoria.** O contrato não tem
  `GET /categories/{id}` (só `GET /categories` em lista) — diferente de
  despesas, que tem `GET /expenses/{id}`. `EditCategoryPage` busca a
  lista completa via `useCategories()` e localiza o item por `:id` da
  rota; se não encontrar (ou lista vazia), renderiza
  `CategoryNotFound`. `CategoryList` já expõe nome/cor/ícone por
  completo — não há campo adicional que justificasse uma tela de
  detalhe própria, então a lista tem ações diretas de editar/excluir por
  item (mesmo padrão visual de `ExpenseList`), sem rota `/categories/:id`
  de detalhe.
- **Cor: `<input type="color">` nativo**, sem biblioteca nova. Já
  produz o formato `#RRGGBB` exigido pelo backend, sem custo de
  dependência. Acompanhado de um texto mostrando o hex atual (leitura
  rápida), mas sem campo de texto editável separado (evita dessincronia
  entre os dois) — a spec não exige entrada manual do hex, só "color
  picker".
- **Ícone: grade curada fixa em `lib/categories/categoryIcons.ts`**,
  mapeando `value` (string kebab-case enviada como `icone`) → label +
  componente `lucide-react` já importado estaticamente (sem import
  dinâmico, mais simples e sem risco de nome inválido em runtime).
  Curadoria inicial (~16 ícones cobrindo os casos de uso comuns de
  despesa pessoal): `utensils`, `car`, `home`, `heart-pulse`,
  `graduation-cap`, `gamepad-2`, `shopping-bag`, `plane`, `wallet`,
  `coffee`, `gift`, `dumbbell`, `paw-print`, `book`, `smartphone`,
  `shirt`. `IconPicker` é um grid de botões (`aria-pressed` no
  selecionado), controlado via `Controller` do RHF (mesmo padrão já
  usado para o `Select` de categoria em `ExpenseFormFields`).
- **`CategoryBadge` fica em `lib/categories/`** (não em
  `features/categories/`), exatamente porque `expenses` também precisa
  dele — mesma razão de `useCategories`/`getCategories` estarem lá.
  Recebe `{ nome, cor, icone } | null` (null → categoria não encontrada,
  renderiza rótulo genérico "Categoria não encontrada", conforme spec).
- **Resolução de categoria em `ExpenseList`/`ExpenseDetailPage` é feita
  no componente-página, não dentro do item da lista.** Cada um chama
  `useCategories()` uma vez e monta um `Map<string, CategoryItem>` para
  resolver `categoryId → CategoryItem | undefined` por item, passado a
  `<CategoryBadge category={...} />`. Evita N chamadas a API por item
  da lista.
- **`ExpenseFormFields` deixa de importar `EXPENSE_CATEGORIES` e passa a
  receber `categories: CategoryItem[]` como prop.** Mantém o componente
  "burro" (sem hook de dados), só troca a fonte da lista de opções do
  `Select`. `ExpenseForm`/`EditExpenseForm` (componentes espertos)
  chamam `useCategories()` e repassam a prop.
- **Guarda "sem categoria" (US10) vive em `ExpenseForm`/
  `EditExpenseForm`, antes de renderizar `ExpenseFormFields`.** Se
  `useCategories()` retornar lista vazia (e não estiver carregando/com
  erro), renderiza uma mensagem + link para `/categories/new` no lugar
  do formulário — mesmo racional já usado para `ExpenseNotFound`
  substituir o formulário de edição em 404.
- **Distinção entre os dois 422 de categoria (`name-conflict` vs.
  `category-in-use`) usa o campo `type` do `ProblemDetails`**, não o
  `title` (que é genérico — "Regra de negócio violada" — para todo
  `UnprocessableEntity`, ver
  `backend/src/GastosApp.Api/Common/ResultHttpExtensions.cs`). O
  backend usa `Type = "https://gastosapp.dev/errors/{error.Code}"`
  (`error.Code` = `"name-conflict"` ou `"category-in-use"`, ver
  `backend/src/GastosApp.Application/Categories/CategoryErrors.cs`).
  `categoriesWriteApi.ts` lê o corpo da resposta 422 e extrai o último
  segmento do `type` para decidir qual erro tipado lançar.
- **`navConfig.ts`: item "categories" já existe (`disabled`), só muda
  `status` para `active` e ganha `to: '/categories'`.** Não é
  `mobilePrimary` (mesmo critério já usado para "Relatórios"/
  "Configurações" — não é ação de uso mais frequente que despesas).
  **Ponto a confirmar com você**: manter fora do `mobilePrimary`, ou
  torná-lo primário no mobile? Assumindo que não, salvo indicação
  contrária.
- **`EXPENSE_CATEGORIES` e `expenseCategories.ts` são removidos**, não
  deprecados — nenhum consumidor restante após a migração (exigência
  explícita do critério de aceite da spec).

## Contratos técnicos

Caminhos relativos a `frontend/app/src/`.

### `lib/categories/types.ts`
```ts
export interface CategoryItem {
  id: string
  nome: string
  cor: string
  icone: string
  createdAt: string
}
```

### `lib/categories/categoryIcons.ts`
```ts
import type { LucideIcon } from 'lucide-react'
import {
  Book, Car, Coffee, Dumbbell, Gamepad2, Gift, GraduationCap, HeartPulse,
  Home, PawPrint, Plane, Shirt, ShoppingBag, Smartphone, Utensils, Wallet,
} from 'lucide-react'

export interface CategoryIconOption {
  value: string
  label: string
  Icon: LucideIcon
}

export const CATEGORY_ICONS: CategoryIconOption[] = [
  { value: 'utensils', label: 'Alimentação', Icon: Utensils },
  { value: 'car', label: 'Transporte', Icon: Car },
  { value: 'home', label: 'Moradia', Icon: Home },
  { value: 'heart-pulse', label: 'Saúde', Icon: HeartPulse },
  { value: 'graduation-cap', label: 'Educação', Icon: GraduationCap },
  { value: 'gamepad-2', label: 'Lazer', Icon: Gamepad2 },
  { value: 'shopping-bag', label: 'Compras', Icon: ShoppingBag },
  { value: 'plane', label: 'Viagem', Icon: Plane },
  { value: 'wallet', label: 'Finanças', Icon: Wallet },
  { value: 'coffee', label: 'Café', Icon: Coffee },
  { value: 'gift', label: 'Presente', Icon: Gift },
  { value: 'dumbbell', label: 'Academia', Icon: Dumbbell },
  { value: 'paw-print', label: 'Pet', Icon: PawPrint },
  { value: 'book', label: 'Livros', Icon: Book },
  { value: 'smartphone', label: 'Assinaturas', Icon: Smartphone },
  { value: 'shirt', label: 'Vestuário', Icon: Shirt },
]

export function findCategoryIcon(value: string): LucideIcon | null {
  return CATEGORY_ICONS.find((icon) => icon.value === value)?.Icon ?? null
}
```

### `features/categories/schemas/categorySchema.ts`
```ts
import { z } from 'zod'
import { CATEGORY_ICONS } from '@/lib/categories/categoryIcons'

const HEX_COLOR_REGEX = /^#[0-9A-Fa-f]{6}$/
const ICON_VALUES = CATEGORY_ICONS.map((icon) => icon.value) as [string, ...string[]]

export const categorySchema = z.object({
  nome: z.string().trim().min(1, 'Informe o nome.').max(50, 'Máximo de 50 caracteres.'),
  cor: z.string().regex(HEX_COLOR_REGEX, 'Selecione uma cor.'),
  icone: z.enum(ICON_VALUES, { message: 'Selecione um ícone.' }),
})

export type CategoryFormInput = z.input<typeof categorySchema>
export type CategoryFormOutput = z.output<typeof categorySchema>
```

### `lib/categories/categoryErrors.ts` (erros do GET, compartilhados)
```ts
export class SessionExpiredError extends Error {
  constructor() {
    super('Sua sessão expirou. Faça login novamente.')
    this.name = 'SessionExpiredError'
  }
}
export class NetworkError extends Error {
  constructor() {
    super('Não foi possível conectar à API. Verifique sua conexão.')
    this.name = 'NetworkError'
  }
}
export class UnknownCategoryError extends Error {
  constructor() {
    super('Ocorreu um erro inesperado. Tente novamente.')
    this.name = 'UnknownCategoryError'
  }
}
```
Mesmo raciocínio já aplicado em cada feature hoje (`expenseErrors.ts`
define sua própria cópia de `SessionExpiredError`/`NetworkError`, não
importa de outro módulo) — aqui quem "possui" esses três erros é
`lib/categories/`, já que é o módulo que faz a chamada GET. O CRUD, em
`features/categories/`, define seu **próprio** conjunto (abaixo),
inclusive reaproveitando estes três importados de `lib/categories/`
para as respostas de escrita que também podem retornar 401/erro
desconhecido.

### `lib/categories/categoriesReadApi.ts`
```ts
export interface GetCategoriesResponse {
  items: CategoryItem[]
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> { /* igual expensesApi */ }

function assertListOk(response: Response): void {
  if (response.status === 401) throw new SessionExpiredError()
  if (!response.ok) throw new UnknownCategoryError()
}

async function getCategories(token: string): Promise<GetCategoriesResponse> {
  const response = await safeFetch(() =>
    httpClient.get('/categories', { headers: { Authorization: `Bearer ${token}` } }),
  )
  assertListOk(response)
  return response.json() as Promise<GetCategoriesResponse>
}

export const categoriesReadApi = { getCategories }
```

### `lib/categories/useCategories.ts`
```ts
interface UseCategoriesResult {
  items: CategoryItem[]
  isLoading: boolean
  error: Error | null
}

export function useCategories(): UseCategoriesResult {
  // mesmo formato de useExpensesQuery (fetch on mount, cancelled guard,
  // SessionExpiredError -> useAuthStore.getState().clearSession())
  // NOTA: useAuthStore ainda vem de features/auth/store/ — mesma
  // dependência já aceita hoje em lib/httpClient (registerAuthPlugin) e
  // em todos os hooks de expenses; não é a dependência cruzada que
  // estamos evitando aqui (essa é feature -> feature; lib -> feature
  // para consumir sessão já é o padrão estabelecido no projeto todo).
}
```

### `features/categories/errors/categoryErrors.ts` (erros de escrita)
```ts
export {
  SessionExpiredError,
  NetworkError,
  UnknownCategoryError,
} from '@/lib/categories/categoryErrors'

export class ValidationError extends Error {
  constructor() {
    super('Não foi possível salvar a categoria. Verifique os dados informados.')
    this.name = 'ValidationError'
  }
}
export class NameConflictError extends Error {
  constructor() {
    super('Já existe uma categoria com esse nome.')
    this.name = 'NameConflictError'
  }
}
export class CategoryInUseError extends Error {
  constructor() {
    super('Esta categoria não pode ser excluída enquanto houver despesas associadas a ela.')
    this.name = 'CategoryInUseError'
  }
}
export class NotFoundError extends Error {
  constructor() {
    super('Categoria não encontrada.')
    this.name = 'NotFoundError'
  }
}
```

### `features/categories/api/categoriesWriteApi.ts`
```ts
export interface CategoryPayload {
  nome: string
  cor: string
  icone: string
}

async function safeFetch(fn: () => Promise<Response>): Promise<Response> { /* igual expensesApi */ }

async function extractErrorCode(response: Response): Promise<string | null> {
  try {
    const body = (await response.json()) as { type?: string }
    return body.type?.split('/').pop() ?? null
  } catch {
    return null
  }
}

async function assertWriteOk(response: Response): Promise<void> {
  if (response.status === 400) throw new ValidationError()
  if (response.status === 401) throw new SessionExpiredError()
  if (response.status === 404) throw new NotFoundError()
  if (response.status === 422) {
    const code = await extractErrorCode(response)
    throw code === 'name-conflict' ? new NameConflictError() : new UnknownCategoryError()
  }
  if (!response.ok) throw new UnknownCategoryError()
}

async function assertDeleteOk(response: Response): Promise<void> {
  if (response.status === 401) throw new SessionExpiredError()
  if (response.status === 404) throw new NotFoundError()
  if (response.status === 422) {
    const code = await extractErrorCode(response)
    throw code === 'category-in-use' ? new CategoryInUseError() : new UnknownCategoryError()
  }
  if (!response.ok) throw new UnknownCategoryError()
}

async function createCategory(token: string, payload: CategoryPayload): Promise<CategoryItem> {
  const response = await safeFetch(() =>
    httpClient.post('/categories', payload, { headers: { Authorization: `Bearer ${token}` } }),
  )
  await assertWriteOk(response)
  return response.json() as Promise<CategoryItem>
}

async function updateCategory(token: string, id: string, payload: CategoryPayload): Promise<CategoryItem> {
  const response = await safeFetch(() =>
    httpClient.put(`/categories/${id}`, payload, { headers: { Authorization: `Bearer ${token}` } }),
  )
  await assertWriteOk(response)
  return response.json() as Promise<CategoryItem>
}

async function deleteCategory(token: string, id: string): Promise<void> {
  const response = await safeFetch(() =>
    httpClient.delete(`/categories/${id}`, { headers: { Authorization: `Bearer ${token}` } }),
  )
  await assertDeleteOk(response)
}

export const categoriesWriteApi = { createCategory, updateCategory, deleteCategory }
```
`CategoryItem` importado de `@/lib/categories/types`. Mesmo padrão de
`token` explícito por chamada já usado em `expensesApi` (consistência
com o código existente, mesmo com o interceptor de auth de `httpClient`
já cobrindo o header automaticamente desde a FEAT-12).

### `features/categories/hooks/useRegisterCategory.ts` / `useUpdateCategory.ts` / `useDeleteCategory.ts`
Mesmo formato de `useRegisterExpense`/`useUpdateExpense`/`useDeleteExpense`
(`{ isLoading, error, success }` + função de ação), parametrizados por
`CategoryFormOutput`/`id` conforme o caso. Usam `categoriesWriteApi`,
não `lib/categories/`.

### `features/categories/components/IconPicker.tsx`
```ts
interface IconPickerProps {
  value: string | undefined
  onChange: (value: string) => void
  error?: boolean
}
```
Grid (`grid grid-cols-4 gap-2` ou similar) de `<button type="button">`
por ícone de `CATEGORY_ICONS`, `aria-pressed={value === icon.value}`,
estilo do selecionado via classe condicional (`border-primary`).
Plugado ao formulário via `Controller` em `CategoryFormFields`.

### `features/categories/components/CategoryFormFields.tsx`
```ts
interface CategoryFormFieldsProps {
  register: UseFormRegister<CategoryFormInput>
  control: Control<CategoryFormInput, unknown, CategoryFormOutput>
  errors: FieldErrors<CategoryFormInput>
}
```
Campo `nome` (`Input` + `register`), campo `cor`
(`<input type="color" {...register('cor')} />` + `<span>` mostrando o
hex atual lido via `useWatch`), campo `icone` (`Controller` +
`IconPicker`). Mesmo formato de erro inline (`role="alert"`) já usado
em `ExpenseFormFields`.

### `features/categories/components/NewCategoryForm.tsx` / `EditCategoryForm.tsx`
Mesmo papel de `ExpenseForm`/`EditExpenseForm`: `useForm` +
`zodResolver(categorySchema)` + hook de ação + `Alert` de
erro/sucesso + `CategoryFormFields`. `NewCategoryForm` reseta o
formulário em sucesso (permanece na tela, como `ExpenseForm`);
`EditCategoryForm` navega para `/categories` em sucesso (como
`EditExpenseForm`). `EditCategoryForm` recebe `category: CategoryItem`
como prop (resolvida pela página a partir de `useCategories()` + `:id`).

### `lib/categories/CategoryBadge.tsx`
```ts
interface CategoryBadgeProps {
  category: Pick<CategoryItem, 'nome' | 'cor' | 'icone'> | undefined
}
```
Se `category` indefinida: `<span className="text-muted-foreground">Categoria não encontrada</span>`.
Caso contrário: ícone resolvido via `findCategoryIcon(category.icone)`
(fallback para um ícone genérico, ex. `Tag`, se por acaso não bater com
a curadoria atual — não deveria ocorrer via UI normal, mas cobre
categoria criada antes de uma mudança futura na curadoria), com
`style={{ color: category.cor }}` + `{category.nome}`.

### `features/categories/components/CategoryList.tsx` / `CategoryDeleteDialog.tsx` / `CategoryNotFound.tsx`
Mesmo formato de `ExpenseList`/`ExpenseDeleteDialog`/`ExpenseNotFound`,
usando `CategoryBadge` (de `lib/categories/`) para o item (nome+cor+
ícone) em vez de texto plano, ícone `Pencil`/`Trash2` por item
(`/categories/{id}/edit` + `CategoryDeleteDialog`). `CategoryDeleteDialog` trata
`CategoryInUseError` como o `otherError` genérico já existente no
padrão (`Alert` dentro do próprio diálogo, item permanece na lista) —
diferente de `NotFoundError`, que remove o item da lista (categoria já
não existe mais, mesmo racional do `ExpenseDeleteDialog`). Sem
paginação (`GET /categories` não pagina, conforme contrato).

### `routes/CategoriesPage.tsx` / `NewCategoryPage.tsx` / `EditCategoryPage.tsx`
Mesmo formato de `ExpensesListPage`/`RegisterExpensePage`/
`EditExpensePage`. `EditCategoryPage` usa `useCategories()` +
`useParams<{ id: string }>()`, encontra o item por `id`; se
`isLoading` → "Carregando..."; se não encontrado (lista carregada e sem
match) → `CategoryNotFound`; senão → `EditCategoryForm`.

### `features/expenses/schemas/expenseSchema.ts` (ajuste)
```ts
export const expenseSchema = z.object({
  description: z.string().trim().min(1, 'Informe a descrição.').max(200, '...'),
  amount: z.string()/* inalterado */,
  categoryId: z.string().min(1, 'Selecione uma categoria.'),
  expenseDate: z.string().min(1, 'Informe a data.'),
})
```
Sem mais dependência de `EXPENSE_CATEGORIES`/`z.enum`; validação de
existência do `categoryId` continua sendo responsabilidade do backend
(400 se inexistente/de outro usuário, já coberto pelo `ValidationError`
existente).

### `features/expenses/schemas/expenseFilterSchema.ts` (ajuste)
`category: z.string().optional()` → `categoryId: z.string().optional()`,
mesmo tratamento de `''` → `undefined` no `.transform`.

### `features/expenses/api/expensesApi.ts` (ajuste)
`category` → `categoryId` em `RegisterExpensePayload`,
`RegisterExpenseResponse`, `GetExpensesParams`, `ExpenseQueryItem`,
`UpdateExpensePayload`, `ExpenseDetail`. Nenhuma outra mudança de
comportamento (mesmos `assert*Ok`).

### `features/expenses/components/ExpenseFormFields.tsx` (ajuste)
```ts
interface ExpenseFormFieldsProps {
  register: UseFormRegister<ExpenseFormInput>
  control: Control<ExpenseFormInput, unknown, ExpenseFormOutput>
  errors: FieldErrors<ExpenseFormInput>
  categories: CategoryItem[]        // NOVO
}
```
`Select` de `category` renomeado para `categoryId`, opções vindas de
`categories` (prop) em vez de `EXPENSE_CATEGORIES` importado.

### `features/expenses/components/ExpenseForm.tsx` / `EditExpenseForm.tsx` (ajuste)
```ts
import { useCategories } from '@/lib/categories/useCategories'
// ...
const { items: categories, isLoading: categoriesLoading } = useCategories()
// ...
if (!categoriesLoading && categories.length === 0) {
  return (
    <div className="flex w-full max-w-sm flex-col items-center gap-4 py-8 text-center">
      <p className="text-sm text-muted-foreground">
        Você ainda não tem nenhuma categoria cadastrada.
      </p>
      <Button render={<Link to="/categories/new">Criar categoria</Link>} />
    </div>
  )
}
```
Passam `categories={categories}` para `ExpenseFormFields`.

### `features/expenses/components/ExpenseFilters.tsx` (ajuste)
`Select` de `category` → `categoryId`, populado por
`useCategories().items` (`@/lib/categories/useCategories`) em vez de
`EXPENSE_CATEGORIES`.

### `features/expenses/components/ExpenseList.tsx` / `routes/ExpenseDetailPage.tsx` (ajuste)
`categoryLabel(value)` removido; `useCategories()` chamado no
componente-página, `Map<string, CategoryItem>` montado uma vez, cada
item renderiza
`<CategoryBadge category={map.get(item.categoryId)} />` (importado de
`@/lib/categories/CategoryBadge`) no lugar do texto de categoria.

### `components/nav/navConfig.ts` (ajuste)
```ts
{ id: 'categories', label: 'Categorias', icon: Tag, to: '/categories', status: 'active' },
```

### `app/router.tsx` (ajuste)
```tsx
{ path: 'categories', element: <CategoriesPage /> },
{ path: 'categories/new', element: <NewCategoryPage /> },
{ path: 'categories/:id/edit', element: <EditCategoryPage /> },
```

## Novas dependências
Nenhuma. `lucide-react` já é dependência (FEAT-04); `<input type="color">`
é HTML nativo.

## Recursos AWS
**Nenhum recurso novo.** Consome `GET/POST/PUT/DELETE /categories` e o
contrato atualizado de `/expenses` (`categoryId`), já implementados e
provisionados pelo backend (FEAT-16/FEAT-17).

## Mapeamento de erros

| Cenário | Origem | Erro tipado | UI |
|---|---|---|---|
| Nome/cor/ícone inválido (client) | Zod | — | Erro inline no campo, não chama a API |
| 400 ao criar/editar categoria | `POST`/`PUT /categories` 400 | `ValidationError` | Alerta genérico, dados preservados |
| Nome duplicado | `POST`/`PUT /categories` 422 `name-conflict` | `NameConflictError` | Erro inline no campo `nome` |
| Categoria com despesas ao excluir | `DELETE /categories/{id}` 422 `category-in-use` | `CategoryInUseError` | Alerta dentro do diálogo de exclusão, categoria permanece |
| Categoria não encontrada (editar/excluir) | 404 | `NotFoundError` | `CategoryNotFound` (editar) / remove item da lista (excluir) |
| Sessão expirada | 401 em qualquer chamada | `SessionExpiredError` | `clearSession()` + redirect via `ProtectedRoute` |
| Falha de rede | `fetch` reject | `NetworkError` | Alerta genérico de conectividade |
| Erro inesperado (5xx) | API | `UnknownCategoryError` | Alerta genérico |
| Despesa sem categoria selecionada (client) | Zod | — | Erro inline, não chama a API |
| `categoryId` inexistente/de outro usuário ao salvar despesa | `POST`/`PUT /expenses` 400 | `ValidationError` (já existente) | Alerta genérico (comportamento já existente, sem mudança) |

## Testes (Vitest + Testing Library + MSW)

**Novo (`lib/categories/`)**:
- `categoriesReadApi.test.ts` — sucesso, 401 (`SessionExpiredError`),
  erro de rede, erro inesperado
- `useCategories.test.ts` — sucesso, cada erro mapeado, incluindo
  `SessionExpiredError` + `clearSession()`
- `CategoryBadge.test.tsx` — renderiza nome/cor/ícone; `category`
  indefinida renderiza rótulo genérico

**Novo (`features/categories/`)**:
- `schemas/categorySchema.test.ts` — nome/cor/ícone válidos/inválidos
- `api/categoriesWriteApi.test.ts` — mapeamento de status/`type` para
  cada erro tipado (incluindo a distinção 422 `name-conflict` vs.
  `category-in-use` via corpo da resposta)
- `hooks/useRegisterCategory.test.ts`, `useUpdateCategory.test.ts`,
  `useDeleteCategory.test.ts` — sucesso e cada erro mapeado, incluindo
  `SessionExpiredError` + `clearSession()`
- `components/IconPicker.test.tsx` — seleção via clique, `aria-pressed`
- `components/NewCategoryForm.test.tsx` / `EditCategoryForm.test.tsx` —
  validação inline, sucesso (reset vs. navegação), 422 `name-conflict`
  inline no campo nome, 400 genérico
- `components/CategoryList.test.tsx` — vazio com CTA, itens com
  nome/cor/ícone, exclusão remove item, `category-in-use` mantém item
  com alerta
- `routes/CategoriesPage.test.tsx`, `NewCategoryPage.test.tsx`,
  `EditCategoryPage.test.tsx` — integração via MSW, 404 ao editar
  renderiza `CategoryNotFound`

**Ajustado (`features/expenses/`)**:
- `schemas/expenseSchema.test.ts` / `expenseFilterSchema.test.ts` —
  `category` → `categoryId` nos casos existentes
- `components/ExpenseFormFields.test.tsx` (se existir isoladamente) ou
  cobertura via `ExpenseForm.test.tsx`/`EditExpenseForm.test.tsx` —
  `Select` populado pela prop `categories`, envia `categoryId`
- `components/ExpenseForm.test.tsx` / `EditExpenseForm.test.tsx` — +
  caso de lista de categorias vazia renderiza CTA "Criar categoria" em
  vez do formulário
- `components/ExpenseFilters.test.tsx` — filtro por `categoryId`
  dinâmico via `useCategories()` mockado (MSW)
- `components/ExpenseList.test.tsx` / `routes/ExpenseDetailPage.test.tsx`
  — renderiza `CategoryBadge` resolvido; `categoryId` sem
  correspondência renderiza rótulo genérico, sem quebrar a tela
- `components/nav/navConfig.test.ts` / `AppShell`-relacionados — item
  "Categorias" passa a `active`/navegável

Todos os testes de `expenseCategories.ts` (se existir arquivo de teste
dedicado) são removidos junto com o arquivo.

## Pontos confirmados com o usuário

1. Item "Categorias" no `navConfig.ts` **não** vira `mobilePrimary` —
   mesmo critério de "Relatórios"/"Configurações". Confirmado.
2. **Sem tela de detalhe de categoria** (`/categories/:id`) — ações de
   editar/excluir ficam inline na listagem, como em `ExpenseList`.
   Confirmado.

## Dívida técnica registrada (fora do escopo desta feature)

`features/expenses` importa `useAuthStore` diretamente de
`features/auth/store/authStore.ts` desde a FEAT-02 — a mesma categoria
de problema que esta feature evitou criar para `categories` (dependência
direta entre duas features de negócio). Diferença: sessão é
infraestrutura transversal ao app inteiro, mais desculpável que um
domínio de negócio (categoria) vazar para outro. Correção futura
sugerida, como Modo Leve, quando fizer sentido priorizar: mover
`authStore` (e só ele — telas de login/registro continuam em
`features/auth/`) para `lib/auth/`, atualizando os imports em todas as
features que hoje o consomem direto.
