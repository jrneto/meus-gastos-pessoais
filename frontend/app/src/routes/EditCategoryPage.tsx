import { useParams } from 'react-router-dom'
import { CategoryNotFound } from '@/features/categories/components/CategoryNotFound'
import { EditCategoryForm } from '@/features/categories/components/EditCategoryForm'
import { useCategories } from '@/lib/categories/useCategories'

export function EditCategoryPage() {
  const { id } = useParams<{ id: string }>()
  const { items, isLoading } = useCategories()
  const category = items.find((item) => item.id === id)

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Editar categoria</h1>
      {isLoading && <p className="text-sm text-muted-foreground">Carregando...</p>}
      {!isLoading && !category && <CategoryNotFound />}
      {!isLoading && category && <EditCategoryForm category={category} />}
    </div>
  )
}
