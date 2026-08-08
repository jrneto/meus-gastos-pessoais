import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'

export function ExpenseNotFound() {
  return (
    <div className="flex w-full max-w-sm flex-col items-center gap-4 py-8 text-center">
      <p className="text-sm text-muted-foreground">Despesa não encontrada.</p>
      <Link to="/expenses" className={buttonVariants({})}>
        Voltar à listagem
      </Link>
    </div>
  )
}
