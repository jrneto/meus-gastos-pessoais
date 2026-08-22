import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import type { CategoryItem } from '@/lib/categories/types'
import { CategoryDeleteDialog } from './CategoryDeleteDialog'

const CATEGORY_URL = 'http://localhost:5049/categories/cat-1'

const category: CategoryItem = {
  id: 'cat-1',
  nome: 'Alimentação',
  cor: '#f97316',
  icone: 'utensils',
  createdAt: '2025-06-15T12:00:00Z',
}

describe('CategoryDeleteDialog', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('fica fechado quando category é null', () => {
    render(<CategoryDeleteDialog category={null} onOpenChange={vi.fn()} onDeleted={vi.fn()} />)

    expect(screen.queryByText('Excluir categoria')).not.toBeInTheDocument()
  })

  it('aberto exibe o nome da categoria', () => {
    render(<CategoryDeleteDialog category={category} onOpenChange={vi.fn()} onDeleted={vi.fn()} />)

    expect(screen.getByText('Excluir categoria')).toBeInTheDocument()
    expect(screen.getByText(/Alimentação/)).toBeInTheDocument()
  })

  it('cancelar não chama a API', async () => {
    const user = userEvent.setup()
    let apiCalled = false
    server.use(
      http.delete(CATEGORY_URL, () => {
        apiCalled = true
        return new HttpResponse(null, { status: 204 })
      }),
    )
    const onOpenChange = vi.fn()

    render(<CategoryDeleteDialog category={category} onOpenChange={onOpenChange} onDeleted={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /cancelar/i }))

    expect(apiCalled).toBe(false)
    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao pressionar Esc', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(<CategoryDeleteDialog category={category} onOpenChange={onOpenChange} onDeleted={vi.fn()} />)
    await user.keyboard('{Escape}')

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('fecha ao clicar no backdrop', async () => {
    const user = userEvent.setup()
    const onOpenChange = vi.fn()

    render(<CategoryDeleteDialog category={category} onOpenChange={onOpenChange} onDeleted={vi.fn()} />)
    await user.click(screen.getByRole('alertdialog').parentElement as HTMLElement)

    expect(onOpenChange).toHaveBeenCalledWith(false)
  })

  it('confirmar com sucesso chama a API e onDeleted', async () => {
    const user = userEvent.setup()
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 204 })))
    const onDeleted = vi.fn()

    render(<CategoryDeleteDialog category={category} onOpenChange={vi.fn()} onDeleted={onDeleted} />)
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('cat-1'))
  })

  it('confirmar com 404 chama onDeleted (categoria já não existia)', async () => {
    const user = userEvent.setup()
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 404 })))
    const onDeleted = vi.fn()

    render(<CategoryDeleteDialog category={category} onOpenChange={vi.fn()} onDeleted={onDeleted} />)
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    await waitFor(() => expect(onDeleted).toHaveBeenCalledWith('cat-1'))
  })

  it('confirmar com erro inesperado mantém o dialog aberto com alerta, sem chamar onDeleted', async () => {
    const user = userEvent.setup()
    server.use(http.delete(CATEGORY_URL, () => new HttpResponse(null, { status: 500 })))
    const onDeleted = vi.fn()

    render(<CategoryDeleteDialog category={category} onOpenChange={vi.fn()} onDeleted={onDeleted} />)
    await user.click(screen.getByRole('button', { name: /^excluir$/i }))

    expect(await screen.findByText('Não foi possível excluir')).toBeInTheDocument()
    expect(screen.getByText('Excluir categoria')).toBeInTheDocument()
    expect(onDeleted).not.toHaveBeenCalled()
  })
})
