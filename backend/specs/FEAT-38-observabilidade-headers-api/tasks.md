# Tasks: FEAT-38 — Observabilidade: trace-id, session-id e client-platform nos headers de API

- [x] 1. Criar `ObservabilityHeaderNames` (`backend/src/GastosApp.Api/Common/ObservabilityHeaderNames.cs`) — constantes `TraceId = "trace-id"`, `SessionId = "session-id"`, `ClientPlatform = "client-platform"`, `ClientVersion = "client-version"`

- [x] 2. Criar `LoggingOptions` (`backend/src/GastosApp.Infrastructure/Configuration/LoggingOptions.cs`) — `SectionName = "Logging"`, propriedade `FullPayloadLoggingEnabled` (bool)

- [x] 3. Registrar a leitura manual de `LoggingOptions` em `AddAwsInfrastructure` (`backend/src/GastosApp.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`) — sem `Configure<T>()`/reflection (AOT-safe, mesmo padrão de `DynamoDbOptions`), `FullPayloadLoggingEnabled = section["FullPayloadLoggingEnabled"] == "true"`

- [x] 4. Criar `SensitiveFieldRedactor` (`backend/src/GastosApp.Api/Common/SensitiveFieldRedactor.cs`) — `HashSet<string>` (case-insensitive) com `password, newPassword, oldPassword, code, token, accessToken, refreshToken, idToken, cardNumber, cvv`; método `Redact(string json)` faz o roundtrip via `JsonDocument`/`Utf8JsonWriter` mascarando (`"***"`) o valor de qualquer propriedade (em qualquer nível de aninhamento) cujo nome bata com a lista; se `json` não for um objeto JSON válido, devolve como veio

- [x] 5. Criar `RequestLogEntryBuilder` (`backend/src/GastosApp.Api/Common/RequestLogEntryBuilder.cs`) — constantes `MaxLoggedBodyLength = 4096` e `MaxHeaderValueLength = 200`; método `Build(...)` (assinatura do `plan.md`) retornando `IReadOnlyDictionary<string, object?>` com `Method`, `Path`, `StatusCode`, `DurationMs`, `TraceId`, `SessionId`, `ClientPlatform`, `ClientVersion`, `UserId`, e — só quando `statusCode >= 400` ou `fullPayloadLoggingEnabled` — `RequestBody`/`ResponseBody`; método privado `RedactIfJson(contentType, body)` só processa `content-type` iniciando com `application/json` (chama `SensitiveFieldRedactor.Redact` + trunca em `MaxLoggedBodyLength`), qualquer outro `content-type` retorna `null`

- [x] 6. Criar `RequestObservabilityMiddleware` (`backend/src/GastosApp.Api/Middlewares/RequestObservabilityMiddleware.cs`) — `InvokeAsync` lê os 4 headers de request (truncando em `MaxHeaderValueLength`), gera `trace-id` com `Guid.NewGuid()` quando ausente, seta `context.Response.Headers[ObservabilityHeaderNames.TraceId]` **antes** de chamar `_next`, bufferiza `Request.Body` (`EnableBuffering()`, só lê se `Content-Type` for `application/json`) e `Response.Body` (`MemoryStream`, copiado de volta pro stream original em `finally`), empurra as 4 propriedades (`TraceId`, `SessionId`, `ClientPlatform`, `ClientVersion`) no `LogContext` do Serilog (`using LogContext.PushProperty(...)`), mede duração com `Stopwatch`, e ao final chama `RequestLogEntryBuilder.Build(...)` + `logger.LogInformation("Requisição concluída: {@RequestLog}", entry)` dentro do `finally`

- [x] 7. Registrar `app.UseMiddleware<RequestObservabilityMiddleware>()` em `Program.cs` (`backend/src/GastosApp.Api/Program.cs`) como a primeira linha depois de `var app = builder.Build();`, **antes** de `app.UseExceptionHandler();`

