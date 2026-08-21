import { useState } from 'react'
import { useAuthStore } from '@/features/auth/store/authStore'
import { categoriesWriteApi } from '../api/categoriesWriteApi'
import { SessionExpiredError } from '../errors/categoryErrors'
import type { CategoryFormOutput } from '../schemas/categorySchema'

interface UseRegisterCategoryResult {
  registerCategory: (data: CategoryFormOutput) => Promise<void>
  isLoading: boolean
  error: Error | null
  success: boolean
}

export function useRegisterCategory(): UseRegisterCategoryResult {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<Error | null>(null)
  const [success, setSuccess] = useState(false)
  const token = useAuthStore((state) => state.token)

  async function registerCategory(data: CategoryFormOutput): Promise<void> {
    setIsLoading(true)
    setError(null)
    setSuccess(false)
    try {
      await categoriesWriteApi.createCategory(token ?? '', data)
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

  return { registerCategory, isLoading, error, success }
}
