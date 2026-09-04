import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { server } from '@/test/msw/server'
import { SettingsPage } from './SettingsPage'

const EXPORT_URL = 'http://localhost:5049/transactions/export'

const downloadBlobMock = vi.hoisted(() => vi.fn())
vi.mock('@/lib/downloadFile', () => ({ downloadBlob: downloadBlobMock }))

function renderSettingsPage() {
  return render(<SettingsPage />)
}

describe('SettingsPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
    downloadBlobMock.mockClear()
  })

  it('exibe o título "Ajustes" e a linha de exportação, sem botão "Sair" próprio', () => {
    renderSettingsPage()

    expect(screen.getByRole('heading', { name: 'Ajustes' })).toBeInTheDocument()
    expect(screen.getByText('Exportar dados')).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /sair/i })).not.toBeInTheDocument()
  })

  it('exibe a versão do build publicado (rastreabilidade FEAT-09)', () => {
    renderSettingsPage()

    expect(screen.getByText(/versão/i)).toBeInTheDocument()
  })

  it('clicar em "Exportar CSV" baixa o arquivo e mostra o toast de sucesso', async () => {
    server.use(http.get(EXPORT_URL, () => HttpResponse.text('data;descricao\r\n')))
    const user = userEvent.setup()
    renderSettingsPage()

    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))

    await waitFor(() => expect(downloadBlobMock).toHaveBeenCalledTimes(1))
    expect(await screen.findByText('Transações exportadas.')).toBeInTheDocument()
  })

  it('mostra estado de carregamento (botão desabilitado, rótulo "Exportando...") durante a exportação', async () => {
    server.use(
      http.get(EXPORT_URL, async () => {
        await new Promise((resolve) => setTimeout(resolve, 20))
        return HttpResponse.text('data;descricao\r\n')
      }),
    )
    const user = userEvent.setup()
    renderSettingsPage()

    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))

    expect(screen.getByRole('button', { name: 'Exportando...' })).toBeDisabled()
    await waitFor(() => expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeEnabled())
  })

  it('sessão expirada ao exportar limpa a sessão e mostra a mensagem inline (redirect fica a cargo do ProtectedRoute global)', async () => {
    server.use(http.get(EXPORT_URL, () => new HttpResponse(null, { status: 401 })))
    const user = userEvent.setup()
    renderSettingsPage()

    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Sua sessão expirou. Faça login novamente.')
    expect(useAuthStore.getState().token).toBeNull()
    expect(downloadBlobMock).not.toHaveBeenCalled()
  })

  it('erro de rede ao exportar mostra mensagem inline e permite tentar de novo', async () => {
    server.use(http.get(EXPORT_URL, () => HttpResponse.error()))
    const user = userEvent.setup()
    renderSettingsPage()

    await user.click(screen.getByRole('button', { name: 'Exportar CSV' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(/não foi possível conectar/i)
    expect(screen.getByRole('button', { name: 'Exportar CSV' })).toBeEnabled()
    expect(downloadBlobMock).not.toHaveBeenCalled()
  })
})
