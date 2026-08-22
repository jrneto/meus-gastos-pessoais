import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { CategoriesPage } from './CategoriesPage'

const CATEGORIES_URL = 'http://localhost:5049/categories'

function renderPage() {
  return render(
    <MemoryRouter>
      <CategoriesPage />
    </MemoryRouter>,
  )
}

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

    renderPage()

    expect(await screen.findByText('Alimentação')).toBeInTheDocument()
  })

  it('"+ Nova categoria" expande o formulário inline e criar insere na lista (FEAT-19)', async () => {
    const user = userEvent.setup()
    server.use(
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [] })),
      http.post(CATEGORIES_URL, () =>
        HttpResponse.json({
          id: 'cat-1',
          nome: 'Viagem',
          cor: '#0ea5e9',
          icone: 'plane',
          createdAt: '2025-06-15T12:00:00Z',
        }),
      ),
    )

    renderPage()

    await user.click(screen.getByRole('button', { name: /nova categoria/i }))
    expect(screen.getByLabelText('Nome')).toBeInTheDocument()

    await user.type(screen.getByLabelText('Nome'), 'Viagem')
    await user.click(screen.getByRole('button', { name: 'Viagem' }))
    await user.click(screen.getByRole('button', { name: /criar categoria/i }))

    await waitFor(() => expect(screen.getByText('Viagem')).toBeInTheDocument())
    expect(screen.queryByLabelText('Nome')).not.toBeInTheDocument()
  })

  it('editar uma linha atualiza a lista (FEAT-19)', async () => {
    const user = userEvent.setup()
    const category = {
      id: 'cat-1',
      nome: 'Alimentação',
      cor: '#f97316',
      icone: 'utensils',
      createdAt: '2025-06-15T12:00:00Z',
    }
    server.use(
      http.get(CATEGORIES_URL, () => HttpResponse.json({ items: [category] })),
      http.put('http://localhost:5049/categories/cat-1', () =>
        HttpResponse.json({ ...category, nome: 'Alimentação e bebidas' }),
      ),
    )

    renderPage()
    await screen.findByText('Alimentação')

    await user.click(screen.getByRole('button', { name: /editar categoria/i }))
    await user.clear(screen.getByLabelText('Nome'))
    await user.type(screen.getByLabelText('Nome'), 'Alimentação e bebidas')
    await user.click(screen.getByRole('button', { name: /^salvar$/i }))

    await waitFor(() => expect(screen.getByText('Alimentação e bebidas')).toBeInTheDocument())
  })
})
