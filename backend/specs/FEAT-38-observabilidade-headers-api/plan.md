# Plan — FEAT-38: Observabilidade — trace-id, session-id e client-platform nos headers de API

## Camadas afetadas

- **Api**:
  - `Middlewares/RequestObservabilityMiddleware.cs` (novo) — lê os 4
    headers de request, resolve/gera `trace-id`, seta o header de
    resposta, empurra as 4 propriedades pro `LogContext` do Serilog, e
    loga uma linha "requisição concluída" ao final, decidindo se inclui
    payload completo.
  - `Common/ObservabilityHeaderNames.cs` (novo) — constantes com os 4
    nomes de header (evita strings mágicas espalhadas).
  - `Common/RequestLogEntryBuilder.cs` (novo) — lógica pura (sem
    Serilog/HttpContext direto) que decide o que vai pro log: inclui
    payload completo ou não, aplica redação de campos sensíveis, monta
    o dicionário de propriedades final. Extraída à parte pra ser
    testável por unit test, sem precisar interceptar o pipeline HTTP
    nem o Serilog de verdade (ver "Testes a criar").
  - `Common/SensitiveFieldRedactor.cs` (novo) — dado um corpo JSON cru
    (string), devolve outro JSON com os campos sensíveis mascarados.
    Usa `System.Text.Json.JsonDocument`/`Utf8JsonWriter` (API de DOM,
    sem reflection/source-generator — segura sob Native AOT, diferente
    de `JsonSerializer.Deserialize<T>` sem `JsonTypeInfo` explícito).
  - `Program.cs`:
    - Serilog ganha `.Enrich.FromLogContext()` (obrigatório pra
      `LogContext.PushProperty` realmente aparecer no log emitido) e
      troca o formatter do `WriteTo.Console(...)` pra
      `new Serilog.Formatting.Json.JsonFormatter()` (já vem no pacote
      `Serilog` base, sem dependência nova) — log passa a ser uma linha
      JSON por evento, parseável por CloudWatch Logs Insights sem
      configuração adicional.
    - `app.UseMiddleware<RequestObservabilityMiddleware>()` registrado
      como a **primeira** linha depois de `var app = builder.Build();`
      — antes até de `UseExceptionHandler()` (ver "Decisões técnicas
      relevantes", item 1).
  - Nenhuma mudança em endpoints existentes, nenhum DTO novo,
    `AppJsonSerializerContext` sem alteração.
- **Infrastructure**:
  - `Configuration/LoggingOptions.cs` (novo) — POCO com
    `FullPayloadLoggingEnabled` (bool), mesmo padrão de
    `SesOptions`/`DynamoDbOptions`.
  - `DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
    — `AddAwsInfrastructure` ganha a leitura manual de `LoggingOptions`
    (sem `Configure<T>()`/reflection, mesmo motivo AOT já documentado
    pros outros Options).
- **Application/Domain** — sem mudança. Nenhum Command/Query, nenhum
  `Error` novo, nenhuma entidade envolvida — feature 100% de
  Api+Infrastructure (cross-cutting).

## Contratos técnicos

### `Common/ObservabilityHeaderNames.cs` (Api, novo)

```csharp
public static class ObservabilityHeaderNames
{
    public const string TraceId = "trace-id";
    public const string SessionId = "session-id";
    public const string ClientPlatform = "client-platform";
    public const string ClientVersion = "client-version";
}
```

### `Configuration/LoggingOptions.cs` (Infrastructure, novo)

```csharp
namespace GastosApp.Infrastructure.Configuration
{
    public sealed class LoggingOptions
    {
        public const string SectionName = "Logging";

        // Toggle global (não por sessão específica — decisão do
        // /specify). "true"/"false" como string no Parameter Store,
        // convertido aqui; ausente ou qualquer valor != "true" = false.
        public bool FullPayloadLoggingEnabled { get; init; }
    }
}
```

Leitura manual em `InfrastructureServiceCollectionExtensions.AddAwsInfrastructure`:

```csharp
services.AddSingleton(_ =>
{
    var section = configuration.GetSection(LoggingOptions.SectionName);
    var options = new LoggingOptions
    {
        FullPayloadLoggingEnabled = section["FullPayloadLoggingEnabled"] == "true"
    };
    return Options.Create(options);
});
```

### `Common/SensitiveFieldRedactor.cs` (Api, novo)

```csharp
public static class SensitiveFieldRedactor
{
    // Nomes de propriedade JSON (case-insensitive) nunca logados em
    // texto puro — lista fechada, fácil de estender depois.
    private static readonly HashSet<string> SensitiveFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "newPassword", "oldPassword", "code",
        "token", "accessToken", "refreshToken", "idToken",
        "cardNumber", "cvv"
    };

    // Faz o roundtrip via JsonDocument (DOM, sem reflection) mascarando
    // o valor de qualquer propriedade (em qualquer nível de
    // aninhamento) cujo nome bata com SensitiveFields. Se o corpo não
    // for um JSON de objeto válido, devolve como veio (ex.: corpo
    // vazio, ou algo que não é JSON — nesse caso quem chama já decidiu
    // não tentar redigir, ver RequestLogEntryBuilder).
    public static string Redact(string json) { /* ... */ }
}
```

### `Common/RequestLogEntryBuilder.cs` (Api, novo)

Lógica pura, sem `HttpContext`/Serilog direto — recebe valores já
extraídos, devolve um `IReadOnlyDictionary<string, object?>` com as
propriedades a logar. Isso é o que fica coberto por unit test (ver
"Testes a criar").

```csharp
public static class RequestLogEntryBuilder
{
    public static IReadOnlyDictionary<string, object?> Build(
        string method,
        string path,
        int statusCode,
        long durationMs,
        string? traceId,
        string? sessionId,
        string? clientPlatform,
        string? clientVersion,
        string? userId,
        bool fullPayloadLoggingEnabled,
        string? requestContentType,
        string? requestBody,
        string? responseContentType,
        string? responseBody)
    {
        var isError = statusCode >= 400;
        var shouldLogBody = isError || fullPayloadLoggingEnabled;

        var entry = new Dictionary<string, object?>
        {
            ["Method"] = method,
            ["Path"] = path,
            ["StatusCode"] = statusCode,
            ["DurationMs"] = durationMs,
            ["TraceId"] = traceId,
            ["SessionId"] = sessionId,
            ["ClientPlatform"] = clientPlatform,
            ["ClientVersion"] = clientVersion,
            ["UserId"] = userId
        };

        if (shouldLogBody)
        {
            entry["RequestBody"] = RedactIfJson(requestContentType, requestBody);
            entry["ResponseBody"] = RedactIfJson(responseContentType, responseBody);
        }

        return entry;
    }

