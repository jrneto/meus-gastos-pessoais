# Plan — FEAT-22: Categorias — tipo e orçamento mensal

## Camadas afetadas

Principal: `frontend/app/src/features/categories/` e
`frontend/app/src/lib/categories/`. Efeito colateral necessário: mover
o utilitário de moeda de `features/expenses/utils/currency.ts` para
`lib/` (ver "Decisões técnicas" item 1) — únicas mudanças fora de
`categories` nesta feature.

| Camada | O que muda |
|---|---|
| `lib/currency.ts` (novo) | `parseCurrencyToCents`, `formatCentsToCurrency`, `centsToAmountInput` — movidos de `features/expenses/utils/currency.ts` (mesma implementação, só de lugar) |
| `features/expenses/utils/currency.ts` | removido; importadores (`expenseSchema.ts`, `expenseFilterSchema.ts`, `ExpenseFormDialog.tsx`, + testes) passam a importar de `@/lib/currency` |
| `lib/categories/types.ts` | `CategoryItem` perde `cor`/`icone`, ganha `tipo: 'despesa' \| 'receita'` e `orcamentoMensalCents: number \| null` |
| `features/categories/schemas/categorySchema.ts` | reescrito: `nome`, `tipo`, `orcamentoMensal` (string bruta, condicional) → `orcamentoMensalCents` |
| `features/categories/api/categoriesWriteApi.ts` | `CategoryPayload` troca `cor`/`icone` por `tipo`/`orcamentoMensalCents?` |
| `features/categories/components/CategoryForm.tsx` | ganha seletor de tipo (`.seg`/`.seg-opt`) e campo de teto mensal condicional (só quando tipo = despesa) |
| `features/categories/components/CategoryList.tsx` | agrupa itens em duas seções (despesa/receita) em vez de lista única; mostra badge de tipo e teto (ou "sem teto") por item de despesa |
| `lib/categories/CategoryLetterTile.tsx` | ganha prop opcional `tipo` para aplicar a cor do tile (accent para despesa, novo token "positive" para receita); sem prop, mantém o estilo neutro atual (nenhuma outra tela é afetada) |
| `styles/modernist/modernist.css` | novos tokens `--color-positive`, `--color-positive-100`, `--color-positive-700` (escala verde usada pelo `.dc.html` para receita — mesmo padrão de `--color-accent*`), escopados em `.ds-modernist` |
| `routes/CategoriesPage.tsx` | título ajustado para "Categorias e orçamentos" (fidelidade ao design; sem mudança estrutural) |

Não muda: `categoryErrors.ts` (nenhum erro novo — `400` já cai em
`ValidationError` existente), `useRegisterCategory`/`useUpdateCategory`/
`useDeleteCategory`/`useCategories` (assinatura inalterada, só o tipo
`CategoryFormOutput`/`CategoryItem` que passam muda), `CategoryDeleteDialog`
(exclusão sem mudança de comportamento), `categoriesReadApi.ts` (sem
filtro `?tipo=` nesta feature, conforme "Fora do escopo" da spec).

`features/categories/schemas/categorySchema.ts` deixa de importar
`CATEGORY_ICONS` — `lib/categories/categoryIcons.ts` fica órfão (usado
só por ele hoje); removido junto, sem substituto (não há mais seletor
de ícone).

## Contratos técnicos

### `lib/categories/types.ts`

```ts
export interface CategoryItem {
  id: string
  nome: string
  tipo: 'despesa' | 'receita'
  orcamentoMensalCents: number | null
  createdAt: string
}
```

### `features/categories/schemas/categorySchema.ts`

Mesmo padrão já usado em `expenseFilterSchema.ts` (`object` →
`.transform` → `.refine` com `path` apontando pro campo errado):

