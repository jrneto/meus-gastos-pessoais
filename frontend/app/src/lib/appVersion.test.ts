import { afterEach, describe, expect, it, vi } from 'vitest'
import { getAppVersion } from './appVersion'

describe('getAppVersion', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
  })

  it('reconhece uma tag semântica como release e monta o link de release', () => {
    vi.stubEnv('VITE_APP_VERSION', 'v1.4.0')
    vi.stubEnv('VITE_APP_COMMIT_SHA', 'abc1234')

    const info = getAppVersion()

    expect(info.isRelease).toBe(true)
    expect(info.isLocal).toBe(false)
    expect(info.url).toBe('https://github.com/jrneto/meus-gastos-pessoais/releases/tag/v1.4.0')
  })

  it('trata uma versão de homologação (dev-<sha>) como não-release e monta o link de commit', () => {
    vi.stubEnv('VITE_APP_VERSION', 'dev-a1b2c3d')
    vi.stubEnv('VITE_APP_COMMIT_SHA', 'a1b2c3d1234567')

    const info = getAppVersion()

    expect(info.isRelease).toBe(false)
    expect(info.isLocal).toBe(false)
    expect(info.url).toBe('https://github.com/jrneto/meus-gastos-pessoais/commit/a1b2c3d1234567')
  })

  it('cai no fallback local quando as env vars de CI não existem, sem link', () => {
    // Nenhum stubEnv aqui de propósito — replica o cenário local
    // (`npm run dev`), onde VITE_APP_VERSION/VITE_APP_COMMIT_SHA nunca
    // são setadas (só existem em build de CI).
    const info = getAppVersion()

    expect(info.version).toBe('dev-local')
    expect(info.commitSha).toBe('local')
    expect(info.isRelease).toBe(false)
    expect(info.isLocal).toBe(true)
    expect(info.url).toBe('')
  })
})
