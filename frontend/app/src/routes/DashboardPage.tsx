import { useState } from 'react'
import { Link } from 'react-router-dom'
import '@/styles/modernist/modernist.css'
import { TransactionFormDialog } from '@/features/transactions/components/TransactionFormDialog'
import { CategorySpendingList } from '@/features/summary/components/CategorySpendingList'
import { RecentTransactionsList } from '@/features/summary/components/RecentTransactionsList'
import { SummaryCards } from '@/features/summary/components/SummaryCards'
import { useSummary } from '@/features/summary/hooks/useSummary'
import { formatMonthLabel, getCurrentYearMonth } from '@/features/summary/utils/month'

type NewTransactionTarget = { tipo: 'despesa' | 'receita' } | null

// Tela "Resumo" (FEAT-26) — substitui o placeholder de HomePage.
// Sempre o mês corrente, sem navegação pra outros meses (decisão
// fechada na spec). Botões de nova despesa/receita reaproveitam o
// mesmo popup já usado em Transações (FEAT-23/24); ao salvar, o
// resumo é recarregado.
export function DashboardPage() {
  const month = getCurrentYearMonth()
  const { data, isLoading, error, refetch } = useSummary(month)
  const [newTransactionTarget, setNewTransactionTarget] = useState<NewTransactionTarget>(null)

  return (
    <div
      className="ds-modernist"
      style={{
        display: 'flex',
        flexDirection: 'column',
        gap: 'var(--space-6)',
        maxWidth: '920px',
        margin: '0 auto',
        padding: '40px 40px 60px',
        boxSizing: 'border-box',
      }}
    >
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-end' }}>
        <div>
          <h1 style={{ fontSize: '30px', margin: 0 }}>Resumo</h1>
          <div style={{ fontSize: '13px', opacity: 0.6 }}>{formatMonthLabel(month)}</div>
        </div>
        <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
          <button
            type="button"
            className="btn btn-secondary"
            onClick={() => setNewTransactionTarget({ tipo: 'receita' })}
          >
            + Nova receita
          </button>
          <button
            type="button"
            className="btn btn-primary"
            onClick={() => setNewTransactionTarget({ tipo: 'despesa' })}
          >
            + Nova despesa
          </button>
        </div>
      </div>

      {isLoading && <p style={{ opacity: 0.7, fontSize: '14px' }}>Carregando...</p>}

      {error && (
        <div style={{ color: 'var(--color-accent-700)' }}>
          <div style={{ fontWeight: 700 }}>Não foi possível carregar o resumo</div>
          <div style={{ fontSize: '13px' }}>{error.message}</div>
        </div>
      )}

      {!isLoading && !error && data && (
        <>
          <SummaryCards summary={data} />

          <div style={{ display: 'grid', gridTemplateColumns: '1.1fr 1fr', gap: 'var(--space-8)' }}>
            <div style={{ minWidth: 0 }}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'baseline',
                  gap: 'var(--space-2)',
                  marginBottom: '14px',
                }}
              >
                <h2 style={{ fontSize: '15px', margin: 0 }}>Onde o dinheiro foi este mês</h2>
                <Link to="/categories" className="btn btn-ghost" style={{ fontSize: '12px', whiteSpace: 'nowrap' }}>
                  Ver todas ({data.porCategoria.length}) →
                </Link>
              </div>
              <CategorySpendingList items={data.porCategoria} />
            </div>

            <div style={{ minWidth: 0 }}>
              <div
                style={{
                  display: 'flex',
                  justifyContent: 'space-between',
                  alignItems: 'baseline',
                  gap: 'var(--space-2)',
                  marginBottom: '14px',
                }}
              >
                <h2 style={{ fontSize: '15px', margin: 0 }}>Últimos lançamentos</h2>
                <Link
                  to={`/transactions?yearMonth=${month}`}
                  className="btn btn-ghost"
                  style={{ fontSize: '12px', whiteSpace: 'nowrap' }}
                >
                  Ver todas →
                </Link>
              </div>
              <RecentTransactionsList items={data.ultimosLancamentos} />
            </div>
          </div>
        </>
      )}

      <TransactionFormDialog
        key={newTransactionTarget ? `create-${newTransactionTarget.tipo}` : 'closed'}
        open={newTransactionTarget !== null}
        tipo={newTransactionTarget?.tipo}
        onOpenChange={(open) => !open && setNewTransactionTarget(null)}
        onSaved={refetch}
      />
    </div>
  )
}
