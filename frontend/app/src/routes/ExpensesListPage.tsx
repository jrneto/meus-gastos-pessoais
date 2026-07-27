import { Link, useNavigate } from 'react-router-dom'
import { Button } from '@/components/ui/button'
import { useAuthStore } from '@/features/auth/store/authStore'
import { ExpenseFilters } from '@/features/expenses/components/ExpenseFilters'
import { ExpenseList } from '@/features/expenses/components/ExpenseList'
import { useExpensesQuery } from '@/features/expenses/hooks/useExpensesQuery'

export function ExpensesListPage() {
  const clearSession = useAuthStore((state) => state.clearSession)
  const navigate = useNavigate()
  const query = useExpensesQuery()

  function handleLogout() {
    clearSession()
    navigate('/login', { replace: true })
  }

  return (
    <main className="flex min-h-svh flex-col items-center gap-6 p-4">
      <header className="flex w-full max-w-sm items-center justify-between pt-4">
        <h1 className="text-2xl font-semibold">Minhas despesas</h1>
        <div className="flex gap-2">
          <Button variant="ghost" render={<Link to="/">Nova despesa</Link>} />
          <Button variant="outline" onClick={handleLogout}>
            Sair
          </Button>
        </div>
      </header>
      <ExpenseFilters onApply={query.applyFilters} />
      <ExpenseList
        items={query.items}
        isLoading={query.isLoading}
        isLoadingMore={query.isLoadingMore}
        error={query.error}
        hasMore={query.hasMore}
        onLoadMore={query.loadMore}
      />
    </main>
  )
}
