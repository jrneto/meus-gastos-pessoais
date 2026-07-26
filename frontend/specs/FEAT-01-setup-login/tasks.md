# Tasks — FEAT-01: Setup inicial do frontend + tela de login

Referência: [`plan.md`](./plan.md) (arquitetura/decisões) e
[`spec.md`](./spec.md) (critérios de aceite). Ordem sequencial — cada
item é do tamanho de um commit. Projeto Vite vive em `frontend/app/`
(separado de `docs/`, `specs/`, `infra/`); caminhos abaixo relativos a
`frontend/app/` (ex.: `src/...` = `frontend/app/src/...`).

- [x] 1. Criar o projeto com Vite (`npm create vite@latest . -- --template react-ts`) em `frontend/app/`, confirmar que `npm run dev` sobe a página padrão
- [x] 2. Instalar dependências de roteamento e estado: `react-router-dom`, `zustand`
- [x] 3. Instalar dependências de formulário/validação: `react-hook-form`, `zod`, `@hookform/resolvers`
- [x] 4. Configurar Tailwind CSS (instalação + `tailwind.config`/`postcss.config` + import no CSS global)
- [x] 5. Inicializar shadcn/ui (`npx shadcn init`) e adicionar os componentes usados na tela de login (`button`, `input`, `label`, `alert`)
- [x] 6. Configurar Vitest + React Testing Library + MSW (`vitest.config`/config no `vite.config.ts`, `setupTests.ts`, `jsdom` como environment, handler base do MSW)
- [x] 7. Criar a estrutura de pastas do padrão feature-based (`src/app/`, `src/routes/`, `src/features/auth/{api,components,hooks,schemas,store,errors}`, `src/components/`, `src/lib/`), conforme `plan.md`
- [x] 8. Criar `.env.example`, `.env.development` (`VITE_API_BASE_URL=http://localhost:5049`) e `.env.production` (placeholder), e confirmar que `.gitignore` cobre `.env*` exceto `.env.example`
- [x] 9. Implementar `src/lib/httpClient.ts` (fetch wrapper usando `import.meta.env.VITE_API_BASE_URL`)
- [x] 10. Implementar `src/features/auth/schemas/loginSchema.ts` (Zod: email + senha ≥ 8) e teste unitário `loginSchema.test.ts`
- [x] 11. Implementar `src/features/auth/errors/authErrors.ts` (`InvalidCredentialsError`, `NetworkError`, `UnknownAuthError`)
- [x] 12. Implementar `src/features/auth/api/authApi.ts` (`login()`, `me()` via `httpClient`, mapeando status HTTP para os erros tipados)
- [x] 13. Implementar `src/features/auth/store/authStore.ts` (Zustand: `token`, `userId`, `expiresAt`, `setSession`, `clearSession`) e teste unitário `authStore.test.ts` (cálculo de `expiresAt`, `isAuthenticated` antes/depois de expirar)
- [x] 14. Implementar `src/features/auth/hooks/useLogin.ts` (chama `authApi.login`, gerencia `isLoading`/`error`, popula `authStore` em caso de sucesso) e teste `useLogin.test.ts` (sucesso, 401, erro de rede — via MSW)
- [x] 15. Implementar `src/features/auth/hooks/useAuthSession.ts` (deriva `isAuthenticated` do `authStore`)
- [x] 16. Implementar `src/features/auth/components/LoginForm.tsx` (RHF + `zodResolver(loginSchema)`, campos email/senha com shadcn, chama `useLogin`, exibe erro inline por campo e alerta de credenciais inválidas)
- [x] 17. Implementar `src/components/ProtectedRoute.tsx` (redireciona para `/login` sem sessão válida, renderiza `children`/`<Outlet />` com sessão válida)
- [x] 18. Implementar `src/routes/LoginPage.tsx` (usa `LoginForm`, redireciona para a rota protegida após login com sucesso)
- [x] 19. Implementar `src/routes/HomePage.tsx` (placeholder pós-login, com ação de logout que chama `authStore.clearSession` e redireciona para `/login`)
- [x] 20. Configurar rotas em `src/app/router.tsx` (`/login` pública, `/` protegida via `ProtectedRoute` renderizando `HomePage`)
- [x] 21. Configurar `src/app/main.tsx`/`src/app/App.tsx` (montagem do `RouterProvider`/providers)
- [x] 22. Escrever teste de componente `src/features/auth/components/LoginForm.test.tsx` (validação inline, submit com sucesso, exibição do erro 401 via MSW mockando `POST /auth/login`)
- [x] 23. Escrever teste de componente `src/components/ProtectedRoute.test.tsx` (redireciona sem sessão, renderiza filhos com sessão válida)
- [x] 24. Rodar a suíte completa (`npm test`) e garantir 100% dos testes passando (critério de conclusão, ver `frontend/docs/constitution.md`)
- [x] 25. Validação manual end-to-end: `npm run dev` apontando para a API local (`.env.development`), percorrer login com sucesso, login com credenciais inválidas, acesso à rota protegida sem sessão, e logout
- [x] 26. Atualizar `spec.md` marcando os critérios de aceite concluídos (`- [x]`)
