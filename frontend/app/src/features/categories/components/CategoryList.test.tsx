import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { CategoryItem } from '@/lib/categories/types'
import { CategoryList } from './CategoryList'

const item: CategoryItem = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#f97316',
  icone: 'utensils',
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

  it('renderiza os itens com nome/cor/ícone', () => {
    renderCategoryList({ items: [item] })

    expect(screen.getByText('Alimentação')).toBeInTheDocument()
  })

  it('exibe estado vazio sem CTA (a ação já vive no botão da página)', () => {
    renderCategoryList()

    expect(
      screen.getByText('Você ainda não tem nenhuma categoria cadastrada.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /criar categoria/i })).not.toBeInTheDocument()
  })

  it('clicar em editar chama onEditToggle com o id do item', async () => {
    const user = userEvent.setup()
    const onEditToggle = vi.fn()
    renderCategoryList({ items: [item], onEditToggle })

    await user.click(screen.getByRole('button', { name: /editar categoria/i }))

    expect(onEditToggle).toHaveBeenCalledWith('cat-1')
  })

  it('quando editingId corresponde ao item, mostra o CategoryForm pré-preenchido', () => {
    renderCategoryList({ items: [item], editingId: 'cat-1' })

    expect(screen.getByLabelText('Nome')).toHaveValue('Alimentação')
    expect(screen.getByRole('button', { name: 'Alimentação' })).toHaveAttribute('aria-pressed', 'true')
  })

  it('confirmar a exclusão chama a API e onDeleted com o id correto', async () => {
    const user = userEvent.setup()
    server.use(
      http.delete('http://localhost:5049/categories/cat-1', () => new HttpResponse(null, { status: 204 })),
    )
    const onDeleted = vi.fn()

    renderCategoryList({ items: [item], onDeleted })

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

    renderCategoryList({ items: [item], onDeleted })

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
