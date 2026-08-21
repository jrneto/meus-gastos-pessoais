import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { EditCategoryPage } from './EditCategoryPage'

const CATEGORIES_URL = 'http://localhost:5049/categories'

function renderPage(id = 'cat-1') {
  return render(
    <MemoryRouter initialEntries={[`/categories/${id}/edit`]}>
      <Routes>
        <Route path="/categories" element={<div>Categories List Page</div>} />
        <Route path="/categories/:id/edit" element={<EditCategoryPage />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('EditCategoryPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('exibe estado de carregamento e depois o formulário pré-preenchido', async () => {
    server.use(
      http.get(CATEGORIES_URL, () =>
        HttpResponse.json({
          items: [
            {
              id: 'cat-1',
              nome: 'Alimentação',
              cor: '#f97316',
              icone: 'utensils',
              createdAt: '2025-06-15T12:00:00Z',
            },
          ],
        }),
      ),
    )

    renderPage()

    expect(screen.getByText('Carregando...')).toBeInTheDocument()
    expect(await screen.findByLabelText('Nome')).toHaveValue('Alimentação')
  })

  it('categoria não encontrada na lista renderiza CategoryNotFound', async () => {
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [] })))

    renderPage('inexistente')

    expect(await screen.findByText('Categoria não encontrada.')).toBeInTheDocument()
  })
})
