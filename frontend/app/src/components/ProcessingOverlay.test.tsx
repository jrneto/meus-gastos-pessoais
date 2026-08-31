import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ProcessingOverlay } from './ProcessingOverlay'

describe('ProcessingOverlay', () => {
  it('renderiza o label recebido', () => {
    render(<ProcessingOverlay label="Enviando convite" />)

    expect(screen.getByText('Enviando convite')).toBeInTheDocument()
  })

  it('renderiza o spinner e a barra indeterminada', () => {
    const { container } = render(<ProcessingOverlay label="Salvando" />)

    expect(container.querySelector('.je-spin')).toBeInTheDocument()
    expect(container.querySelector('.je-indet')).toBeInTheDocument()
  })
})
