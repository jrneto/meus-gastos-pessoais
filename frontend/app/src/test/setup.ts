import '@testing-library/jest-dom/vitest'
import { afterAll, afterEach, beforeAll, vi } from 'vitest'
import { server } from './msw/server'

// Base URL fixa e previsível pros testes — desacopla os testes do
// .env.development real, evitando que mudem de comportamento conforme
// a config local de cada dev/CI.
vi.stubEnv('VITE_API_BASE_URL', 'http://localhost:5049')

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }))
afterEach(() => server.resetHandlers())
afterAll(() => server.close())
