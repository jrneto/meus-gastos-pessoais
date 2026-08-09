import { afterEach, describe, expect, it, vi } from 'vitest'
import { getAppVersion } from './appVersion'

describe('getAppVersion', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('retorna a versão injetada pelo CI', () => {
    vi.stubEnv('VITE_APP_VERSION', 'v1.4.0')

    expect(getAppVersion()).toBe('v1.4.0')
  })

  it('cai no fallback dev-local quando a env var de CI não existe (execução local)', () => {
    // Nenhum stubEnv aqui de propósito — replica o cenário local
    // (`npm run dev`), onde VITE_APP_VERSION nunca é setada (só
    // existe em build de CI).
    expect(getAppVersion()).toBe('dev-local')
  })
})
