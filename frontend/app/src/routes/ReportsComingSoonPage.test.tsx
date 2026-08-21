import { render, screen } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ReportsComingSoonPage } from './ReportsComingSoonPage'

describe('ReportsComingSoonPage', () => {
  it('exibe o texto de placeholder', () => {
    render(<ReportsComingSoonPage />)

    expect(screen.getByText('Relatórios')).toBeInTheDocument()
    expect(screen.getByText('Em breve.')).toBeInTheDocument()
  })
})