```ts
const CURRENCY_REGEX = /^\d+(\.\d{3})*(,\d{2})?$/

export const categorySchema = z
  .object({
    nome: z.string().trim().min(1, 'Informe o nome.').max(50, 'O nome deve ter no máximo 50 caracteres.'),
    tipo: z.enum(['despesa', 'receita'], { message: 'Selecione o tipo da categoria.' }),
    orcamentoMensal: z
      .string()
      .optional()
      .refine((value) => !value || CURRENCY_REGEX.test(value), 'Use o formato 0,00.'),
  })
  .transform((data) => ({
    nome: data.nome,
    tipo: data.tipo,
    orcamentoMensalCents:
      data.tipo === 'despesa' && data.orcamentoMensal
        ? parseCurrencyToCents(data.orcamentoMensal)
        : undefined,
  }))
  .refine((data) => data.orcamentoMensalCents === undefined || data.orcamentoMensalCents > 0, {
    message: 'O teto deve ser maior que zero.',
    path: ['orcamentoMensal'],
  })

export type CategoryFormInput = z.input<typeof categorySchema>   // { nome, tipo, orcamentoMensal? }
export type CategoryFormOutput = z.output<typeof categorySchema>  // { nome, tipo, orcamentoMensalCents? }
```

Trocar o tipo para Receita no formulário (US4 da spec) não precisa de
lógica no schema — quem descarta o valor de `orcamentoMensal` é o
próprio `CategoryForm` (`resetField`/limpar o campo ao trocar de tipo),
e o `transform` já ignora `orcamentoMensal` quando `tipo !== 'despesa'`
como segunda camada de proteção.

### `features/categories/api/categoriesWriteApi.ts`

```ts
export interface CategoryPayload {
  nome: string
  tipo: 'despesa' | 'receita'
  orcamentoMensalCents?: number
}
```

`createCategory`/`updateCategory` inalterados (só o tipo do parâmetro
`payload` muda) — `assertWriteOk` já cobre `400`/`401`/`404`/`422` sem
necessidade de novo caso.

### `CategoryForm.tsx` — seletor de tipo e campo condicional

```tsx
const tipo = watch('tipo')

<div className="seg">
  <label className="seg-opt">
    <input type="radio" value="despesa" {...register('tipo')} style={{ display: 'none' }} />
    Despesa
  </label>
  <label className="seg-opt">
    <input type="radio" value="receita" {...register('tipo')} style={{ display: 'none' }} />
    Receita
  </label>
</div>

{tipo === 'despesa' && (
  <label className="field">
    <span>Teto mensal (R$)</span>
    <input className="input" placeholder="0,00" {...register('orcamentoMensal')} />
  </label>
)}
```

Ao trocar de Despesa para Receita, `onChange` do radio também chama
`resetField('orcamentoMensal')` — garante que o valor não sobrevive
escondido caso o usuário volte para Despesa antes de submeter (US4).

`initialValues` no modo edição (montado em `CategoryList.tsx`):
```ts
{
  nome: item.nome,
  tipo: item.tipo,
  orcamentoMensal: item.orcamentoMensalCents != null ? centsToAmountInput(item.orcamentoMensalCents) : '',
}
```
(`centsToAmountInput`, mesmo padrão já usado por `ExpenseFormDialog`
para pré-popular o campo de valor na edição.)

### `CategoryList.tsx` — agrupamento por tipo

```ts
const expenseItems = items.filter((item) => item.tipo === 'despesa')
const incomeItems = items.filter((item) => item.tipo === 'receita')
```

Duas seções (`<section>` com `<h2>` "Categorias de despesa" /
"Categorias de receita"), reaproveitando o mesmo bloco de item (nome +
`CategoryLetterTile` + botões editar/excluir) para as duas, com a
única diferença visual: item de despesa mostra teto formatado
(`formatCentsToCurrency`) ou "Sem teto definido"; item de receita não
mostra nenhum valor.

### Tokens de cor (`modernist.css`)

```css
.ds-modernist {
  /* ...tokens existentes... */
  --color-positive: oklch(45% 0.13 150);
  --color-positive-100: oklch(95% 0.04 150);
  --color-positive-700: oklch(38% 0.12 150);
}
```

Valores copiados literalmente do `.dc.html` (`oklch(45% 0.13 150)` /
`oklch(95% 0.04 150)` / `oklch(38% 0.12 150)`), mesma lógica de
"vendorizar só o que é usado" já seguida desde a FEAT-14.

## Decisões técnicas

