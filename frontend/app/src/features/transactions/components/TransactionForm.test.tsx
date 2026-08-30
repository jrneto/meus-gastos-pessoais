import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { ExpenseForm } from './TransactionForm'

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

function renderForm(props: Partial<React.ComponentProps<typeof ExpenseForm>> = {}) {
  return render(
    <MemoryRouter>
      <ExpenseForm {...props} />
    </MemoryRouter>,
  )
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Descrição'), 'Almoço no restaurante')
  await user.type(screen.getByLabelText('Valor'), '45,90')
  await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-1')
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
      '/categories',
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

  it('submit com sucesso reseta o formulário e chama onSuccess', async () => {
    mockCategories()
    const user = userEvent.setup()
    const onSuccess = vi.fn()
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

    renderForm({ onSuccess })
    await screen.findByLabelText('Descrição')
    await fillValidForm(user)

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    await waitFor(() => expect(onSuccess).toHaveBeenCalled())
    expect(screen.getByLabelText('Descrição')).toHaveValue('')
    expect(screen.getByLabelText('Valor')).toHaveValue('')
  })

  it('exibe o botão "Cancelar" só quando onCancel é passado, e ele chama onCancel', async () => {
    mockCategories()
    const user = userEvent.setup()
    const onCancel = vi.fn()

    renderForm({ onCancel })
    await screen.findByLabelText('Descrição')

    await user.click(screen.getByRole('button', { name: /cancelar/i }))
    expect(onCancel).toHaveBeenCalled()
  })

  describe('mode="edit" (FEAT-18)', () => {
    const EXPENSE_URL = 'http://localhost:5049/expenses/exp-1'
    const initialValues = {
      description: 'Almoço no restaurante',
      amount: '45,90',
      categoryId: 'cat-1',
      expenseDate: '2025-06-15',
    }

    it('renderiza pré-preenchido com o rótulo "Salvar alterações"', async () => {
      mockCategories()

      renderForm({ mode: 'edit', expenseId: 'exp-1', initialValues })

      expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
      expect(screen.getByLabelText('Valor')).toHaveValue('45,90')
      // Categorias chegam de forma assíncrona (MSW) — só depois que a
      // opção existe no DOM o <select> reflete o value selecionado.
      await screen.findByRole('option', { name: 'Alimentação' })
      expect(screen.getByLabelText('Categoria')).toHaveValue('cat-1')
      expect(screen.getByLabelText('Data')).toHaveValue('2025-06-15')
      expect(screen.getByRole('button', { name: /salvar alterações/i })).toBeInTheDocument()
    })

    it('submit chama PUT /expenses/{id} e onSuccess ao ter sucesso', async () => {
      mockCategories()
      const user = userEvent.setup()
      const onSuccess = vi.fn()
      let apiCalled = false
      server.use(
        http.put(EXPENSE_URL, () => {
          apiCalled = true
          return HttpResponse.json({ ...initialValues, id: 'exp-1', amountInCents: 4590 })
        }),
      )

      renderForm({ mode: 'edit', expenseId: 'exp-1', initialValues, onSuccess })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSuccess).toHaveBeenCalled())
      expect(apiCalled).toBe(true)
    })

    it('404 ao salvar chama onSuccess silenciosamente, sem exibir erro', async () => {
      mockCategories()
      const user = userEvent.setup()
      const onSuccess = vi.fn()
      server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 404 })))

      renderForm({ mode: 'edit', expenseId: 'exp-1', initialValues, onSuccess })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSuccess).toHaveBeenCalled())
      expect(screen.queryByText('Não foi possível salvar')).not.toBeInTheDocument()
    })

    it('erro 400 exibe "Não foi possível salvar" e mantém os dados preenchidos', async () => {
      mockCategories()
      const user = userEvent.setup()
      server.use(http.put(EXPENSE_URL, () => new HttpResponse(null, { status: 400 })))

      renderForm({ mode: 'edit', expenseId: 'exp-1', initialValues })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      expect(await screen.findByText('Não foi possível salvar')).toBeInTheDocument()
      expect(screen.getByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
    })
  })
})
