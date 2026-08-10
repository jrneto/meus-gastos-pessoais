# FEAT-15: Refresh Token — Tasks

Checklist sequencial de implementação, baseado em `plan.md`. Cada item é do
tamanho de um commit.

## Infrastructure
- [x] 1. Adicionar `RefreshToken` ao `record LoginResult` em
      `GastosApp.Application/Common/Interfaces/IAuthService.cs` e o novo
      método `Task<Result<RefreshResult>> RefreshAsync(...)` à interface
      `IAuthService`, com `record RefreshResult(string AccessToken, int ExpiresIn, string UserId)`
- [x] 2. Atualizar `CognitoAuthService.LoginAsync` para propagar
      `result.RefreshToken` (do `AuthenticationResult` do Cognito) no
      `LoginResult`
- [x] 3. Implementar `CognitoAuthService.RefreshAsync` (fluxo
      `AuthFlowType.REFRESH_TOKEN_AUTH` + `GetUserAsync`, capturando
      `NotAuthorizedException` → `AuthErrors.InvalidRefreshToken`)

## Application
- [x] 4. Adicionar `AuthErrors.RefreshTokenMissing` e
      `AuthErrors.InvalidRefreshToken` em `AuthErrors.cs`
- [x] 5. Adicionar `[JsonIgnore] RefreshToken` e o factory
      `FromLoginResult` ao `LoginUserResult`; atualizar
      `LoginUserCommandHandler` para usar o factory
- [x] 6. Criar `Auth/Commands/Refresh/RefreshTokenCommand.cs`
      (`RefreshTokenCommand`, `RefreshTokenCommandHandler`,
      `RefreshTokenResult` com factory `FromRefreshResult`)
- [x] 7. Criar `Auth/Commands/Logout/LogoutCommand.cs`
      (`LogoutCommand`, `LogoutCommandHandler` retornando sempre sucesso)

## Api
- [x] 8. Criar `Api/Common/RefreshTokenCookie.cs` (helper com `Name`,
      `ForSet()`, `ForClear()`)
- [x] 9. Atualizar `POST /login` em `AuthEndpoints.cs` para injetar
      `HttpContext` e setar o cookie de refresh token no sucesso
- [x] 10. Adicionar `POST /refresh` em `AuthEndpoints.cs` (lê o cookie,
      envia `RefreshTokenCommand`, limpa o cookie em qualquer falha,
      não reescreve o cookie no sucesso)
- [x] 11. Adicionar `POST /logout` em `AuthEndpoints.cs` (envia
      `LogoutCommand`, sempre limpa o cookie, retorna 200 sem corpo)
- [x] 12. Registrar `RefreshTokenResult` em
      `AppJsonSerializerContext.cs` (`[JsonSerializable]`)

## Testes unitários
- [x] 13. `CognitoAuthServiceTests`: atualizar casos de `LoginAsync` para
      cobrir `RefreshToken` no `LoginResult`; adicionar casos de
      `RefreshAsync` (sucesso e `NotAuthorizedException`)
- [x] 14. Criar `RefreshTokenCommandHandlerTests` (token vazio/whitespace
      → falha sem chamar `IAuthService`; sucesso mapeando `RefreshResult`;
      falha propagada do `IAuthService`)
- [x] 15. Criar `LogoutCommandHandlerTests` (sempre retorna sucesso)

## Testes de componente
- [x] 16. `AuthEndpointsTests`: `Login_ComCredenciaisValidas` passa a
      também validar o `Set-Cookie` de `refreshToken` (flags
      `HttpOnly`/`Secure`/`SameSite=Strict`/`Path=/auth`) e que o corpo
      não contém a chave `refreshToken`
- [x] 17. `AuthEndpointsTests`: novo caso `Refresh_ComCookieValido_Retorna200`
- [x] 18. `AuthEndpointsTests`: novo caso `Refresh_SemCookie_Retorna401`
      (sem chamar `IAuthService.RefreshAsync`)
- [x] 19. `AuthEndpointsTests`: novo caso
      `Refresh_ComCookieInvalidoOuExpirado_Retorna401ELimpaCookie`
- [x] 20. `AuthEndpointsTests`: novo caso `Logout_ComOuSemCookie_Retorna200ELimpaCookie`

## Contrato e fechamento
- [x] 21. Rodar `backend/scripts/export-openapi.sh` e revisar/ajustar
      manualmente `backend/docs/openapi.json` para os 3 endpoints
      (`/auth/login`, `/auth/refresh`, `/auth/logout`), incluindo o
      header `Set-Cookie` (não gerado automaticamente pelo Minimal API)
- [x] 22. Rodar `dotnet test GastosApp.sln` e confirmar 100% dos testes
      passando
- [x] 23. Atualizar os critérios de aceite em `spec.md` marcando os itens
      concluídos (`- [x]`)
