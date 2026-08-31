import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { TransactionForm } from './TransactionForm'

const TRANSACTIONS_URL = 'http://localhost:5049/transactions'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const expenseCategory = {
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

function mockCategories(items: unknown[] = [expenseCategory]) {
  server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items })))
}

function renderForm(props: Partial<React.ComponentProps<typeof TransactionForm>> = {}) {
  return render(
    <MemoryRouter>
      <TransactionForm tipo="despesa" {...props} />
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

describe('TransactionForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('usuário sem nenhuma categoria cadastrada é orientado a criar uma', async () => {
    mockCategories([])

    renderForm()

    expect(
      await screen.findByText('Você ainda não tem nenhuma categoria de despesa cadastrada.'),
    ).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /criar categoria/i })).toHaveAttribute(
      'href',
      '/categories',
    )
  })

  it('usuário com só categoria de receita também é orientado a criar uma (dropdown é só de despesa)', async () => {
    mockCategories([incomeCategory])

    renderForm()

    expect(
      await screen.findByText('Você ainda não tem nenhuma categoria de despesa cadastrada.'),
    ).toBeInTheDocument()
  })

  it('dropdown de categoria não lista categoria de receita', async () => {
    mockCategories([expenseCategory, incomeCategory])

    renderForm()
    await screen.findByLabelText('Descrição')
    await screen.findByRole('option', { name: 'Alimentação' })

    expect(screen.queryByRole('option', { name: 'Salário' })).not.toBeInTheDocument()
  })

  it('exibe erros de validação inline e não chama a API com campos vazios', async () => {
    mockCategories()
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(TRANSACTIONS_URL, () => {
        apiCalled = true
        return HttpResponse.json({})
      }),
    )

    renderForm()
    await screen.findByLabelText('Descrição')

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    expect(await screen.findByText('Informe a descrição.')).toBeInTheDocument()
    expect(screen.getByText('Informe o valor.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('data vem pré-preenchida com a data atual ao criar', async () => {
    // shouldAdvanceTime: true é obrigatório aqui — sem isso, o
    // polling interno (setTimeout) do findByLabelText trava e o teste
    // estoura o timeout, sem nunca chegar ao useRealTimers() abaixo.
    vi.useFakeTimers({ shouldAdvanceTime: true })
    vi.setSystemTime(new Date(2026, 7, 30)) // 30 de agosto de 2026
    mockCategories()

    try {
      renderForm()

      expect(await screen.findByLabelText('Data')).toHaveValue('2026-08-30')
    } finally {
      vi.useRealTimers()
    }
  })

  it('limpar a data pré-preenchida e submeter exibe "Informe a data."', async () => {
    mockCategories()
    const user = userEvent.setup()

    renderForm()
    await screen.findByLabelText('Descrição')
    fireEvent.change(screen.getByLabelText('Data'), { target: { value: '' } })

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    expect(await screen.findByText('Informe a data.')).toBeInTheDocument()
  })

  it('erro 400 da API exibe alerta genérico e mantém os dados preenchidos', async () => {
    mockCategories()
    const user = userEvent.setup()
    server.use(http.post(TRANSACTIONS_URL, () => new HttpResponse(null, { status: 400 })))

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
      http.post(TRANSACTIONS_URL, () =>
        HttpResponse.json({
          id: 'tx-1',
          description: 'Almoço no restaurante',
          amountInCents: 4590,
          categoryId: 'cat-1',
          tipo: 'despesa',
          date: '2025-06-15',
          createdByUserId: 'user-1',
          createdByLabel: 'Você',
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

  describe('tipo="receita" (FEAT-24)', () => {
    it('usuário sem nenhuma categoria de receita cadastrada é orientado a criar uma', async () => {
      mockCategories([expenseCategory])

      renderForm({ tipo: 'receita' })

      expect(
        await screen.findByText('Você ainda não tem nenhuma categoria de receita cadastrada.'),
      ).toBeInTheDocument()
    })

    it('dropdown de categoria não lista categoria de despesa', async () => {
      mockCategories([expenseCategory, incomeCategory])

      renderForm({ tipo: 'receita' })
      await screen.findByLabelText('Descrição')
      await screen.findByRole('option', { name: 'Salário' })

      expect(screen.queryByRole('option', { name: 'Alimentação' })).not.toBeInTheDocument()
    })

    it('botão de submit mostra "Registrar receita"', async () => {
      mockCategories([incomeCategory])

      renderForm({ tipo: 'receita' })
      await screen.findByLabelText('Descrição')

      expect(screen.getByRole('button', { name: /registrar receita/i })).toBeInTheDocument()
    })

    it('submit com sucesso chama POST /transactions com tipo "receita"', async () => {
      mockCategories([incomeCategory])
      const user = userEvent.setup()
      const onSuccess = vi.fn()
      let receivedBody: unknown = null
      server.use(
        http.post(TRANSACTIONS_URL, async ({ request }) => {
          receivedBody = await request.json()
          return HttpResponse.json({
            id: 'tx-2',
            description: 'Salário mensal',
            amountInCents: 500000,
            categoryId: 'cat-2',
            tipo: 'receita',
            date: '2025-06-05',
            createdByUserId: 'user-1',
            createdByLabel: 'Você',
            createdAt: '2025-06-05T12:00:00Z',
          })
        }),
      )

      renderForm({ tipo: 'receita', onSuccess })
      await screen.findByLabelText('Descrição')
      await user.type(screen.getByLabelText('Descrição'), 'Salário mensal')
      await user.type(screen.getByLabelText('Valor'), '5000,00')
      await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-2')
      fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2025-06-05' } })

      await user.click(screen.getByRole('button', { name: /registrar receita/i }))

      await waitFor(() => expect(onSuccess).toHaveBeenCalled())
      expect(receivedBody).toMatchObject({ tipo: 'receita', categoryId: 'cat-2' })
    })

    it('editar receita existente chama PUT /transactions/{id} preservando tipo "receita"', async () => {
      mockCategories([incomeCategory])
      const user = userEvent.setup()
      const onSuccess = vi.fn()
      let receivedBody: unknown = null
      server.use(
        http.put('http://localhost:5049/transactions/tx-2', async ({ request }) => {
          receivedBody = await request.json()
          return HttpResponse.json({
            id: 'tx-2',
            description: 'Salário mensal',
            amountInCents: 500000,
            categoryId: 'cat-2',
            tipo: 'receita',
            date: '2025-06-05',
            createdByUserId: 'user-1',
            createdByLabel: 'Você',
            createdAt: '2025-06-05T12:00:00Z',
          })
        }),
      )

      renderForm({
        tipo: 'receita',
        mode: 'edit',
        transactionId: 'tx-2',
        initialValues: {
          description: 'Salário mensal',
          amount: '5000,00',
          categoryId: 'cat-2',
          date: '2025-06-05',
        },
        onSuccess,
      })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSuccess).toHaveBeenCalled())
      expect(receivedBody).toMatchObject({ tipo: 'receita' })
    })
  })

  describe('mode="edit" (FEAT-18)', () => {
    const TRANSACTION_URL = 'http://localhost:5049/transactions/tx-1'
    const initialValues = {
      description: 'Almoço no restaurante',
      amount: '45,90',
      categoryId: 'cat-1',
      date: '2025-06-15',
    }

    it('renderiza pré-preenchido com o rótulo "Salvar alterações"', async () => {
      mockCategories()

      renderForm({ mode: 'edit', transactionId: 'tx-1', initialValues })

      expect(await screen.findByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
      expect(screen.getByLabelText('Valor')).toHaveValue('45,90')
      // Categorias chegam de forma assíncrona (MSW) — só depois que a
      // opção existe no DOM o <select> reflete o value selecionado.
      await screen.findByRole('option', { name: 'Alimentação' })
      expect(screen.getByLabelText('Categoria')).toHaveValue('cat-1')
      expect(screen.getByLabelText('Data')).toHaveValue('2025-06-15')
      expect(screen.getByRole('button', { name: /salvar alterações/i })).toBeInTheDocument()
    })

    it('submit chama PUT /transactions/{id} e onSuccess ao ter sucesso', async () => {
      mockCategories()
      const user = userEvent.setup()
      const onSuccess = vi.fn()
      let apiCalled = false
      server.use(
        http.put(TRANSACTION_URL, () => {
          apiCalled = true
          return HttpResponse.json({ ...initialValues, id: 'tx-1', amountInCents: 4590, tipo: 'despesa' })
        }),
      )

      renderForm({ mode: 'edit', transactionId: 'tx-1', initialValues, onSuccess })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSuccess).toHaveBeenCalled())
      expect(apiCalled).toBe(true)
    })

    it('404 ao salvar chama onSuccess silenciosamente, sem exibir erro', async () => {
      mockCategories()
      const user = userEvent.setup()
      const onSuccess = vi.fn()
      server.use(http.put(TRANSACTION_URL, () => new HttpResponse(null, { status: 404 })))

      renderForm({ mode: 'edit', transactionId: 'tx-1', initialValues, onSuccess })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      await waitFor(() => expect(onSuccess).toHaveBeenCalled())
      expect(screen.queryByText('Não foi possível salvar')).not.toBeInTheDocument()
    })

    it('erro 400 exibe "Não foi possível salvar" e mantém os dados preenchidos', async () => {
      mockCategories()
      const user = userEvent.setup()
      server.use(http.put(TRANSACTION_URL, () => new HttpResponse(null, { status: 400 })))

      renderForm({ mode: 'edit', transactionId: 'tx-1', initialValues })
      await screen.findByLabelText('Descrição')

      await user.click(screen.getByRole('button', { name: /salvar alterações/i }))

      expect(await screen.findByText('Não foi possível salvar')).toBeInTheDocument()
      expect(screen.getByLabelText('Descrição')).toHaveValue('Almoço no restaurante')
    })
  })
})
