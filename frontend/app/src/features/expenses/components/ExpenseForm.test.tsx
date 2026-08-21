import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ExpenseForm } from './ExpenseForm'

const EXPENSES_URL = 'http://localhost:5049/expenses'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

function mockCategories(items: unknown[] = [category]) {
  server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items })))
}

function renderForm() {
  return render(
    <MemoryRouter>
      <ExpenseForm />
    </MemoryRouter>,
  )
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Descrição'), 'Almoço no restaurante')
  await user.type(screen.getByLabelText('Valor'), '45,90')
  await user.click(screen.getByRole('combobox', { name: 'Categoria' }))
  await user.click(await screen.findByRole('option', { name: 'Alimentação' }))
  // input[type=date] não aceita digitação simulada char-a-char do
  // userEvent (segmentos do date picker nativo) — fireEvent.change é a
  // forma confiável de setar o valor em testes com jsdom.
  fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2025-06-15' } })
}

describe('ExpenseForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('usuário sem nenhuma categoria cadastrada é orientado a criar uma', async () => {
    mockCategories([])

    renderForm()

    expect(await screen.findByText('Você ainda não tem nenhuma categoria cadastrada.')).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /criar categoria/i })).toHaveAttribute(
      'href',
      '/categories/new',
    )
  })

  it('exibe erros de validação inline e não chama a API com campos vazios', async () => {
    mockCategories()
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(EXPENSES_URL, () => {
        apiCalled = true
        return HttpResponse.json({})
      }),
    )

    renderForm()
    await screen.findByLabelText('Descrição')

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    expect(await screen.findByText('Informe a descrição.')).toBeInTheDocument()
    expect(screen.getByText('Informe o valor.')).toBeInTheDocument()
    expect(screen.getByText('Informe a data.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('erro 400 da API exibe alerta genérico e mantém os dados preenchidos', async () => {
    mockCategories()
    const user = userEvent.setup()
    server.use(http.post(EXPENSES_URL, () => new HttpResponse(null, { status: 400 })))

    renderForm()
    await screen.findByLabelText('Descrição')
    await fillValidForm(user)

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    expect(await screen.findByText('Não foi possível registrar')).toBeInTheDocument()
    expect(screen.getByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
    expect(screen.getByLabelText('Valor')).toHaveValue('45,90')
  })

  it('submit com sucesso limpa o formulário e mostra confirmação', async () => {
    mockCategories()
    const user = userEvent.setup()
    server.use(
      http.post(EXPENSES_URL, () =>
        HttpResponse.json({
          id: 'exp-1',
          description: 'Almoço no restaurante',
          amountInCents: 4590,
          categoryId: 'cat-1',
          expenseDate: '2025-06-15',
          createdAt: '2025-06-15T12:00:00Z',
        }),
      ),
    )

    renderForm()
    await screen.findByLabelText('Descrição')
    await fillValidForm(user)

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    expect(await screen.findByText('Despesa registrada')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByLabelText('Descrição')).toHaveValue(''))
    expect(screen.getByLabelText('Valor')).toHaveValue('')
  })
})
