import '@/styles/modernist/modernist.css'
import { useEffect } from 'react'
import { CategoryLetterTile } from '@/lib/categories/CategoryLetterTile'
import { useCategories } from '@/lib/categories/useCategories'
import type { TransactionQueryItem } from '../api/transactionsApi'
import { formatCentsToCurrency } from '@/lib/currency'

interface TransactionDetailDialogProps {
  transaction: TransactionQueryItem | null
  onOpenChange: (open: boolean) => void
  onEdit: (transaction: TransactionQueryItem) => void
  onDelete: (transaction: TransactionQueryItem) => void
}

// Popup "Detalhe da despesa/receita" (FEAT-20) — sem chamada à API,
// usa o item já carregado na listagem. Só orquestra os popups de
// editar (`TransactionFormDialog`) e excluir (`TransactionDeleteDialog`)
// já existentes, sem duplicar lógica de formulário/exclusão. Ganhou a
// seção "Lançado por" na FEAT-23. Título, cor e sinal do valor
// alternam por tipo desde a FEAT-24 (mesmo padrão de
// `TransactionList`) — qualquer generalização visual além disso
// (ícone, demais ajustes finos de `19-detalhe-transacao.png`) continua
// sendo escopo da FEAT-25.
export function TransactionDetailDialog({ transaction, onOpenChange, onEdit, onDelete }: TransactionDetailDialogProps) {
  const { items: categories } = useCategories()
  const open = transaction !== null

  useEffect(() => {
    if (!open) return undefined

    function handleKeyDown(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        onOpenChange(false)
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [open, onOpenChange])

  if (!open) {
    return null
  }

  const category = categories.find((c) => c.id === transaction.categoryId)
  const isIncome = transaction.tipo === 'receita'
  const amountColor = isIncome ? 'var(--color-positive-700)' : 'var(--color-accent-700)'
  const amountSign = isIncome ? '+ ' : '- '
  const title = isIncome ? 'Detalhe da receita' : 'Detalhe da despesa'

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="transaction-detail-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="transaction-detail-title">
          {title}
        </div>

        <div>
          <div style={{ fontSize: '30px', fontWeight: 800, fontFamily: 'var(--font-heading)', color: amountColor }}>
            {amountSign}
            {formatCentsToCurrency(transaction.amountInCents)}
          </div>
          <div style={{ fontSize: '13px', opacity: 0.6 }}>{transaction.date}</div>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
          {category ? (
            <>
              <CategoryLetterTile name={category.nome} />
              <span style={{ fontSize: '14px' }}>{category.nome}</span>
            </>
          ) : (
            <span style={{ fontSize: '14px', opacity: 0.6 }}>Categoria não encontrada</span>
          )}
        </div>

        <div>
          <div style={{ fontSize: '12px', opacity: 0.6, marginBottom: '4px' }}>Lançado por</div>
          <div style={{ fontSize: '14px' }}>{transaction.createdByLabel}</div>
        </div>

        <div>
          <div style={{ fontSize: '12px', opacity: 0.6, marginBottom: '4px' }}>Descrição</div>
          <div style={{ fontSize: '14px' }}>{transaction.description}</div>
        </div>

        <div className="dialog-actions" style={{ justifyContent: 'space-between' }}>
          <button
            type="button"
            className="btn btn-ghost"
            style={{ color: 'var(--color-accent-700)' }}
            onClick={() => {
              onDelete(transaction)
              onOpenChange(false)
            }}
          >
            Excluir
          </button>
          <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
            <button
              type="button"
              className="btn btn-secondary"
              onClick={() => {
                onEdit(transaction)
                onOpenChange(false)
              }}
            >
              Editar
            </button>
            <button type="button" className="btn btn-primary" onClick={() => onOpenChange(false)}>
              Fechar
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
