import { render, screen } from '@testing-library/react'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { CategoriesPage } from './CategoriesPage'

const CATEGORIES_URL = 'http://localhost:5049/categories'

describe('CategoriesPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('carrega e lista as categorias do usuário', async () => {
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

    render(
      <MemoryRouter>
        <CategoriesPage />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
  })

  it('link "Nova categoria" aponta para /categories/new', () => {
    server.use(http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [] })))

    render(
      <MemoryRouter>
        <CategoriesPage />
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: /nova categoria/i })).toHaveAttribute(
      'href',
      '/categories/new',
    )
  })
})
