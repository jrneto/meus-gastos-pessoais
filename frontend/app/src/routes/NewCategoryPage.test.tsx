import { render, screen } from '@testing-library/react'
import { beforeEach, describe, expect, it } from 'vitest'
import { useAuthStore } from '@/features/auth/store/authStore'
import { NewCategoryPage } from './NewCategoryPage'

describe('NewCategoryPage', () => {
  beforeEach(() => {
    useAuthStore.getState().clearSession()
    useAuthStore.getState().setSession('tok-123', 'user-1', 3600)
  })

  it('renderiza o título e o formulário de nova categoria', () => {
    render(<NewCategoryPage />)

    expect(screen.getByRole('heading', { name: 'Nova categoria' })).toBeInTheDocument()
    expect(screen.getByLabelText('Nome')).toBeInTheDocument()
  })
})