- [x] 8. Atualizar a configuração do Serilog em `Program.cs` — adicionar `.Enrich.FromLogContext()` e trocar `WriteTo.Console()` por `WriteTo.Console(new Serilog.Formatting.Json.JsonFormatter())`

- [x] 9. Rodar `dotnet build backend/GastosApp.sln` e confirmar que compila sem erro (Native AOT trim warnings incluídos)

- [x] 10. Adicionar `aws_ssm_parameter.logging_full_payload_enabled` (`/GastosApp/Logging/FullPayloadLoggingEnabled`, tipo `String`, valor `"false"`) em `backend/infra/terraform/environments/prod/parameter-store.tf`

- [x] 11. Adicionar o mesmo recurso (`/GastosApp/Hom/Logging/FullPayloadLoggingEnabled`) em `backend/infra/terraform/environments/hom/parameter-store.tf`

- [x] 12. ~~Ajustar `retention_in_days` de `14` para `15`~~ Mantido em `14` — 15 não é valor válido na API da AWS, decisão do usuário em `aws_cloudwatch_log_group.lambda` (`backend/infra/terraform/environments/prod/lambda.tf`), `aws_cloudwatch_log_group.account_trigger_lambda` (`backend/infra/terraform/environments/prod/lambda-account-trigger.tf`) e `aws_cloudwatch_log_group.custom_message_trigger_lambda` (`backend/infra/terraform/environments/prod/lambda-custom-message-trigger.tf`)

- [x] 13. Ajustar `retention_in_days` de `14` para `7` nos 3 log groups equivalentes de homologação (`backend/infra/terraform/environments/hom/lambda.tf`, `lambda-account-trigger.tf`, `lambda-custom-message-trigger.tf`)

- [x] 14. Atualizar `cors_configuration` de `aws_apigatewayv2_api.main` em `backend/infra/terraform/environments/prod/api-gateway.tf` — `allow_headers` ganha `"trace-id", "session-id", "client-platform", "client-version"` (mantendo `"Authorization", "Content-Type"`), novo `expose_headers = ["trace-id"]`

- [x] 15. Aplicar a mesma mudança de `cors_configuration` em `backend/infra/terraform/environments/hom/api-gateway.tf`

- [x] 16. Rodar `terraform fmt`/`validate` e (com credenciais AWS válidas) `terraform plan` real e `terraform validate` (ou `terraform plan`, sem aplicar) nos dois ambientes (`environments/prod`, `environments/hom`) e confirmar que os únicos recursos alterados são os das tasks 10-15 — aplicação em si (`terraform apply`) segue o fluxo normal de deploy, fora do escopo desta task, e exige aprovação explícita do usuário antes de rodar

- [x] 17. Criar `backend/tests/GastosApp.UnitTests/Api/SensitiveFieldRedactorTests.cs` com `Redact_ShouldMaskPassword_WhenPresentAtTopLevel`

- [x] 18. Adicionar `Redact_ShouldMaskMultipleSensitiveFields_WhenPresentTogether` (Theory: `password`, `newPassword`, `code`, `token`, `refreshToken`) no mesmo arquivo

- [x] 19. Adicionar `Redact_ShouldMaskNestedSensitiveField_WhenPresentInsideObject` no mesmo arquivo

- [x] 20. Adicionar `Redact_ShouldNotChangeNonSensitiveFields` no mesmo arquivo

- [x] 21. Adicionar `Redact_ShouldReturnOriginal_WhenBodyIsNotValidJson` e `Redact_ShouldReturnOriginal_WhenBodyIsNullOrEmpty` no mesmo arquivo

