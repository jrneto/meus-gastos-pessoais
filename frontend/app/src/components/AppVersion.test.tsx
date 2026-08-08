import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AppVersion } from './AppVersion'

describe('AppVersion', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('exibe a versão como link para a release quando é uma tag semântica', () => {
    vi.stubEnv('VITE_APP_VERSION', 'v1.4.0')
    vi.stubEnv('VITE_APP_COMMIT_SHA', 'abc1234')

    render(<AppVersion />)

    const link = screen.getByRole('link', { name: 'v1.4.0' })
    expect(link).toHaveAttribute(
      'href',
      'https://github.com/jrneto/meus-gastos-pessoais/releases/tag/v1.4.0',
    )
  })

  it('exibe a versão sem link quando não há env vars de CI (execução local)', () => {
    render(<AppVersion />)

    expect(screen.getByText(/dev-local/)).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })
})
