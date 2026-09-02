import { render, screen } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Toast } from './Toast'

describe('Toast', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  it('renderiza a mensagem quando message não é null', () => {
    render(<Toast message="Convite enviado para pessoa@email.com." onDismiss={() => {}} />)

    expect(screen.getByText('Convite enviado para pessoa@email.com.')).toBeInTheDocument()
  })

  it('não renderiza nada quando message é null', () => {
    const { container } = render(<Toast message={null} onDismiss={() => {}} />)

    expect(container).toBeEmptyDOMElement()
  })

  it('chama onDismiss automaticamente após o timeout', () => {
    vi.useFakeTimers()
    const onDismiss = vi.fn()

    render(<Toast message="Convite enviado." onDismiss={onDismiss} />)
    expect(onDismiss).not.toHaveBeenCalled()

    vi.advanceTimersByTime(3200)

    expect(onDismiss).toHaveBeenCalledTimes(1)
  })

  it('reagenda o timeout quando message muda antes do anterior disparar', () => {
    vi.useFakeTimers()
    const onDismiss = vi.fn()

    const { rerender } = render(<Toast message="Primeira mensagem." onDismiss={onDismiss} />)
    vi.advanceTimersByTime(2000)
    rerender(<Toast message="Segunda mensagem." onDismiss={onDismiss} />)
    vi.advanceTimersByTime(2000)

    // 4000ms desde o início, mas só 2000ms desde a segunda mensagem —
    // o timeout da primeira foi cancelado, ainda não disparou nenhum
    expect(onDismiss).not.toHaveBeenCalled()

    vi.advanceTimersByTime(1200)

    expect(onDismiss).toHaveBeenCalledTimes(1)
  })
})
