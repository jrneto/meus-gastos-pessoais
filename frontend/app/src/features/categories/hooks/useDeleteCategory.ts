import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { categoriesWriteApi } from '../api/categoriesWriteApi'
import { SessionExpiredError } from '../errors/categoryErrors'

interface UseDeleteCategoryResult {
  deleteCategory: (id: string) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useDeleteCategory(): UseDeleteCategoryResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function deleteCategory(id: string): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await categoriesWriteApi.deleteCategory(token ?? '', id)
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

  return { deleteCategory, isLoading, error, success }
}
