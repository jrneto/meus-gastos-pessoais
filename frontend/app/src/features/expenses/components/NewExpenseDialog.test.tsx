import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NewExpenseDialog } from './NewExpenseDialog'

const EXPENSES_URL = 'http://localhost:5049/expenses'
const CATEGORIES_URL = 'http://localhost:5049/categories'

const category = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#F97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

function renderDialog(props: Partial<React.ComponentProps<typeof NewExpenseDialog>> = {}) {
  return render(
    <MemoryRouter>
      <NewExpenseDialog
        open
        onOpenChange={vi.fn()}
        onCreated={vi.fn()}
        {...props}
      />
    </MemoryRouter>,
  )
}

describe('NewExpenseDialog', () => {
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

  it('cadastro com sucesso chama onCreated e fecha o popup', async () => {
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
    const onCreated = vi.fn()
    const onOpenChange = vi.fn()

    renderDialog({ onCreated, onOpenChange })
    await screen.findByLabelText('Descrição')

    await user.type(screen.getByLabelText('Descrição'), 'Almoço no restaurante')
    await user.type(screen.getByLabelText('Valor'), '45,90')
    await user.selectOptions(screen.getByLabelText('Categoria'), 'cat-1')
    // input[type=date] não aceita digitação simulada char-a-char do
    // userEvent (segmentos do date picker nativo) — fireEvent.change é a
    // forma confiável de setar o valor em testes com jsdom.
    fireEvent.change(screen.getByLabelText('Data'), { target: { value: '2025-06-15' } })

    await user.click(screen.getByRole('button', { name: /registrar despesa/i }))

    await waitFor(() => expect(onCreated).toHaveBeenCalled())
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })
})