- [x] 22. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~SensitiveFieldRedactorTests` e confirmar tudo passando

- [x] 23. Criar `backend/tests/GastosApp.UnitTests/Api/RequestLogEntryBuilderTests.cs` com `Build_ShouldIncludeBody_WhenStatusCodeIsError` (Theory: 400, 404, 500) — mesmo com toggle desligado

- [x] 24. Adicionar `Build_ShouldNotIncludeBody_WhenSuccessAndToggleDisabled` e `Build_ShouldIncludeBody_WhenSuccessAndToggleEnabled` no mesmo arquivo

- [x] 25. Adicionar `Build_ShouldNotIncludeBody_WhenContentTypeIsNotJson` (Theory: `text/csv`, `null`, mesmo em erro/toggle ligado) no mesmo arquivo

- [x] 26. Adicionar `Build_ShouldTruncateBody_WhenLongerThanMaxLength` no mesmo arquivo

- [x] 27. Adicionar `Build_ShouldIncludeAllFourObservabilityFields_WhenPresent` e `Build_ShouldAllowNullFields_WhenSessionIdClientPlatformClientVersionAbsent` no mesmo arquivo

- [x] 28. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~RequestLogEntryBuilderTests` e confirmar tudo passando

- [x] 29. Criar `backend/tests/GastosApp.ComponentTests/Observability/RequestObservabilityMiddlewareTests.cs` (mesmo padrão de `Cors/CorsTests.cs`, `IClassFixture<ComponentTestWebApplicationFactory>`) com `Requisicao_ComTraceIdEnviado_EcoaMesmoValorNaResposta`

- [x] 30. Adicionar `Requisicao_SemTraceIdEnviado_RecebeTraceIdGeradoNaResposta` no mesmo arquivo

- [x] 31. Adicionar `Requisicao_ComErro_AindaAssimRecebeHeaderTraceId` (ex.: rota protegida sem JWT → 401) no mesmo arquivo

- [x] 32. Adicionar `Requisicao_ComSessionIdClientPlatformClientVersionAusentes_ContinuaFuncionandoNormalmente` no mesmo arquivo

- [x] 33. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~RequestObservabilityMiddlewareTests` e confirmar tudo passando

- [x] 34. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unit + componente) sem regressão

- [ ] 35. Criar `backend/tests/GastosApp.IntegrationTests/Observability/ObservabilityFlowTests.cs` com `Health_QualquerRequisicao_RecebeHeaderTraceIdNaResposta` — chama `GET /health` sem enviar `trace-id`, confirma que a resposta traz o header gerado pela API

- [ ] 36. Rodar `backend/infra/lambda/run-local.sh` (binário Native AOT via Runtime Interface Emulator) e os testes integrados relevantes (`ObservabilityFlowTests`, `--filter Category=Integration`) localmente, confirmando que passam — validação obrigatória do risco Native AOT (`JsonDocument`, buffer de stream) antes de dar a feature por concluída

- [ ] 37. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que `backend/docs/openapi.json` **não muda** (headers de middleware cross-cutting não são representados pelo gerador de OpenAPI do projeto — ver `plan.md`, "Mapeamento de erro")

- [ ] 38. Atualizar `backend/infra/CLAUDE.md` — nova seção curta sobre `Logging/FullPayloadLoggingEnabled` no Parameter Store (mesmo padrão das seções já existentes de Cognito/CORS/SES) e sobre a retenção de log group ter deixado de ser uniforme entre hom/prod (7 x 15 dias, FEAT-38)

- [ ] 39. Registrar em `backend/docs/backlog.md`, seção "Débitos técnicos e melhorias futuras", os 3 itens já previstos no `spec.md`: (a) tornar os 4 headers obrigatórios no futuro, após ajustes no frontend; (b) segmentar o log de payload completo por `session-id` específico, não só globalmente; (c) propagar `trace-id` pra Lambda de triggers do Cognito via `ClientMetadata`

- [ ] 40. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-38-observabilidade-headers-api/spec.md` e preencher uma seção "Status", resumindo o que foi implementado — incluir nota explícita de que o conteúdo do log em CloudWatch (redação, payload condicional) foi validado manualmente em hom, não por teste automatizado de ponta a ponta (ponto confirmado no `plan.md`)

- [ ] 41. Atualizar `backend/docs/backlog.md` — marcar o item da FEAT-38 como concluído, seguindo a convenção já usada para features anteriores (ver commit da FEAT-37)
