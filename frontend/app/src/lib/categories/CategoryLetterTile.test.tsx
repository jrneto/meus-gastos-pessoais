import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { CategoryLetterTile } from './CategoryLetterTile'

describe('CategoryLetterTile', () => {
  it('renderiza a inicial do nome em maiúscula', () => {
    render(<CategoryLetterTile name="alimentação" />)

    expect(screen.getByText('A')).toBeInTheDocument()
  })
})
