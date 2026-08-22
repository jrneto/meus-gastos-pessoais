import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { categoriesWriteApi } from '../api/categoriesWriteApi'
import { SessionExpiredError } from '../errors/categoryErrors'
import type { CategoryItem } from '@/lib/categories/types'
import type { CategoryFormOutput } from '../schemas/categorySchema'

interface UseUpdateCategoryResult {
  updateCategory: (data: CategoryFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
  data: CategoryItem | null
}

export function useUpdateCategory(id: string): UseUpdateCategoryResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const [data, setData] = useState<CategoryItem | null>(null)
  const token = useAuthStore((state) => state.token)

  async function updateCategory(formData: CategoryFormOutput): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      const updated = await categoriesWriteApi.updateCategory(token ?? '', id, formData)
      setData(updated)
      setSuccess(true)
    } catch (err) {
      if (err instanceof SessionExpiredError) {
        useAuthStore.getState().clearSession()
      }
      setError(err as Error)
    } finally {
      setIsLoading(false)
    }
  }

  return { updateCategory, isLoading, error, success, data }
}
