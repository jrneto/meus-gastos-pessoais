import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CategoryBadge } from './CategoryBadge'

describe('CategoryBadge', () => {
  it('renderiza nome e ícone da categoria', () => {
    render(<CategoryBadge category={{ nome: 'Alimentação', cor: '#F97316', icone: 'utensils' }} />)

    expect(screen.getByText('Alimentação')).toBeInTheDocument()
  })

  it('categoria indefinida renderiza rótulo genérico', () => {
    render(<CategoryBadge category={undefined} />)

    expect(screen.getByText('Categoria não encontrada')).toBeInTheDocument()
  })

  it('ícone desconhecido não quebra, usa o ícone de fallback', () => {
    render(<CategoryBadge category={{ nome: 'Outros', cor: '#000000', icone: 'inexistente' }} />)

    expect(screen.getByText('Outros')).toBeInTheDocument()
  })
})
