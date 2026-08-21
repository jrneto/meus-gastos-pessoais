import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { buttonVariants } from '@/components/ui/button'
import { CategoryList } from '@/features/categories/components/CategoryList'
import { useCategories } from '@/lib/categories/useCategories'
import type { CategoryItem } from '@/lib/categories/types'

export function CategoriesPage() {
  const { items: fetchedItems, isLoading, error } = useCategories()
  const [items, setItems] = useState<CategoryItem[]>([])

  useEffect(() => {
    setItems(fetchedItems)
  }, [fetchedItems])

  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <div className="flex w-full max-w-sm items-center justify-between">
        <h1 className="text-2xl font-semibold">Categorias</h1>
        <Link to="/categories/new" className={buttonVariants({ size: 'sm' })}>
          Nova categoria
        </Link>
      </div>
      <CategoryList
        items={items}
        isLoading={isLoading}
        error={error}
        onDeleted={(id) => setItems((prev) => prev.filter((item) => item.id !== id))}
      />
    </div>
  )
}
