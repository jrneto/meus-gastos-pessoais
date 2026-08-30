import { useMemo } from 'react'
import '@/styles/modernist/modernist.css'
import { useCategories } from '@/lib/categories/useCategories'
import type { TransactionQueryItem } from '../api/transactionsApi'
import { formatCentsToCurrency } from '@/lib/currency'

interface TransactionListProps {
  items: TransactionQueryItem[]
  isLoading: boolean
  isLoadingMore: boolean
  error: Error | null
  hasMore: boolean
  onLoadMore: () => void
  onRowClick: (item: TransactionQueryItem) => void
}

export function TransactionList({
  items,
  isLoading,
  isLoadingMore,
  error,
  hasMore,
  onLoadMore,
  onRowClick,
}: TransactionListProps) {
  const { items: categories } = useCategories()
  const categoryById = useMemo(
    () => new Map(categories.map((category) => [category.id, category])),
    [categories],
  )

  return (
    <div className="ds-modernist" style={{ display: 'flex', width: '100%', flexDirection: 'column', gap: 'var(--space-4)' }}>
      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível buscar as despesas</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      {!isLoading && items.length === 0 && !error && (
        <p style={{ opacity: 0.55, fontSize: '13px' }}>
          Nenhuma despesa encontrada para os filtros selecionados.
        </p>
      )}

      {items.length > 0 && (
        <table className="table">
          <thead>
            <tr>
              <th>Categoria</th>
              <th>Descrição</th>
              <th>Data</th>
              <th style={{ textAlign: 'right' }}>Valor</th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const category = categoryById.get(item.categoryId)
              // Listagem já mista (despesa e receita, FEAT-23) — sinal
              // e cor por tipo, igual ao design; criar/editar continua
              // restrito a despesa nesta feature (ver TransactionForm).
              const isIncome = item.tipo === 'receita'
              const amountColor = isIncome ? 'var(--color-positive-700)' : 'var(--color-accent-700)'
              const amountSign = isIncome ? '+ ' : '- '
              return (
                <tr key={item.id} onClick={() => onRowClick(item)} style={{ cursor: 'pointer' }}>
                  <td>
                    {category ? (
                      <span className="tag tag-neutral">{category.nome}</span>
                    ) : (
                      <span style={{ opacity: 0.6 }}>Categoria não encontrada</span>
                    )}
                  </td>
                  <td>{item.description}</td>
                  <td style={{ opacity: 0.65 }}>{item.date}</td>
                  <td style={{ textAlign: 'right', fontWeight: 600, color: amountColor }}>
                    {amountSign}
                    {formatCentsToCurrency(item.amountInCents)}
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      )}

      {hasMore && (
        <button type="button" className="btn btn-secondary" onClick={onLoadMore} disabled={isLoadingMore}>
          {isLoadingMore ? 'Carregando...' : 'Carregar mais'}
        </button>
      )}
    </div>
  )
}
