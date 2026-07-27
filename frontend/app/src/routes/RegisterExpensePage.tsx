import { Link, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { ExpenseForm } from '@/features/expenses/components/ExpenseForm'
import { useAuthStore } from '@/features/auth/store/authStore'

export function RegisterExpensePage() {
  const clearSession = useAuthStore((state) => state.clearSession)
  const navigate = useNavigate()

  function handleLogout() {
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <main className="flex min-h-svh flex-col items-center gap-6 p-4">
      <header className="flex w-full max-w-sm items-center justify-between pt-4">
        <h1 className="text-2xl font-semibold">Nova despesa</h1>
        <div className="flex gap-2">
          <Button variant="ghost" render={<Link to="/expenses">Ver despesas</Link>} />
          <Button variant="outline" onClick={handleLogout}>
            Sair
          </Button>
        </div>
      </header>
      <ExpenseForm />
    </main>
  )
}