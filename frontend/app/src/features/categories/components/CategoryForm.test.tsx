import { render, screen, waitFor } from '@testing-library/react'
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

describe('CategoryForm', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('não exibe campos de Cor nem Ícone', () => {
    renderForm()

    expect(screen.queryByLabelText('Cor')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Ícone')).not.toBeInTheDocument()
  })

  it('inicia com o tipo Despesa selecionado e o campo de teto visível', () => {
    renderForm()

    expect(screen.getByRole('radio', { name: 'Despesa' })).toBeChecked()
    expect(screen.getByLabelText('Teto mensal (R$)')).toBeInTheDocument()
  })

  describe('mode="create" (padrão)', () => {
    it('exibe erro inline e não chama a API sem nome', async () => {
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
      expect(apiCalled).toBe(false)
    })

    it('cria categoria de despesa sem teto', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.post(CATEGORIES_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Assinaturas',
            tipo: 'despesa',
            orcamentoMensalCents: null,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ onSaved })
      await user.type(screen.getByLabelText('Nome'), 'Assinaturas')
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(screen.getByLabelText('Nome')).toHaveValue('')
      expect(sentPayload).toEqual({ nome: 'Assinaturas', tipo: 'despesa' })
    })

    it('cria categoria de despesa com teto válido, convertendo para centavos', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.post(CATEGORIES_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Alimentação',
            tipo: 'despesa',
            orcamentoMensalCents: 80000,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ onSaved })
      await user.type(screen.getByLabelText('Nome'), 'Alimentação')
      await user.type(screen.getByLabelText('Teto mensal (R$)'), '800,00')
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(sentPayload).toEqual({ nome: 'Alimentação', tipo: 'despesa', orcamentoMensalCents: 80000 })
    })

    it('cria categoria de receita, sem exibir o campo de teto', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.post(CATEGORIES_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Salário',
            tipo: 'receita',
            orcamentoMensalCents: null,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ onSaved })
      await user.type(screen.getByLabelText('Nome'), 'Salário')
      await user.click(screen.getByRole('radio', { name: 'Receita' }))

      expect(screen.queryByLabelText('Teto mensal (R$)')).not.toBeInTheDocument()

      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(sentPayload).toEqual({ nome: 'Salário', tipo: 'receita' })
    })

    it('trocar de Despesa para Receita esconde e descarta o teto preenchido', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.post(CATEGORIES_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Freelance',
            tipo: 'receita',
            orcamentoMensalCents: null,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ onSaved })
      await user.type(screen.getByLabelText('Nome'), 'Freelance')
      await user.type(screen.getByLabelText('Teto mensal (R$)'), '500,00')
      await user.click(screen.getByRole('radio', { name: 'Receita' }))
      await user.click(screen.getByRole('radio', { name: 'Despesa' }))

      expect(screen.getByLabelText('Teto mensal (R$)')).toHaveValue('')

      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(sentPayload).toEqual({ nome: 'Freelance', tipo: 'despesa' })
    })

    it('teto em formato inválido bloqueia o submit com mensagem no campo', async () => {
      const user = userEvent.setup()
      let apiCalled = false
      server.use(
        http.post(CATEGORIES_URL, () => {
          apiCalled = true
          return HttpResponse.json({})
        }),
      )

      renderForm()
      await user.type(screen.getByLabelText('Nome'), 'Alimentação')
      await user.type(screen.getByLabelText('Teto mensal (R$)'), 'abc')
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      expect(await screen.findByText('Use o formato 0,00.')).toBeInTheDocument()
      expect(apiCalled).toBe(false)
    })

    it('teto igual a zero bloqueia o submit com mensagem no campo', async () => {
      const user = userEvent.setup()
      let apiCalled = false
      server.use(
        http.post(CATEGORIES_URL, () => {
          apiCalled = true
          return HttpResponse.json({})
        }),
      )

      renderForm()
      await user.type(screen.getByLabelText('Nome'), 'Alimentação')
      await user.type(screen.getByLabelText('Teto mensal (R$)'), '0,00')
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      expect(await screen.findByText('O teto deve ser maior que zero.')).toBeInTheDocument()
      expect(apiCalled).toBe(false)
    })

    it('nome duplicado (422 name-conflict) exibe erro inline no campo nome', async () => {
      const user = userEvent.setup()
      server.use(http.post(CATEGORIES_URL, () => problem('name-conflict')))

      renderForm()
      await user.type(screen.getByLabelText('Nome'), 'Viagem')
      await user.click(screen.getByRole('button', { name: /criar categoria/i }))

      expect(await screen.findByText('Já existe uma categoria com esse nome.')).toBeInTheDocument()
    })

    it('erro 400 da API exibe "Não foi possível salvar"', async () => {
      const user = userEvent.setup()
      server.use(http.post(CATEGORIES_URL, () => new HttpResponse(null, { status: 400 })))

      renderForm()
      await user.type(screen.getByLabelText('Nome'), 'Viagem')
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
    const initialValues = { nome: 'Alimentação', tipo: 'despesa' as const, orcamentoMensal: '800,00' }

    it('renderiza pré-preenchido com o rótulo "Salvar"', () => {
      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues })

      expect(screen.getByLabelText('Nome')).toHaveValue('Alimentação')
      expect(screen.getByRole('radio', { name: 'Despesa' })).toBeChecked()
      expect(screen.getByLabelText('Teto mensal (R$)')).toHaveValue('800,00')
      expect(screen.getByRole('button', { name: /^salvar$/i })).toBeInTheDocument()
    })

    it('submit sem mudanças reenvia nome/tipo/teto e chama onSaved', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.put(CATEGORY_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Alimentação e bebidas',
            tipo: 'despesa',
            orcamentoMensalCents: 80000,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues, onSaved })
      await user.clear(screen.getByLabelText('Nome'))
      await user.type(screen.getByLabelText('Nome'), 'Alimentação e bebidas')
      await user.click(screen.getByRole('button', { name: /^salvar$/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(sentPayload).toEqual({ nome: 'Alimentação e bebidas', tipo: 'despesa', orcamentoMensalCents: 80000 })
    })

    it('trocar o tipo para Receita envia sem orcamentoMensalCents', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.put(CATEGORY_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Alimentação',
            tipo: 'receita',
            orcamentoMensalCents: null,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues, onSaved })
      await user.click(screen.getByRole('radio', { name: 'Receita' }))
      await user.click(screen.getByRole('button', { name: /^salvar$/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(sentPayload).toEqual({ nome: 'Alimentação', tipo: 'receita' })
    })

    it('remover o teto envia orcamentoMensalCents ausente', async () => {
      const user = userEvent.setup()
      const onSaved = vi.fn()
      let sentPayload: unknown = null
      server.use(
        http.put(CATEGORY_URL, async ({ request }) => {
          sentPayload = await request.json()
          return HttpResponse.json({
            id: 'cat-1',
            nome: 'Alimentação',
            tipo: 'despesa',
            orcamentoMensalCents: null,
            createdAt: '2025-06-15T12:00:00Z',
          })
        }),
      )

      renderForm({ mode: 'edit', categoryId: 'cat-1', initialValues, onSaved })
      await user.clear(screen.getByLabelText('Teto mensal (R$)'))
      await user.click(screen.getByRole('button', { name: /^salvar$/i }))

      await waitFor(() => expect(onSaved).toHaveBeenCalled())
      expect(sentPayload).toEqual({ nome: 'Alimentação', tipo: 'despesa' })
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
