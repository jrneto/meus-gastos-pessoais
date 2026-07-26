import type { HttpHandler } from 'msw'

// Handlers base, sem rotas ainda — cada feature adiciona os seus
// (ex.: features/auth adiciona os handlers de /auth/login, /auth/me
// nos próprios arquivos de teste, via server.use(...)).
export const handlers: HttpHandler[] = []
