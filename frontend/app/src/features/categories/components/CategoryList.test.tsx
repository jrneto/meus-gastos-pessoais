import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { CategoryItem } from '@/lib/categories/types'
import { CategoryList } from './CategoryList'

const expenseWithBudget: CategoryItem = {
  id: 'cat-1',
  nome: 'Alimentação',
  tipo: 'despesa',
  orcamentoMensalCents: 80000,
  createdAt: '2025-06-15T12:00:00Z',
}

const expenseWithoutBudget: CategoryItem = {
  id: 'cat-2',
  nome: 'Assinaturas',
  tipo: 'despesa',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

const income: CategoryItem = {
  id: 'cat-3',
  nome: 'Salário',
  tipo: 'receita',
  orcamentoMensalCents: null,
  createdAt: '2025-06-15T12:00:00Z',
}

function renderCategoryList(props: Partial<React.ComponentProps<typeof CategoryList>> = {}) {
  return render(
    <CategoryList
      items={[]}
      isLoading={false}
      error={null}
      onDeleted={vi.fn()}
      editingId={null}
      onEditToggle={vi.fn()}
      onSaved={vi.fn()}
      onNotFound={vi.fn()}
      {...props}
    />,
  )
}

describe('CategoryList', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('exibe estado vazio sem seções quando não há categorias', () => {
    renderCategoryList()

    expect(
      screen.getByText('Você ainda não tem nenhuma categoria cadastrada.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /categorias de despesa/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('heading', { name: /categorias de receita/i })).not.toBeInTheDocument()
  })

  it('agrupa categorias de despesa e receita nas seções correspondentes', () => {
    renderCategoryList({ items: [expenseWithBudget, income] })

    const expenseSection = screen.getByRole('heading', { name: /categorias de despesa/i }).closest('section')!
    const incomeSection = screen.getByRole('heading', { name: /categorias de receita/i }).closest('section')!

    expect(within(expenseSection).getByText('Alimentação')).toBeInTheDocument()
    expect(within(incomeSection).getByText('Salário')).toBeInTheDocument()
    expect(within(incomeSection).queryByText('Alimentação')).not.toBeInTheDocument()
    expect(within(expenseSection).queryByText('Salário')).not.toBeInTheDocument()
  })

  it('categoria de despesa com teto exibe o valor formatado', () => {
    renderCategoryList({ items: [expenseWithBudget] })

    expect(screen.getByText('R$ 800,00')).toBeInTheDocument()
  })

  it('categoria de despesa sem teto exibe "Sem teto definido"', () => {
    renderCategoryList({ items: [expenseWithoutBudget] })

    expect(screen.getByText('Sem teto definido')).toBeInTheDocument()
  })

  it('categoria de receita não exibe nenhum valor monetário', () => {
    renderCategoryList({ items: [income] })

    expect(screen.queryByText('Sem teto definido')).not.toBeInTheDocument()
    expect(screen.queryByText(/^R\$/)).not.toBeInTheDocument()
  })

  it('clicar em editar chama onEditToggle com o id do item', async () => {
    const user = userEvent.setup()
    const onEditToggle = vi.fn()
    renderCategoryList({ items: [expenseWithBudget], onEditToggle })

    await user.click(screen.getByRole('button', { name: /editar categoria/i }))

    expect(onEditToggle).toHaveBeenCalledWith('cat-1')
  })

  it('quando editingId corresponde ao item, mostra o CategoryForm pré-preenchido com tipo e teto', () => {
    renderCategoryList({ items: [expenseWithBudget], editingId: 'cat-1' })

    expect(screen.getByLabelText('Nome')).toHaveValue('Alimentação')
    expect(screen.getByRole('radio', { name: 'Despesa' })).toBeChecked()
    expect(screen.getByLabelText('Teto mensal (R$)')).toHaveValue('800,00')
  })

  it('confirmar a exclusão chama a API e onDeleted com o id correto', async () => {
    const user = userEvent.setup()
    server.use(
      http.delete('http://localhost:5049/categories/cat-1', () => new HttpResponse(null, { status: 204 })),
    )
    const onDeleted = vi.fn()

    renderCategoryList({ items: [expenseWithBudget], onDeleted })

    await user.click(screen.getByRole('button', { name: /excluir categoria/i }))
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('cat-1'))
  })

  it('categoria com despesas associadas (422 category-in-use) mantém o item na lista com alerta', async () => {
    const user = userEvent.setup()
    server.use(
      http.delete('http://localhost:5049/categories/cat-1', () =>
        HttpResponse.json(
          {
            status: 422,
            title: 'Regra de negócio violada',
            detail: '...',
            type: 'https://gastosapp.dev/errors/category-in-use',
          },
          { status: 422 },
        ),
      ),
    )
    const onDeleted = vi.fn()

    renderCategoryList({ items: [expenseWithBudget], onDeleted })

    await user.click(screen.getByRole('button', { name: /excluir categoria/i }))
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(
      await screen.findByText(
        'Esta categoria não pode ser excluída enquanto houver despesas associadas a ela.',
      ),
    ).toBeInTheDocument()
    expect(onDeleted).not.toHaveBeenCalled()
    expect(screen.getByText('Alimentação')).toBeInTheDocument()
  })
})
