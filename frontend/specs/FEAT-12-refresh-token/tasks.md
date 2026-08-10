# Tasks — FEAT-12: Consumo do refresh token no frontend

Referência: [`plan.md`](./plan.md) e [`spec.md`](./spec.md).

- [x] 1. `lib/httpClient.ts`: adicionar `credentials: 'include'` em toda
      requisição (`request()`), sem nenhuma outra mudança de
      comportamento ainda
- [x] 2. `lib/httpClient.ts`: adicionar tipo `AuthPlugin` e
      `registerAuthPlugin(plugin)` (guarda em variável de módulo, sem
      uso ainda em `request()`)
- [x] 3. `lib/httpClient.ts`: em `request()`, para paths fora de
      `/auth/login` e `/auth/refresh`, injetar
      `Authorization: Bearer <token>` a partir de
      `authPlugin?.getAccessToken()` (sobrescrevendo header explícito
      do chamador, se houver)
- [x] 4. `lib/httpClient.ts`: implementar a lógica de 401 → refresh →
      retry (com deduplicação via promise de módulo) — token novo
      repete a chamada original uma vez; `null` chama
      `onSessionExpired()` e devolve a resposta 401 original sem
      retry; exceção de rede na tentativa de refresh é repropagada sem
      chamar `onSessionExpired()`
- [x] 5. `lib/httpClient.test.ts`: teste cobrindo injeção automática de
      `Authorization`
- [x] 6. `lib/httpClient.test.ts`: teste cobrindo retry transparente em
      401 com refresh bem-sucedido (chamada original repetida com o
      token novo)
- [x] 7. `lib/httpClient.test.ts`: teste cobrindo `onSessionExpired()`
      chamado e resposta 401 repassada quando o refresh também retorna
      401
- [x] 8. `lib/httpClient.test.ts`: teste cobrindo que falha de rede no
      refresh propaga a exceção sem chamar `onSessionExpired()`
- [x] 9. `lib/httpClient.test.ts`: teste cobrindo deduplicação — várias
      chamadas 401 concorrentes disparam só uma chamada de refresh
- [x] 10. `features/auth/errors/authErrors.ts`: adicionar
      `RefreshFailedError`
- [x] 11. `features/auth/api/authApi.ts`: implementar `refresh()`
      (`POST /auth/refresh`, sem body, 401 → `RefreshFailedError`)
- [x] 12. `features/auth/api/authApi.ts`: implementar `logout()`
      (`POST /auth/logout`, sem body, sem tratamento especial de erro
      nesta camada)
- [x] 13. Adicionar handlers MSW para `/auth/refresh` e `/auth/logout`
      nos testes de `authApi` (arquivo de teste novo ou existente)
      cobrindo sucesso, 401 e falha de rede de `refresh()`, e sucesso
      de `logout()`
- [x] 14. `features/auth/hooks/useSessionBootstrap.ts`: implementar
      hook (chama `authApi.refresh()` no mount, popula `authStore` em
      sucesso, ignora falha, expõe `isBootstrapping`)
- [x] 15. `useSessionBootstrap.test.ts`: cobrir sucesso (popula store),
      401 (store permanece vazia) e falha de rede (store permanece
      vazia) — todos terminando com `isBootstrapping === false`
- [x] 16. `features/auth/hooks/useLogout.ts`: implementar hook (chama
      `authApi.logout()` ignorando erro, depois `clearSession()`)
- [x] 17. `useLogout.test.ts`: cobrir chamada a `/auth/logout` +
      `clearSession()`, e que falha em `/auth/logout` não impede o
      `clearSession()`
- [x] 18. `app/authBootstrap.ts`: implementar `registerAuthPlugin(...)`
      ligando `httpClient` a `authStore`/`authApi.refresh()`
- [x] 19. `app/App.tsx`: importar `authBootstrap`, usar
      `useSessionBootstrap()` e renderizar `null` enquanto
      `isBootstrapping` for `true`, `RouterProvider` depois
- [x] 20. `routes/SettingsPage.tsx`: trocar uso direto de
      `clearSession` por `useLogout()`, ajustando `handleLogout` para
      `async`
- [x] 21. `routes/SettingsPage.test.tsx`: atualizar/adicionar teste
      cobrindo que o logout chama `POST /auth/logout` antes de navegar
      para `/login`
- [x] 22. Rodar `npm test` completo no `frontend/app`, corrigir
      qualquer regressão nos testes existentes de `features/expenses`
      e demais suítes afetadas pela injeção automática de
      `Authorization`
- [x] 23. Atualizar `spec.md` desta feature marcando os critérios de
      aceite concluídos (`- [x]`)
