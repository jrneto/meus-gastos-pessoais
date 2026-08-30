import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { TransactionFormDialog } from './TransactionFormDialog'

const TRANSACTIONS_URL = 'http://localhost:5049/transactions'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const incomeCategory = {
  id: 'cat-2',
  nome: 'Salário',
  tipo: 'receita',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const transactionDetail = {
  id: 'tx-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  tipo: 'despesa',
  date: '2025-06-15',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2025-06-15T12:00:00Z',
}

const incomeTransactionDetail = {
  id: 'tx-2',
  description: 'Salário mensal',
  amountInCents: 500000,
  categoryId: 'cat-2',
  tipo: 'receita',
  date: '2025-06-05',
  createdByUserId: 'user-1',
  createdByLabel: 'Você',
  createdAt: '2025-06-05T12:00:00Z',
}

function renderDialog(props: Partial<React.ComponentProps<typeof TransactionFormDialog>> = {}) {
  return render(
    <MemoryRouter>
      <TransactionFormDialog
        open
        onOpenChange={vi.fn()}
        onSaved={vi.fn()}
        {...props}
      />
    </MemoryRouter>,
  )
}

describe('TransactionFormDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category, incomeCategory] })))
  })

  it('fica fechado quando open é false', () => {
    renderDialog({ open: false })

    expect(screen.queryByText('Nova despesa')).not.toBeInTheDocument()
  })

  it('aberto exibe o formulário de nova despesa', async () => {
    renderDialog()

    expect(screen.getByText('Nova despesa')).toBeInTheDocument()
    expect(await screen.findByLabelText('Descrição')).toBeInTheDocument()
  })

  it('fecha ao pressionar Esc', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    renderDialog({ onOpenChange })
    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    renderDialog({ onOpenChange })
    await user.click(screen.getByRole('dialog').parentElement as HTMLElement)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('"Cancelar" fecha sem chamar a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(TRANSACTIONS_URL, () => {
        apiCalled = true
        return HttpResponse.json({})
      }),
    )
    const onOpenChange = vi.fn()

    renderDialog({ onOpenChange })
    await screen.findByLabelText('Descrição')
    await user.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(apiCalled).toBe(false)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('cadastro com sucesso chama onSaved e fecha o popup', async () => {
    const user = userEvent.setup()
    server.use(http.post(TRANSACTIONS_URL, () => HttpResponse.json(transactionDetail)))
    const onSaved = vi.fn()
    const onOpenChange = vi.fn()

    renderDialog({ onSaved, onOpenChange })
    await screen.findByLabelText('Descrição')

    await user.type(screen.getByLabelText('Descrição'), 'Almoço no restaurante')
    await user.type(screen.getByLabelText('Valor'), '45,90')
    await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-1')
    // input[type=date] não aceita digitação simulada char-a-char do
    // userEvent (segmentos do date picker nativo) — fireEvent.change é a
    // forma confiável de setar o valor em testes com jsdom.
    fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2025-06-15' } })

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    await waitFor(() => expect(onSaved).toHaveBeenCalled())
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  describe('tipo="receita" (FEAT-24)', () => {
    it('criar com tipo="receita" mostra título "Nova receita" e salva com sucesso', async () => {
      const user = userEvent.setup()
      server.use(http.post(TRANSACTIONS_URL, () => HttpResponse.json(incomeTransactionDetail)))
      const onSaved = vi.fn()
      const onOpenChange = vi.fn()

      renderDialog({ tipo: 'receita', onSaved, onOpenChange })

      expect(screen.getByText('Nova receita')).toBeInTheDocument()
      await screen.findByLabelText('Descrição')

      await user.type(screen.getByLabelText('Descrição'), 'Salário mensal')
      await user.type(screen.getByLabelText('Valor'), '5000,00')
      await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-2')
      fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2025-06-05' } })

      await user.click(screen.getByRole('button', { name: /registrar receita/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(onOpenChange).toHaveBeenCalledWith(false)
    })

    it('editar uma receita mostra título "Editar receita", sem precisar passar tipo por fora', async () => {
      const TRANSACTION_URL = 'http://localhost:5049/transactions/tx-2'
      server.use(http.get(TRANSACTION_URL, () => HttpResponse.json(incomeTransactionDetail)))

      renderDialog({ transactionId: 'tx-2' })

      expect(await screen.findByText('Editar receita')).toBeInTheDocument()
      expect(await screen.findByLabelText('Descrição')).toHaveValue('Salário mensal')
    })
  })

  describe('com transactionId (modo edição, FEAT-18)', () => {
    const TRANSACTION_URL = 'http://localhost:5049/transactions/tx-1'

    it('mostra "Carregando..." e depois "Editar despesa" com os campos pré-preenchidos', async () => {
      server.use(http.get(TRANSACTION_URL, () => HttpResponse.json(transactionDetail)))

      renderDialog({ transactionId: 'tx-1' })

      expect(screen.getByText('Editar despesa')).toBeInTheDocument()
      expect(screen.getByText('Carregando...')).toBeInTheDocument()

      expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
      expect(screen.getByRole('button', { name: /salvar alterações/i })).toBeInTheDocument()
    })

    it('404 ao carregar fecha o popup e chama onSaved, sem exibir erro', async () => {
      server.use(http.get(TRANSACTION_URL, () => new HttpResponse(null, { status: 404 })))
      const onSaved = vi.fn()
      const onOpenChange = vi.fn()

      renderDialog({ transactionId: 'tx-1', onSaved, onOpenChange })

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(onOpenChange).toHaveBeenCalledWith(false)
    })

    it('editar com sucesso chama onSaved e fecha o popup', async () => {
      const user = userEvent.setup()
      server.use(
        http.get(TRANSACTION_URL, () => HttpResponse.json(transactionDetail)),
        http.put(TRANSACTION_URL, () => HttpResponse.json({ ...transactionDetail, description: 'Atualizado' })),
      )
      const onSaved = vi.fn()
      const onOpenChange = vi.fn()

      renderDialog({ transactionId: 'tx-1', onSaved, onOpenChange })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(onOpenChange).toHaveBeenCalledWith(false)
    })
  })
})
