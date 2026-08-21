import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { NewCategoryForm } from './NewCategoryForm'

const CATEGORIES_URL = 'http://localhost:5049/categories'

function problem(type: string) {
  return HttpResponse.json(
    { status: 422, title: 'Regra de negócio violada', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 422 },
  )
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Nome'), 'Viagem')
  fireEvent.change(screen.getByLabelText('Cor'), { target: { value: '#0ea5e9' } })
  await user.click(screen.getByRole('button', { name: 'Viagem' }))
}

describe('NewCategoryForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('exibe erro inline e não chama a API sem nome nem ícone', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.post(CATEGORIES_URL, () => {
        apiCalled = true
        return HttpResponse.json({})
      }),
    )

    render(<NewCategoryForm />)
    await user.click(screen.getByRole('button', { name: /criar categoria/i }))

    expect(await screen.findByText('Informe o nome.')).toBeInTheDocument()
    expect(screen.getByText('Selecione um ícone.')).toBeInTheDocument()
    expect(apiCalled).toBe(false)
  })

  it('submit com sucesso limpa o formulário e mostra confirmação', async () => {
    const user = userEvent.setup()
    server.use(
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

    render(<NewCategoryForm />)
    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: /criar categoria/i }))

    expect(await screen.findByText('Categoria criada')).toBeInTheDocument()
    await waitFor(() => expect(screen.getByLabelText('Nome')).toHaveValue(''))
  })

  it('nome duplicado (422 name-conflict) exibe erro inline no campo nome', async () => {
    const user = userEvent.setup()
    server.use(http.post(CATEGORIES_URL, () => problem('name-conflict')))

    render(<NewCategoryForm />)
    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: /criar categoria/i }))

    expect(await screen.findByText('Já existe uma categoria com esse nome.')).toBeInTheDocument()
  })

  it('erro 400 da API exibe alerta genérico', async () => {
    const user = userEvent.setup()
    server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 400 })))

    render(<NewCategoryForm />)
    await fillValidForm(user)
    await user.click(screen.getByRole('button', { name: /criar categoria/i }))

    expect(await screen.findByText('Não foi possível salvar')).toBeInTheDocument()
  })
})