1. **Mover `currency.ts` de `features/expenses/utils/` para `lib/`.**
   Regra de dependência da constitution (`frontend/docs/constitution.md`,
   "feature nunca importa de dentro de outra feature"; "algo usado por
   mais de uma feature sobe pra lib/") — `categorySchema.ts` precisa de
   `parseCurrencyToCents`/`formatCentsToCurrency`/`centsToAmountInput`,
   hoje presos em `features/expenses/`. Move mecânica (mesma
   implementação, atualiza os 3 importadores existentes de `expenses` +
   o próprio teste), sem mudança de comportamento em `expenses`.
2. **Sem filtro `GET /categories?tipo=` nesta feature** (decisão já
   fechada na spec) — `useCategories`/`categoriesReadApi` continuam
   buscando tudo de uma vez; o agrupamento em duas seções acontece no
   client, dentro de `CategoryList`.
3. **`CategoryLetterTile` ganha prop opcional `tipo`, com fallback
   neutro.** Evita duplicar o componente só para colorir o tile, e não
   arrisca `ExpenseDetailDialog` (que também o usa, fora do escopo
   desta feature) — como não passamos a prop lá, o visual dele não
   muda.
4. **Sem indicador de consumo/realizado** (decisão já fechada na spec,
   item 2) — `CategoryList` não faz nenhuma chamada a `GET /summary`
   nesta feature.
5. **`categoryIcons.ts` removido**, não só `cor`/`icone` do schema —
   confirmado por busca (`grep`) que nenhuma outra tela importa
   `CATEGORY_ICONS`/`findCategoryIcon` hoje.
6. **Sem toast/overlay novos** — segue o mesmo padrão inline (mensagem
   de erro/sucesso + botão com spinner) já usado em `CategoryForm`,
   mesma decisão já tomada na FEAT-21 (débito técnico registrado em
   `backlog.md`, não resolvido aqui).

## Recursos AWS

Nenhum. Consome `POST`/`PUT`/`GET`/`DELETE /categories`, já publicados
pelo backend (FEAT-21), sem infraestrutura nova.

## Mapeamento de erros

Sem mudança em relação ao que já existe — `400` (`tipo` ausente/
inválido, `orcamentoMensalCents` inválido) continua caindo em
`ValidationError` (mensagem genérica), já que o client bloqueia esses
casos antes do submit e o backend não distingue por campo:

| Origem | Condição | Exceção lançada | Mensagem exibida |
|---|---|---|---|
| `POST`/`PUT /categories` | `400` (`validation-error`) | `ValidationError` (já existe) | "Não foi possível salvar a categoria. Verifique os dados informados." |
| `POST`/`PUT /categories` | `401` | `SessionExpiredError` (já existe) | limpa sessão, mesmo fluxo atual |
| `POST`/`PUT /categories` | `404` (edição) | `NotFoundError` (já existe) | fecha o form silenciosamente (categoria removida por outra sessão) |
| `POST`/`PUT /categories` | `422` (`name-conflict`) | `NameConflictError` (já existe) | erro no campo nome |
| `DELETE /categories/{id}` | `422` (`category-in-use`) | `CategoryInUseError` (já existe) | sem mudança |

## Pontos a confirmar antes do `/tasks`

1. **Mover `currency.ts` pra `lib/` toca arquivos de `features/expenses/`
   fora do escopo direto da FEAT-22** (import path de
   `expenseSchema.ts`, `expenseFilterSchema.ts`, `ExpenseFormDialog.tsx`
   e seus testes). É uma mudança mecânica (sem alterar comportamento),
   mas confirmar que tudo bem tocar esses arquivos nesta feature em vez
   de duplicar a função só em `categories` — duplicar violaria a regra
   já documentada na constitution, mas é uma escolha explícita a validar.
2. **Novos tokens de cor `--color-positive*`** em `modernist.css` — cor
   nova (verde) que a paleta atual não tinha; confirmar que copiar os
   valores `oklch(...)` exatos do `.dc.html` é aceitável (em vez de,
   por exemplo, escolher uma cor com melhor contraste testado à mão).
3. **Título da página ajustado para "Categorias e orçamentos"** —
   mudança cosmética não pedida explicitamente nos critérios de
   aceite da spec; confirmar que é desejada ou se o título atual
   ("Categorias") deve ficar como está.
