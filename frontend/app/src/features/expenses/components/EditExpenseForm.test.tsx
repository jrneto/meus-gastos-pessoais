import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { ExpenseDetail } from '../api/expensesApi'
import { EditExpenseForm } from './EditExpenseForm'

const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'

const expense: ExpenseDetail = {
  id: 'exp-1',
  description: 'Almoço no restaurante',
  amountInCents: 4590,
  category: 'Alimentacao',
  expenseDate: '2025-06-15',
  createdAt: '2025-06-15T12:00:00Z',
}

function renderEditForm() {
  return render(
    <MemoryRouter initialEntries={['/expenses/exp-1/edit']}>
      <Routes>
        <Route path="/expenses" element={<div>Expenses List Page</div>} />
        <Route path="/expenses/:id/edit" element={<EditExpenseForm expense={expense} />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('EditExpenseForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('pré-preenche o formulário com os dados da despesa', () => {
    renderEditForm()

    expect(screen.getByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
    expect(screen.getByLabelText('Valor')).toHaveValue('45,90')
    expect(screen.getByLabelText('Data')).toHaveValue('2025-06-15')
    expect(screen.getByRole('combobox', { name: 'Categoria' })).toHaveTextContent('Alimentação')
  })

  it('exibe erro inline e não chama a API ao apagar um campo obrigatório', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.put(EXPENSE_URL, () => {
        apiCalled = true
        return HttpResponse.json(expense)
      }),
    )

    renderEditForm()
    await user.clear(screen.getByLabelText('Descrição'))
    await user.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText('Informe a descrição.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('salvar com sucesso navega para /expenses', async () => {
    const user = userEvent.setup()
    server.use(
      http.put(EXPENSE_URL, () =>
        HttpResponse.json({ ...expense, description: 'Almoço atualizado' }),
      ),
    )

    renderEditForm()
    await user.clear(screen.getByLabelText('Descrição'))
    await user.type(screen.getByLabelText('Descrição'), 'Almoço atualizado')
    await user.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText('Expenses List Page')).toBeInTheDocument()
  })

  it('erro 400 da API exibe alerta genérico e mantém os dados preenchidos', async () => {
    const user = userEvent.setup()
    server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 400 })))

    renderEditForm()
    await user.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText('Não foi possível salvar')).toBeInTheDocument()
    expect(screen.getByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
  })

  it('erro 404 ao salvar troca o formulário por ExpenseNotFound', async () => {
    const user = userEvent.setup()
    server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

    renderEditForm()
    await user.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText('Despesa não encontrada.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /voltar à listagem/i })).toBeInTheDocument()
  })

  it('cancelar navega para /expenses sem chamar a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.put(EXPENSE_URL, () => {
        apiCalled = true
        return HttpResponse.json(expense)
      }),
    )

    renderEditForm()
    await user.click(screen.getByRole('link', { name: /cancelar/i }))

    await waitFor(() => expect(screen.getByText('Expenses List Page')).toBeInTheDocument())
    expect(apiCalled).toBe(false)
  })
})
