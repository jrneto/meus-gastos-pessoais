import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { AppVersion } from './AppVersion'

describe('AppVersion', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('exibe a versão como texto, sem link', () => {
    vi.stubEnv('VITE_APP_VERSION', 'v1.4.0')

    render(<AppVersion />)

    expect(screen.getByText(/v1\.4\.0/)).toBeInTheDocument()
    expect(screen.queryByRole('link')).not.toBeInTheDocument()
  })

  it('exibe o fallback dev-local quando não há env var de CI (execução local)', () => {
    render(<AppVersion />)

    expect(screen.getByText(/dev-local/)).toBeInTheDocument()
  })
})
