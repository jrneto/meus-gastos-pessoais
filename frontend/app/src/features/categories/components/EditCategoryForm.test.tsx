import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { CategoryItem } from '@/lib/categories/types'
import { EditCategoryForm } from './EditCategoryForm'

const CATEGORY_URL = 'http://localhost:5049/categories/cat-1'

const category: CategoryItem = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#f97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

function renderEditForm() {
  return render(
    <MemoryRouter initialEntries={['/categories/cat-1/edit']}>
      <Routes>
        <Route path="/categories" element={<div>Categories List Page</div>} />
        <Route path="/categories/:id/edit" element={<EditCategoryForm category={category} />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('EditCategoryForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('pré-preenche o formulário com os dados da categoria', () => {
    renderEditForm()

    expect(screen.getByLabelText('Nome')).toHaveValue('Alimentação')
    expect(screen.getByRole('button', { name: 'Alimentação' })).toHaveAttribute(
      'aria-pressed',
      'true',
    )
  })

  it('exibe erro inline e não chama a API ao apagar o nome', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.put(CATEGORY_URL, () => {
        apiCalled = true
        return HttpResponse.json(category)
      }),
    )

    renderEditForm()
    await user.clear(screen.getByLabelText('Nome'))
    await user.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText('Informe o nome.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('salvar com sucesso navega para /categories', async () => {
    const user = userEvent.setup()
    server.use(
      http.put(CATEGORY_URL, () => HttpResponse.json({ ...category, nome: 'Alimentação e bebidas' })),
    )

    renderEditForm()
    await user.clear(screen.getByLabelText('Nome'))
    await user.type(screen.getByLabelText('Nome'), 'Alimentação e bebidas')
    await user.click(screen.getByRole('button', { name: /salvar/i }))

    expect(await screen.findByText('Categories List Page')).toBeInTheDocument()
  })

  it('cancelar navega para /categories sem chamar a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.put(CATEGORY_URL, () => {
        apiCalled = true
        return HttpResponse.json(category)
      }),
    )

    renderEditForm()
    await user.click(screen.getByRole('link', { name: /cancelar/i }))

    await waitFor(() => expect(screen.getByText('Categories List Page')).toBeInTheDocument())
    expect(apiCalled).toBe(false)
  })
})
