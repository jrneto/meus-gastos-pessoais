# Tasks: FEAT-39 — Headers de observabilidade obrigatórios (trace-id, client-platform, client-version)

- [x] 1. Criar `ObservabilityHeaderValidator` (`backend/src/GastosApp.Api/Common/ObservabilityHeaderValidator.cs`) — `PathString HealthPath = "/health"`; `IsExemptPath(PathString path)` via `StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase)`; `GetMissingHeaders(string? traceId, string? clientPlatform, string? clientVersion)` retornando `IReadOnlyList<string>` (nomes de `ObservabilityHeaderNames`, na ordem trace-id/client-platform/client-version, vazio quando nenhum ausente); `BuildMissingHeadersError(IReadOnlyList<string> missingHeaders)` retornando `Error.Validation("missing-observability-headers", $"Header(s) obrigatório(s) ausente(s): {string.Join(", ", missingHeaders)}")`

- [x] 2. Alterar `RequestObservabilityMiddleware.InvokeAsync` (`backend/src/GastosApp.Api/Middlewares/RequestObservabilityMiddleware.cs`) — logo após ler os 4 headers e antes do restante do método: calcular `traceId = rawTraceId ?? Guid.NewGuid().ToString()` e setar `context.Response.Headers[ObservabilityHeaderNames.TraceId]` já nesse ponto (mesma posição de hoje); em seguida, se `!ObservabilityHeaderValidator.IsExemptPath(context.Request.Path)`, calcular `missingHeaders` via `ObservabilityHeaderValidator.GetMissingHeaders(rawTraceId, clientPlatform, clientVersion)` e, se não vazio, chamar o novo método privado `RejectMissingHeadersAsync(...)` (task 3) e retornar sem chamar `_next(context)`

- [x] 3. Implementar o método privado `RejectMissingHeadersAsync` no mesmo arquivo — captura o corpo do request via `CaptureRequestBodyIfJsonAsync` (método privado já existente, reaproveitado como está); monta `Result.Failure(ObservabilityHeaderValidator.BuildMissingHeadersError(missingHeaders)).ToHttpResult(() => Results.Ok())` e chama `.ExecuteAsync(context)`; mede duração com `Stopwatch`; loga via `RequestLogEntryBuilder.Build(...)` (statusCode já refletindo o 400 escrito por `ExecuteAsync`, `userId: null`, `responseBody: null`) + `logger.LogInformation("Requisição concluída: {@RequestLog}", entry)`

- [x] 4. Rodar `dotnet build backend/GastosApp.sln` e confirmar que compila sem erro (Native AOT trim warnings incluídos)

- [x] 5. Criar `backend/tests/GastosApp.UnitTests/Api/ObservabilityHeaderValidatorTests.cs` com `IsExemptPath_ShouldReturnTrue_ForHealthPath` (Theory: `/health`, variações de case) e `IsExemptPath_ShouldReturnFalse_ForAnyOtherPath` (Theory: `/auth/login`, `/transactions`, `/`)

- [x] 6. Adicionar `GetMissingHeaders_ShouldReturnEmpty_WhenAllThreePresent` e `GetMissingHeaders_ShouldReturnOnlyThatHeader_WhenOnlyOneMissing` (Theory: cada um dos 3 headers isolado, valor `null` e vazio) no mesmo arquivo

- [x] 7. Adicionar `GetMissingHeaders_ShouldReturnAllThree_WhenNonePresent` e `BuildMissingHeadersError_ShouldListAllMissingHeaders_InMessage` (Theory: 1, 2 e 3 headers ausentes) no mesmo arquivo

- [x] 8. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~ObservabilityHeaderValidatorTests` e confirmar tudo passando

- [x] 9. Em `backend/tests/GastosApp.ComponentTests/Observability/RequestObservabilityMiddlewareTests.cs`, dividir o teste existente `Requisicao_ComSessionIdClientPlatformClientVersionAusentes_ContinuaFuncionandoNormalmente`: renomear/restringir para cobrir só a ausência de `session-id` (`Requisicao_SemSessionId_ContinuaFuncionandoNormalmente`), já que `client-platform`/`client-version` deixam de ser opcionais

- [x] 10. Adicionar `Requisicao_SemTraceId_Retorna400ComDetailIdentificandoTraceId`, `Requisicao_SemClientPlatform_Retorna400` e `Requisicao_SemClientVersion_Retorna400` no mesmo arquivo — cada um chamando uma rota não isenta com os outros dois headers presentes, conferindo status 400 e `detail` do `ProblemDetails` citando o header ausente

- [x] 11. Adicionar `Requisicao_SemNenhumDosTresObrigatorios_Retorna400ComOsTresNoDetail` e `Requisicao_Rejeitada_AindaAssimRecebeHeaderTraceIdNaResposta` (confirma `trace-id` gerado pela API mesmo quando ele próprio está entre os ausentes) no mesmo arquivo

- [x] 12. Adicionar `Health_SemNenhumHeaderDeObservabilidade_Continua200` no mesmo arquivo — chama `GET /health` sem nenhum dos 4 headers, confirma 200 (exceção de propósito, distinta do teste genérico de `trace-id` já existente)

- [x] 13. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~RequestObservabilityMiddlewareTests` e confirmar tudo passando

- [x] 14. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unit + componente) sem regressão

- [x] 15. Adicionar `RotaNaoIsenta_SemClientVersion_Retorna400` em `backend/tests/GastosApp.IntegrationTests/Observability/ObservabilityFlowTests.cs` — chama uma rota não isenta já existente sem `client-version`, confirma 400 real do binário publicado

- [x] 16. Rodar `backend/infra/lambda/run-local.sh` (binário Native AOT via Runtime Interface Emulator) e os testes integrados relevantes (`ObservabilityFlowTests`, `--filter Category=Integration`) localmente, confirmando que passam — validação obrigatória do risco Native AOT (`Result.Failure(...).ToHttpResult(...).ExecuteAsync(...)` fora do fluxo normal de endpoint) antes de dar a feature por concluída

- [x] 17. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` se `backend/docs/openapi.json` muda — se não mudar, confirma a ressalva do `plan.md` (headers de middleware cross-cutting não representados pelo gerador); se mudar, documentar o motivo no `spec.md`

- [x] 18. Atualizar `backend/docs/backlog.md`, item "DÉBITO — Headers de observabilidade continuam opcionais": marcar `[x]` e anotar que `trace-id`/`client-platform`/`client-version` foram resolvidos pela FEAT-39 (referenciar `backend/specs/FEAT-39-headers-observabilidade-obrigatorios/`); acrescentar um novo item `[ ]` de débito técnico específico sobre `session-id` continuar opcional indefinidamente, e por quê (estrutural: `POST /auth/login`/`POST /auth/refresh` sempre chamados sem sessão ainda; best-effort no frontend, pode faltar em modo de navegação privada)

- [x] 19. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-39-headers-observabilidade-obrigatorios/spec.md` e preencher uma seção "Status", resumindo o que foi implementado (inclusive o resultado da task 17 sobre o `openapi.json`)
