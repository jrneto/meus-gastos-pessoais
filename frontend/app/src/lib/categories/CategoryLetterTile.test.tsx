import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CategoryLetterTile } from './CategoryLetterTile'

describe('CategoryLetterTile', () => {
  it('renderiza a inicial do nome em maiúscula', () => {
    render(<CategoryLetterTile name="alimentação" />)

    expect(screen.getByText('A')).toBeInTheDocument()
  })

  // `toHaveStyle` (jest-dom) passa por `getComputedStyle`, que o jsdom
  // não resolve pra `var(...)` — lê o `style` inline diretamente, que
  // preserva a expressão literal.
  it('sem tipo, usa a borda neutra (comportamento original, ex.: ExpenseDetailDialog)', () => {
    render(<CategoryLetterTile name="alimentação" />)

    const tile = screen.getByText('A')
    expect(tile.style.border).toBe('1px solid var(--color-divider)')
  })

  it('com tipo despesa, usa a cor accent', () => {
    render(<CategoryLetterTile name="alimentação" tipo="despesa" />)

    const tile = screen.getByText('A')
    expect(tile.style.border).toBe('1px solid var(--color-accent)')
  })

  it('com tipo receita, usa a cor positive', () => {
    render(<CategoryLetterTile name="salário" tipo="receita" />)

    const tile = screen.getByText('S')
    expect(tile.style.border).toBe('1px solid var(--color-positive)')
  })
})
