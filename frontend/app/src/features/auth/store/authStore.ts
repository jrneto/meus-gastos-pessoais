import { create } from 'zustand'

interface AuthState {
  token: string | null
  userId: string | null
  expiresAt: number | null
  setSession: (token: string, userId: string, expiresIn: number) => void
  clearSession: () => void
}

export const useAuthStore = create<AuthState>((set) => ({
  token: null,
  userId: null,
  expiresAt: null,
  setSession: (token, userId, expiresIn) =>
    set({ token, userId, expiresAt: Date.now() + expiresIn * 1000 }),
  clearSession: () => set({ token: null, userId: null, expiresAt: null }),
}))
