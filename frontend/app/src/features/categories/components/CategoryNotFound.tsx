import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'

export function CategoryNotFound() {
  return (
    <div className="flex w-full max-w-sm flex-col items-center gap-4 py-8 text-center">
      <p className="text-sm text-muted-foreground">Categoria não encontrada.</p>
      <Link to="/categories" className={buttonVariants({})}>
        Voltar à listagem
      </Link>
    </div>
  )
}
