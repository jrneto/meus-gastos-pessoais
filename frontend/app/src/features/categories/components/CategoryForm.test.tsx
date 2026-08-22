import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { CategoryForm } from './CategoryForm'

const CATEGORIES_URL = 'http://localhost:5049/categories'

function problem(type: string) {
  return HttpResponse.json(
    { status: 422, title: 'Regra de negócio violada', detail: '...', type: `https://gastosapp.dev/errors/${type}` },
    { status: 422 },
  )
}

function renderForm(props: Partial<React.ComponentProps<typeof CategoryForm>> = {}) {
  return render(<CategoryForm onSaved={vi.fn()} onCancel={vi.fn()} {...props} />)
}

async function fillValidForm(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Nome'), 'Viagem')
  fireEvent.change(screen.getByLabelText('Cor'), { target: { value: '#0ea5e9' } })
  await user.click(screen.getByRole('button', { name: 'Viagem' }))
}

describe('CategoryForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  describe('mode="create" (padrão)', () => {
    it('exibe erro inline e não chama a API sem nome nem ícone', async () => {
      const user = userEvent.setup()
      let apiCalled = false
      server.use(
        http.post(CATEGORIES_URL, () => {
          apiCalled = true
          return HttpResponse.json({})
        }),
      )

      renderForm()
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      expect(await screen.findByText('Informe o nome.')).toBeInTheDocument()
      expect(screen.getByText('Selecione um ícone.')).toBeInTheDocument()
      expect(apiCalled).toBe(false)
    })

    it('submit com sucesso reseta o formulário e chama onSaved com a categoria criada', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      const created = {
        id: 'cat-1',
        nome: 'Viagem',
        cor: '#0ea5e9',
        icone: 'plane',
        createdAt: '2025-06-15T12:00:00Z',
      }
      server.use(http.post(CATEGORIES_URL, () => HttpResponse.json(created)))

      renderForm({ onSaved })
      await fillValidForm(user)
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalledWith(created))
      expect(screen.getByLabelText('Nome')).toHaveValue('')
    })

    it('nome duplicado (422 name-conflict) exibe erro inline no campo nome', async () => {
      const user = userEvent.setup()
      server.use(http.post(CATEGORIES_URL, () => problem('name-conflict')))

      renderForm()
      await fillValidForm(user)
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      expect(await screen.findByText('Já existe uma categoria com esse nome.')).toBeInTheDocument()
    })

    it('erro 400 da API exibe "Não foi possível salvar"', async () => {
      const user = userEvent.setup()
      server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 400 })))

      renderForm()
      await fillValidForm(user)
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      expect(await screen.findByText('Não foi possível salvar')).toBeInTheDocument()
    })

    it('cancelar chama onCancel sem chamar a API', async () => {
      const user = userEvent.setup()
      const onCancel = vi.fn()
      let apiCalled = false
      server.use(
        http.post(CATEGORIES_URL, () => {
          apiCalled = true
          return HttpResponse.json({})
        }),
      )

      renderForm({ onCancel })
      await user.click(screen.getByRole('button', { name: /cancelar/i }))

      expect(onCancel).toHaveBeenCalled()
      expect(apiCalled).toBe(false)
    })
  })

  describe('mode="edit"', () => {
    const CATEGORY_URL = 'http://localhost:5049/categories/cat-1'
    const initialValues = { nome: 'Alimentação', cor: '#f97316', icone: 'utensils' }

    it('renderiza pré-preenchido com o rótulo "Salvar"', () => {
      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues })

      expect(screen.getByLabelText('Nome')).toHaveValue('Alimentação')
      expect(screen.getByRole('button', { name: 'Alimentação' })).toHaveAttribute('aria-pressed', 'true')
      expect(screen.getByRole('button', { name: /^salvar$/i })).toBeInTheDocument()
    })

    it('submit com sucesso chama onSaved com a categoria atualizada', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      const updated = { ...initialValues, id: 'cat-1', nome: 'Alimentação e bebidas', createdAt: '2025-06-15T12:00:00Z' }
      server.use(http.put(CATEGORY_URL, () => HttpResponse.json(updated)))

      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues, onSaved })
      await user.clear(screen.getByLabelText('Nome'))
      await user.type(screen.getByLabelText('Nome'), 'Alimentação e bebidas')
      await user.click(screen.getByRole('button', { name: /^salvar$/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalledWith(updated))
    })

    it('404 ao salvar chama onNotFound silenciosamente, sem exibir erro', async () => {
      const user = userEvent.setup()
      const onNotFound = vi.fn()
      server.use(http.put(CATEGORY_URL, () => new HttpResponse(null, { status: 404 })))

      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues, onNotFound })
      await user.click(screen.getByRole('button', { name: /^salvar$/i }))

      await waitFor(() => expect(onNotFound).toHaveBeenCalled())
      expect(screen.queryByText('Não foi possível salvar')).not.toBeInTheDocument()
    })

    it('cancelar chama onCancel sem chamar a API', async () => {
      const user = userEvent.setup()
      const onCancel = vi.fn()
      let apiCalled = false
      server.use(
        http.put(CATEGORY_URL, () => {
          apiCalled = true
          return HttpResponse.json({})
        }),
      )

      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues, onCancel })
      await user.click(screen.getByRole('button', { name: /cancelar/i }))

      expect(onCancel).toHaveBeenCalled()
      expect(apiCalled).toBe(false)
    })
  })
})
