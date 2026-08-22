import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { SignupComingSoonPage } from './SignupComingSoonPage'

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/cadastro-em-breve']}>
      <Routes>
        <Route path="/cadastro-em-breve" element={<SignupComingSoonPage />} />
        <Route path="/login" element={<div>Tela de login</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('SignupComingSoonPage', () => {
  it('exibe o texto de placeholder e volta para o login ao clicar no link', async () => {
    const user = userEvent.setup()
    renderPage()

    expect(screen.getByText('Cadastro em breve')).toBeInTheDocument()

    await user.click(screen.getByRole('link', { name: 'Voltar para o login' }))

    expect(await screen.findByText('Tela de login')).toBeInTheDocument()
  })
})
