import { useEffect } from 'react'
import '@/styles/modernist/modernist.css'
import { NotFoundError } from '../errors/transactionErrors'
import { useTransaction } from '../hooks/useTransaction'
import { centsToAmountInput } from '@/lib/currency'
import { TransactionForm } from './TransactionForm'

interface TransactionFormDialogProps {
  open: boolean
  onOpenChange: (open: boolean) => void
  onSaved: () => void
  transactionId?: string
  /** Usado só ao criar (sem `transactionId`) — ao editar, o tipo vem
   * da própria transação carregada (`data.tipo`), nunca deste prop. */
  tipo?: 'despesa' | 'receita'
}

// Painel próprio (`.dialog-backdrop`/`.dialog` do Modernist), mesmo
// padrão de `TransactionDeleteDialog`/`NavMoreSheet` — substitui as
// antigas rotas `/expenses/new` (FEAT-17) e `/expenses/:id/edit`
// (FEAT-18), unificando cadastro e edição no mesmo popup. Título
// alterna "despesa"/"receita" (FEAT-24) conforme o tipo em uso — sem
// seletor dentro do formulário, o tipo vem de fora (qual botão abriu
// o popup, ao criar; a própria transação, ao editar).
export function TransactionFormDialog({ open, onOpenChange, onSaved, transactionId, tipo }: TransactionFormDialogProps) {
  const isEdit = !!transactionId
  const { data, isLoading, error } = useTransaction(transactionId ?? '')
  // Fallback 'despesa' só cobre o instante de `isLoading` no modo
  // edição, antes de `data.tipo` chegar — autocorrige assim que os
  // dados carregam (ver plan.md, "Pontos a confirmar" item 2).
  const effectiveTipo: 'despesa' | 'receita' = isEdit ? (data?.tipo ?? 'despesa') : (tipo ?? 'despesa')

  useEffect(() => {
    // A transação não existe mais (excluída por outra sessão) — fecha o
    // popup silenciosamente e atualiza a listagem, sem exibir erro.
    if (open && isEdit && error instanceof NotFoundError) {
      onSaved()
      onOpenChange(false)
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, isEdit, error])

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

  function handleSuccess() {
    onSaved()
    onOpenChange(false)
  }

  return (
    <div className="ds-modernist dialog-backdrop" onClick={() => onOpenChange(false)}>
      <div
        className="dialog"
        role="dialog"
        aria-modal="true"
        aria-labelledby="transaction-form-dialog-title"
        onClick={(event) => event.stopPropagation()}
      >
        <div className="dialog-title" id="transaction-form-dialog-title">
          {isEdit
            ? (effectiveTipo === 'receita' ? 'Editar receita' : 'Editar despesa')
            : (effectiveTipo === 'receita' ? 'Nova receita' : 'Nova despesa')}
        </div>

        {isEdit && isLoading && <p>Carregando...</p>}

        {isEdit && !isLoading && data && (
          <TransactionForm
            mode="edit"
            tipo={data.tipo}
            transactionId={data.id}
            initialValues={{
              description: data.description,
              amount: centsToAmountInput(data.amountInCents),
              categoryId: data.categoryId,
              date: data.date,
            }}
            onSuccess={handleSuccess}
            onCancel={() => onOpenChange(false)}
          />
        )}

        {!isEdit && (
          <TransactionForm
            mode="create"
            tipo={effectiveTipo}
            onSuccess={handleSuccess}
            onCancel={() => onOpenChange(false)}
          />
        )}
      </div>
    </div>
  )
}
