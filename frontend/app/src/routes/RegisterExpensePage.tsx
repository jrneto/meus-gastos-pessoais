import { ExpenseForm } from '@/features/expenses/components/ExpenseForm'

export function RegisterExpensePage() {
  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Nova despesa</h1>
      <ExpenseForm />
    </div>
  )
}
