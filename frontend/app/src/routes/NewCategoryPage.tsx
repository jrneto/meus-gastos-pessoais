import { NewCategoryForm } from '@/features/categories/components/NewCategoryForm'

export function NewCategoryPage() {
  return (
    <div className="flex flex-col items-center gap-6 p-4">
      <h1 className="w-full max-w-sm text-2xl font-semibold">Nova categoria</h1>
      <NewCategoryForm />
    </div>
  )
}
