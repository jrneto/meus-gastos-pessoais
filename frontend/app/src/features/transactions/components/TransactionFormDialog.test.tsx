import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ExpenseFormDialog } from './TransactionFormDialog'

const EXPENSES_URL = 'http://localhost:5049/expenses'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

const expenseDetail = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  categoryId: 'cat-1',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

function renderDialog(props: Partial<React.ComponentProps<typeof ExpenseFormDialog>> = {}) {
  return render(
    <MemoryRouter>
      <ExpenseFormDialog
        open
        onOpenChange={vi.fn()}
        onSaved={vi.fn()}
        {...props}
      />
    </MemoryRouter>,
  )
}

describe('ExpenseFormDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })))
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
      http.post(EXPENSES_URL, () => {
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
    server.use(http.post(EXPENSES_URL, () => HttpResponse.json(expenseDetail)))
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

  describe('com expenseId (modo edição, FEAT-18)', () => {
    const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

    it('mostra "Carregando..." e depois "Editar despesa" com os campos pré-preenchidos', async () => {
      server.use(http.get(EXPENSE_URL, () => HttpResponse.json(expenseDetail)))

      renderDialog({ expenseId: 'exp-1' })

      expect(screen.getByText('Editar despesa')).toBeInTheDocument()
      expect(screen.getByText('Carregando...')).toBeInTheDocument()

      expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
      expect(screen.getByRole('button', { name: /salvar alterações/i })).toBeInTheDocument()
    })

    it('404 ao carregar fecha o popup e chama onSaved, sem exibir erro', async () => {
      server.use(http.get(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))
      const onSaved = vi.fn()
      const onOpenChange = vi.fn()

      renderDialog({ expenseId: 'exp-1', onSaved, onOpenChange })

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(onOpenChange).toHaveBeenCalledWith(false)
    })

    it('editar com sucesso chama onSaved e fecha o popup', async () => {
      const user = userEvent.setup()
      server.use(
        http.get(EXPENSE_URL, () => HttpResponse.json(expenseDetail)),
        http.put(EXPENSE_URL, () => HttpResponse.json({ ...expenseDetail, description: 'Atualizado' })),
      )
      const onSaved = vi.fn()
      const onOpenChange = vi.fn()

      renderDialog({ expenseId: 'exp-1', onSaved, onOpenChange })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(onOpenChange).toHaveBeenCalledWith(false)
    })
  })
})
