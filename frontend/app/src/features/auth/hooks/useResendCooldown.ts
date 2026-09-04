import { useEffect, useRef, useState } from 'react'

interface UseResendCooldownResult {
  secondsLeft: number
  isExpired: boolean
  restart: () => void
}

// Só UI — não sabe nada sobre confirmação de conta, apenas conta
// segundos. Começa a contar assim que montado (a tela de confirmação
// nasce com o cooldown já em andamento) e para sozinho ao chegar a 0.
export function useResendCooldown(initialSeconds = 60): UseResendCooldownResult {
  const [secondsLeft, setSecondsLeft] = useState(initialSeconds)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  function clear() {
    if (intervalRef.current !== null) {
      clearInterval(intervalRef.current)
      intervalRef.current = null
    }
  }

  function start() {
    clear()
    intervalRef.current = setInterval(() => {
      setSecondsLeft((current) => {
        if (current <= 1) {
          clear()
          return 0
        }
        return current - 1
      })
    }, 1000)
  }

  useEffect(() => {
    start()
    return clear
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  function restart() {
    setSecondsLeft(initialSeconds)
    start()
  }

  return { secondsLeft, isExpired: secondsLeft === 0, restart }
}