    // Só corpo application/json (ou vazio) é candidato a log — exclui
    // CSV (GET /transactions/export), binários e qualquer outro
    // content-type: evita truncar/poluir o log com payload não-JSON, e
    // não precisa de regra de redação pra formato que a redação (JSON)
    // não sabe interpretar.
    private static string? RedactIfJson(string? contentType, string? body) { /* ... */ }
}
```

Constante de truncamento (mesmo arquivo ou `LoggingOptions`):
`MaxLoggedBodyLength = 4096` caracteres — corpo maior é truncado com
sufixo `"...(truncado)"` antes de entrar no log, pra não estourar
custo/tamanho de evento do CloudWatch Logs com uma resposta grande.
Mesma constante limita cada header de observabilidade lido do client
(`MaxHeaderValueLength = 200`) — protege contra um client mal-
comportado inflando o log indefinidamente; **não** rejeita a
requisição, só trunca o valor antes de logar/devolver.

### `Middlewares/RequestObservabilityMiddleware.cs` (Api, novo)

```csharp
public sealed class RequestObservabilityMiddleware
{
    private readonly RequestDelegate _next;

    public RequestObservabilityMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<LoggingOptions> loggingOptions,
        ILogger<RequestObservabilityMiddleware> logger)
    {
        var traceId = Truncate(context.Request.Headers[ObservabilityHeaderNames.TraceId].FirstOrDefault())
            ?? Guid.NewGuid().ToString();
        var sessionId = Truncate(context.Request.Headers[ObservabilityHeaderNames.SessionId].FirstOrDefault());
        var clientPlatform = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientPlatform].FirstOrDefault());
        var clientVersion = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientVersion].FirstOrDefault());

        // Setado ANTES de next() — seguro mesmo se algo mais adiante
        // lançar: Response.Headers pode ser escrito a qualquer momento
        // antes do corpo começar a ser gravado (não acontece ainda aqui).
        context.Response.Headers[ObservabilityHeaderNames.TraceId] = traceId;

        var requestBody = await CaptureRequestBodyIfJsonAsync(context.Request);

        // Swap do Response.Body por um MemoryStream: só assim dá pra
        // decidir DEPOIS (com o status code final em mãos) se o corpo
        // da resposta entra no log — copiado de volta pro stream
        // original ao final, senão nada chega no client de verdade.
        var originalResponseBody = context.Response.Body;
        await using var capturedResponseBody = new MemoryStream();
        context.Response.Body = capturedResponseBody;

        using var _ = LogContext.PushProperty("TraceId", traceId);
        using var __ = LogContext.PushProperty("SessionId", sessionId);
        using var ___ = LogContext.PushProperty("ClientPlatform", clientPlatform);
        using var ____ = LogContext.PushProperty("ClientVersion", clientVersion);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context); // UseExceptionHandler roda aqui dentro — status code final já vem pronto
        }
        finally
        {
            stopwatch.Stop();

            capturedResponseBody.Seek(0, SeekOrigin.Begin);
            await capturedResponseBody.CopyToAsync(originalResponseBody);
            context.Response.Body = originalResponseBody;

            var entry = RequestLogEntryBuilder.Build(
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                traceId, sessionId, clientPlatform, clientVersion,
                userId: context.User.FindFirst("sub")?.Value,
                fullPayloadLoggingEnabled: loggingOptions.Value.FullPayloadLoggingEnabled,
                requestContentType: context.Request.ContentType,
                requestBody: requestBody,
                responseContentType: context.Response.ContentType,
                responseBody: ReadCapturedBodyAsString(capturedResponseBody));

            logger.LogInformation("Requisição concluída: {@RequestLog}", entry);
        }
    }

    // ...Truncate / CaptureRequestBodyIfJsonAsync / ReadCapturedBodyAsString...
}
```

Registro em `Program.cs` (**antes** de `UseExceptionHandler()`):

```csharp
app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseExceptionHandler();
```

## Recursos AWS

- **2 novos parâmetros no Parameter Store** (`String`, não segredo —
  só liga/desliga um log mais verboso):
  - Prod (`environments/prod/parameter-store.tf`):
    ```hcl
    resource "aws_ssm_parameter" "logging_full_payload_enabled" {
      name  = "/GastosApp/Logging/FullPayloadLoggingEnabled"
      type  = "String"
      value = "false"
    }
    ```
  - Hom (`environments/hom/parameter-store.tf`):
    ```hcl
    resource "aws_ssm_parameter" "logging_full_payload_enabled" {
      name  = "/GastosApp/Hom/Logging/FullPayloadLoggingEnabled"
      type  = "String"
      value = "false"
    }
    ```
  Sem IAM novo — a statement `ParameterStoreAccess` de cada ambiente já
  cobre `arn:...parameter/GastosApp/*` (prod) e
  `arn:...parameter/GastosApp/Hom/*` (hom) via `ssm:GetParametersByPath`
  (`lambda.tf`), que já inclui qualquer parâmetro novo sob esses
  prefixos.
- **Retenção de log group, 6 recursos existentes ajustados** (2
  ambientes × 3 Lambdas do backend — API principal, trigger de conta,
  trigger de Custom Message — todos hoje em `retention_in_days = 14`,
  ver "Pontos que precisam de confirmação", item 1):
  - Hom: `environments/hom/lambda.tf`,
    `environments/hom/lambda-account-trigger.tf`,
    `environments/hom/lambda-custom-message-trigger.tf` →
    `retention_in_days = 7`
  - Prod: `environments/prod/lambda.tf`,
    `environments/prod/lambda-account-trigger.tf`,
    `environments/prod/lambda-custom-message-trigger.tf` →
    `retention_in_days = 15`
- **CORS do API Gateway (HTTP API) — mudança necessária pro header
  chegar de verdade num browser**: `cors_configuration` de
  `aws_apigatewayv2_api.main` (`environments/{hom,prod}/api-gateway.tf`)
  hoje só libera `allow_headers = ["Authorization", "Content-Type"]`.
  Sem incluir os 4 headers novos aí, o preflight `OPTIONS` do navegador
  (frontend web enviando `trace-id`/`session-id`/etc.) é recusado pelo
  próprio API Gateway, antes até de chegar na Lambda:
  ```hcl
  cors_configuration {
    allow_origins     = var.frontend_origins
    allow_methods     = ["GET", "POST", "PUT", "DELETE", "OPTIONS"]
    allow_headers     = ["Authorization", "Content-Type", "trace-id", "session-id", "client-platform", "client-version"]
    allow_credentials = true
    expose_headers    = ["trace-id"] # senão o JS do browser não consegue ler o header de resposta
  }
  ```
  Mudança em ambos os ambientes (`hom` e `prod`).
- **Nenhuma dependência de código nova** (NuGet) — `JsonDocument`/
  `Utf8JsonWriter`/`JsonFormatter` já vêm do BCL/`Serilog` base, sem
  pacote adicional.

## Mapeamento de erro

Não se aplica — FEAT-38 não introduz nenhum `Error`/`ErrorType` novo,
nem muda status code ou `type` de nenhuma resposta de erro existente
(os 4 headers são só request/log; o único header de resposta novo,
`trace-id`, aparece igual em sucesso e erro). `openapi.json` não deve
mudar: o gerador de OpenAPI nativo do projeto (`AddOpenApi()`, Minimal
APIs) documenta request/response *body* e status codes por operação —
não headers de middleware cross-cutting — então não há
`.Produces<T>()`/`.WithOpenApi()` novo a adicionar; rodar
`export-openapi.sh` ao final só pra confirmar que o arquivo sai
idêntico (ver critério de aceite correspondente do spec.md, que previa
esse cenário como condicional).

## Decisões técnicas relevantes

1. **`RequestObservabilityMiddleware` registrado ANTES de
   `UseExceptionHandler()`**, não depois: assim, quando `next()`
   retorna pro nosso middleware, `context.Response.StatusCode` já
   reflete o resultado final (200/4xx tratado pelo `Result` pattern, ou
   500 já escrito pelo `GlobalExceptionHandler`) — uma única linha de
   log por requisição, sempre com o status code correto, sem duplicar
   lógica de "logar no catch" dentro do próprio `GlobalExceptionHandler`.
2. **Corpo da resposta sempre bufferizado num `MemoryStream`**
   (trade-off aceito): não dá pra saber de antemão se a resposta vai
   ser erro (o que obrigaria logar o corpo) — bufferizar sempre é mais
   simples que tentar decidir cedo demais. Impacto de memória
   irrelevante pra esta aplicação (payloads pequenos, JSON de
   domínio pessoal) — a exceção relevante é a exportação CSV
   (`GET /transactions/export`), coberta pelo filtro de content-type
   abaixo (nunca teria o corpo lido/logado de qualquer forma, mas ainda
   passa pelo buffer/cópia — custo de I/O em memória, não de log).
3. **Só corpo `application/json` (ou vazio) entra em
   `RequestLogEntryBuilder`** — qualquer outro `Content-Type` (CSV,
   multipart, etc.) fica de fora do log de payload (mesmo em erro/
   toggle ligado), evitando truncar/poluir o log com formato que a
   redação de campo sensível não sabe interpretar, e evitando logar um
   corpo potencialmente grande (export CSV).
4. **Redação via `JsonDocument`/`Utf8JsonWriter`** (DOM, não
   `JsonSerializer.Deserialize<T>`) — evita qualquer necessidade de
   `JsonTypeInfo` source-generated pra um corpo de formato arbitrário
   (diferente do resto do projeto, que sempre serializa/desserializa
   tipos conhecidos via `AppJsonSerializerContext`) — mantém a
   feature 100% compatível com Native AOT sem ampliar o serializer
   context.
5. **Nomes de propriedade de log em PascalCase** (`TraceId`,
   `SessionId`, `ClientPlatform`, `ClientVersion`, `UserId`) mesmo os
   headers HTTP sendo kebab-case (`trace-id` etc., por convenção RFC
   6648/HTTP2+, ver spec.md) — evita nomes de campo com hífen em
   queries do CloudWatch Logs Insights (que exigiriam escapar cada
   `fields \`trace-id\``), sem nenhuma implicação de contrato (o hífen
   só importa no nome do header HTTP, nunca no log).
6. **Serilog: `Enrich.FromLogContext()` + `JsonFormatter()` do próprio
   pacote `Serilog`** (não `Serilog.Formatting.Compact`, que exigiria
   pacote novo) — aplicado igual em todo ambiente, inclusive dev local
   (console local passa a mostrar JSON em vez de texto simples). Aceito
   como trade-off pela simplicidade de uma única configuração — ver
   "Pontos que precisam de confirmação", item 2, caso o usuário prefira
   texto legível em dev local.
7. **Parâmetro de log-level lido uma única vez por cold start da
   Lambda** (mesmo padrão de leitura do Parameter Store já usado por
   `CognitoOptions`/`DynamoDbOptions`/`SesOptions`, via
   `AddAwsParameterStore` no `Program.cs`) — ligar/desligar o toggle no
   SSM não tem efeito imediato em instâncias já "quentes" da Lambda, só
   nas próximas que passarem por cold start. Aceito como limitação
   conhecida, consistente com o resto do projeto (nenhum outro `Options`
   tem reload em runtime hoje); não introduz polling nem mecanismo de
   configuração dinâmica nesta feature.
8. **`userId` extraído de `context.User.FindFirst("sub")`** direto no
   middleware (não via `CurrentAccountContext`, que só é populado pelo
   `ResolveAccountEndpointFilter` — específico de rotas de
   Category/Transaction/Members, FEAT-19) — o claim `sub` já está
   disponível em `HttpContext.User` assim que `UseAuthentication()`
   roda, cobrindo qualquer rota autenticada sem depender de um filtro
   por endpoint.

## Testes a criar

**Unit (`GastosApp.UnitTests/Api/SensitiveFieldRedactorTests.cs`,
novo)**:
- `Redact_ShouldMaskPassword_WhenPresentAtTopLevel`
- `Redact_ShouldMaskMultipleSensitiveFields_WhenPresentTogether`
  (Theory: `password`, `newPassword`, `code`, `token`, `refreshToken`)
- `Redact_ShouldMaskNestedSensitiveField_WhenPresentInsideObject`
- `Redact_ShouldNotChangeNonSensitiveFields`
- `Redact_ShouldReturnOriginal_WhenBodyIsNotValidJson`
- `Redact_ShouldReturnOriginal_WhenBodyIsNullOrEmpty`

**Unit (`GastosApp.UnitTests/Api/RequestLogEntryBuilderTests.cs`,
novo)**:
- `Build_ShouldIncludeBody_WhenStatusCodeIsError` (Theory: 400, 404,
  500) — mesmo com toggle desligado
- `Build_ShouldNotIncludeBody_WhenSuccessAndToggleDisabled`
- `Build_ShouldIncludeBody_WhenSuccessAndToggleEnabled`
- `Build_ShouldNotIncludeBody_WhenContentTypeIsNotJson` (Theory:
  `text/csv`, `null`, mesmo em erro/toggle ligado)
- `Build_ShouldTruncateBody_WhenLongerThanMaxLength`
- `Build_ShouldIncludeAllFourObservabilityFields_WhenPresent`
- `Build_ShouldAllowNullFields_WhenSessionIdClientPlatformClientVersionAbsent`

**Componente (`GastosApp.ComponentTests/Observability/
RequestObservabilityMiddlewareTests.cs`, novo)** — mesmo padrão de
`Cors/CorsTests.cs` (só HTTP observável; conteúdo do log não é
verificável aqui, ver "Pontos que precisam de confirmação", item 3):
- `Requisicao_ComTraceIdEnviado_EcoaMesmoValorNaResposta`
- `Requisicao_SemTraceIdEnviado_RecebeTraceIdGeradoNaResposta`
- `Requisicao_ComErro_AindaAssimRecebeHeaderTraceId` (ex.: rota
  protegida sem JWT → 401, ou payload inválido → 400)
- `Requisicao_ComSessionIdClientPlatformClientVersionAusentes_
  ContinuaFuncionandoNormalmente` (nenhum dos 3 é obrigatório)

**Integrado (`GastosApp.IntegrationTests/Observability/
ObservabilityFlowTests.cs`, novo)** — só pra validar contra o binário
Native AOT de verdade (risco real: `JsonDocument`/buffer de stream sob
`provided.al2023`), não porque é "endpoint novo" (não é):
- `Health_QualquerRequisicao_RecebeHeaderTraceIdNaResposta` — chama
  `GET /health` sem enviar `trace-id`, confirma que a resposta traz o
  header gerado pela API.

## Documentação a atualizar

- `backend/infra/CLAUDE.md` — nova seção curta sobre os 2 parâmetros
  `Logging/FullPayloadLoggingEnabled` (mesmo padrão das seções já
  existentes de Cognito/CORS/SES no Parameter Store) e sobre a
  retenção de log group ter deixado de ser uniforme entre hom/prod (7 x
  15 dias).
- `backend/docs/backlog.md` — ao final da implementação, os 3 débitos
  técnicos já previstos no spec.md (headers obrigatórios no futuro; log
  segmentado por `session-id`; propagação de `trace-id` pra Lambda de
  triggers via `ClientMetadata`).
- `backend/docs/openapi.json` — regenerar via `export-openapi.sh` só
  pra confirmar que sai idêntico (ver "Mapeamento de erro" acima).
- `backend/docs/data-model.md` — sem mudança (nenhum item novo no
  DynamoDB).

## Pontos confirmados com o usuário durante este `/plan`

1. **Retenção de log group cobre as 3 Lambdas do backend por
   ambiente** (API principal + os 2 triggers do Cognito), não só a API
   principal — confirmado.
2. **`JsonFormatter` do Serilog aplicado em todo ambiente, inclusive
   dev local** — console local também passa a mostrar uma linha JSON
   por evento em vez de texto simples, sem ramificação por ambiente —
   confirmado.
3. **Log de payload completo não é verificável por teste automatizado
   de ponta a ponta** (nem componente, nem integrado) — só a decisão
   "o que logar" é testada isoladamente (`RequestLogEntryBuilderTests`).
   Verificar de fato que uma linha JSON chega no CloudWatch com os
   campos esperados fica para validação manual em hom, mesmo espírito
   já aceito em outras features (ex.: FEAT-36, fluxo "código real por
   email" não testável) — confirmado, será sinalizado no `/tasks` como
   validação manual explícita.
4. **Nomes propostos para os arquivos/parâmetros novos**
   (`LoggingOptions`, `RequestObservabilityMiddleware`,
   `RequestLogEntryBuilder`, `SensitiveFieldRedactor`,
   `/GastosApp/Logging/FullPayloadLoggingEnabled`) — confirmados,
   seguem para o `/tasks` como estão.
