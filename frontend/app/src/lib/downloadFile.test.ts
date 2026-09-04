import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { downloadBlob } from './downloadFile'

describe('downloadBlob', () => {
  const createObjectURL = vi.fn(() => 'blob:mock-url')
  const revokeObjectURL = vi.fn()

  beforeEach(() => {
    vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL })
    // jsdom não implementa navegação — sem isso, `link.click()` num `<a
    // href>` de verdade loga "Not implemented: navigation to another
    // Document" no console a cada teste.
    vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(() => {})
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    createObjectURL.mockClear()
    revokeObjectURL.mockClear()
  })

  it('cria a object URL a partir do blob informado', () => {
    const blob = new Blob(['conteudo'], { type: 'text/csv' })

    downloadBlob(blob, 'transacoes.csv')

    expect(createObjectURL).toHaveBeenCalledWith(blob)
  })

  it('cria um link temporário com href/download corretos, aciona o clique e remove o link', () => {
    const appendSpy = vi.spyOn(document.body, 'appendChild')

    downloadBlob(new Blob(['conteudo']), 'transacoes.csv')

    const createdLink = appendSpy.mock.calls[0][0] as HTMLAnchorElement
    expect(createdLink.tagName).toBe('A')
    expect(createdLink.href).toBe('blob:mock-url')
    expect(createdLink.download).toBe('transacoes.csv')
    expect(createdLink.click).toHaveBeenCalledTimes(1)
    expect(document.body.contains(createdLink)).toBe(false)
  })

  it('revoga a object URL depois de disparar o download', () => {
    downloadBlob(new Blob(['conteudo']), 'transacoes.csv')

    expect(revokeObjectURL).toHaveBeenCalledWith('blob:mock-url')
  })
})
