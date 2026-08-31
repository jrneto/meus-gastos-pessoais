import '@/styles/modernist/modernist.css'
import { CategoryLetterTile } from '@/lib/categories/CategoryLetterTile'
import { useCategories } from '@/lib/categories/useCategories'
import type { SummaryTransactionItem } from '../api/summaryApi'
import { formatCentsToCurrency } from '@/lib/currency'

interface RecentTransactionsListProps {
  items: SummaryTransactionItem[]
}

// "Últimos lançamentos" (FEAT-26) — puramente informativo nesta
// feature (decisão fechada na spec): sem onClick, sem abrir popup de
// detalhe. Sinal/cor por tipo duplicam a fórmula já usada em
// `TransactionList`/`TransactionDetailDialog` (features/transactions),
// já que uma feature nunca importa de dentro de outra (constitution).
export function RecentTransactionsList({ items }: RecentTransactionsListProps) {
  const { items: categories, isLoading: categoriesLoading } = useCategories()

  if (items.length === 0) {
    return (
      <p className="ds-modernist" style={{ opacity: 0.55, fontSize: '13px' }}>
        Nenhuma transação neste mês.
      </p>
    )
  }

  return (
    <div className="ds-modernist" style={{ display: 'flex', flexDirection: 'column' }}>
      {items.map((item) => {
        const category = categories.find((c) => c.id === item.categoryId)
        // Enquanto `useCategories` ainda carrega, `category` vem
        // undefined mesmo pra uma categoria que existe de verdade —
        // evita o flash de "Categoria não encontrada" nesse instante
        // (a categoria some de fato só quando `categoriesLoading` já
        // terminou e ainda assim não é encontrada).
        const categoryLabel = category ? category.nome : categoriesLoading ? '' : 'Categoria não encontrada'
        const isIncome = item.tipo === 'receita'
        const amountColor = isIncome ? 'var(--color-positive-700)' : 'var(--color-accent-700)'
        const amountSign = isIncome ? '+ ' : '- '

        return (
          <div
            key={item.id}
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: 'var(--space-2)',
              padding: '10px 0',
              borderBottom: '1px solid var(--color-divider)',
            }}
          >
            <CategoryLetterTile name={category?.nome ?? (categoriesLoading ? '' : '?')} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div style={{ fontSize: '13px', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                {item.description}
              </div>
              <div style={{ fontSize: '11px', opacity: 0.55 }}>
                {categoryLabel} · {item.date}
              </div>
            </div>
            <div style={{ fontSize: '13px', fontWeight: 600, color: amountColor, whiteSpace: 'nowrap' }}>
              {amountSign}
              {formatCentsToCurrency(item.amountInCents)}
            </div>
          </div>
        )
      })}
    </div>
  )
}
