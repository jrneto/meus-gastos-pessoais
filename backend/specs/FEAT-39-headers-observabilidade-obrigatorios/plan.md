# Plan — FEAT-39: Headers de observabilidade obrigatórios (trace-id, client-platform, client-version)

## Camadas afetadas

- **Api**:
  - `Middlewares/RequestObservabilityMiddleware.cs` (existente, FEAT-38)
    — ganha a validação dos 3 headers obrigatórios logo após lê-los do
    request, antes de montar o `LogContext`/chamar `_next()`. Rota
    isenta (`GET /health`) e requisições `OPTIONS` (preflight, já
    tratado hoje) pulam a validação.
  - `Common/ObservabilityHeaderValidator.cs` (novo) — lógica pura (sem
    `HttpContext`/Serilog direto, mesmo espírito de
    `RequestLogEntryBuilder`): decide se um path está isento da
    obrigatoriedade e quais dos 3 headers obrigatórios estão ausentes.
    Testável por unit test sem precisar do pipeline HTTP.
  - `Common/ObservabilityHeaderNames.cs` — sem mudança (constantes já
    existem).
  - `Common/RequestLogEntryBuilder.cs` — sem mudança de assinatura;
    reaproveitado como está para logar a linha "Requisição concluída"
    também no caminho de rejeição (400 por header ausente).
  - `Common/ResultHttpExtensions.cs` — sem mudança; reaproveitado via
    `Result.Failure(Error.Validation(...)).ToHttpResult(...)` para
    montar o `ProblemDetails` do 400, igual a qualquer outro erro de
    validação do projeto (única fonte de verdade do formato de erro).
  - `Program.cs` — sem mudança (middleware já registrado antes de
    `UseExceptionHandler()` desde a FEAT-38).
- **Application/Domain/Infrastructure** — sem mudança. Nenhum
  Command/Query, nenhuma entidade envolvida, nenhum `Options`/parâmetro
  novo — feature 100% Api (cross-cutting), assim como a FEAT-38.

## Contratos técnicos

### `Common/ObservabilityHeaderValidator.cs` (Api, novo)

Lógica pura, sem `HttpContext`/Serilog direto — recebe valores já
extraídos (mesmo padrão de `RequestLogEntryBuilder`).

```csharp
public static class ObservabilityHeaderValidator
{
    // GET /health fica isento da obrigatoriedade dos 3 headers —
    // continua consultável via curl manual simples (checagem de
    // versão em produção), ver HealthEndpoints.cs.
    private static readonly PathString HealthPath = "/health";

    public static bool IsExemptPath(PathString path) =>
        path.StartsWithSegments(HealthPath, StringComparison.OrdinalIgnoreCase);

    // Devolve os nomes dos headers obrigatórios ausentes (lista vazia
    // se nenhum), na ordem trace-id/client-platform/client-version.
    // Recebe os valores JÁ truncados (mesmo Truncate() já usado pelo
    // middleware) — vazio ou nulo conta como ausente.
    public static IReadOnlyList<string> GetMissingHeaders(
        string? traceId, string? clientPlatform, string? clientVersion)
    {
        var missing = new List<string>(3);
        if (string.IsNullOrEmpty(traceId))
            missing.Add(ObservabilityHeaderNames.TraceId);
        if (string.IsNullOrEmpty(clientPlatform))
            missing.Add(ObservabilityHeaderNames.ClientPlatform);
        if (string.IsNullOrEmpty(clientVersion))
            missing.Add(ObservabilityHeaderNames.ClientVersion);
        return missing;
    }

    // Mensagem única, reaproveitando o mesmo Error/ProblemDetails de
    // qualquer outra validação do projeto (ver ResultHttpExtensions).
    public static Error BuildMissingHeadersError(IReadOnlyList<string> missingHeaders) =>
        Error.Validation(
            "missing-observability-headers",
            $"Header(s) obrigatório(s) ausente(s): {string.Join(", ", missingHeaders)}");
}
```

### `Middlewares/RequestObservabilityMiddleware.cs` (Api, alterado)

Trecho novo, inserido logo depois de ler os 4 headers (antes do
`context.Response.Headers[...TraceId] = traceId` já existente):

```csharp
var rawTraceId = Truncate(context.Request.Headers[ObservabilityHeaderNames.TraceId].ToString());
var sessionId = Truncate(context.Request.Headers[ObservabilityHeaderNames.SessionId].ToString());
var clientPlatform = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientPlatform].ToString());
var clientVersion = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientVersion].ToString());

// Fallback de geração só é usado para o VALOR ECOADO na resposta —
// nunca "resgata" a requisição da validação abaixo. Um trace-id
// ausente conta como ausente do mesmo jeito que os outros dois.
var traceId = rawTraceId ?? Guid.NewGuid().ToString();
context.Response.Headers[ObservabilityHeaderNames.TraceId] = traceId;

if (!ObservabilityHeaderValidator.IsExemptPath(context.Request.Path))
{
    var missingHeaders = ObservabilityHeaderValidator.GetMissingHeaders(rawTraceId, clientPlatform, clientVersion);
    if (missingHeaders.Count > 0)
    {
        await RejectMissingHeadersAsync(
            context, logger, traceId, sessionId, clientPlatform, clientVersion, missingHeaders);
        return; // next() nunca é chamado — nenhum Command/Query roda
    }
}

// ...restante do método (captura de corpo, swap de Response.Body,
// LogContext, chamada a _next()) segue igual à FEAT-38.
```

Novo método privado, reaproveitando `ResultHttpExtensions` (mesma
fábrica de `ProblemDetails` de qualquer outro 400 do projeto) e ainda
gerando a linha de log padrão da FEAT-38 para esta requisição rejeitada:

```csharp
private static async Task RejectMissingHeadersAsync(
    HttpContext context,
    ILogger logger,
    string traceId,
    string? sessionId,
    string? clientPlatform,
    string? clientVersion,
    IReadOnlyList<string> missingHeaders)
{
    var stopwatch = Stopwatch.StartNew();

    var requestBody = await CaptureRequestBodyIfJsonAsync(context.Request);

    var error = ObservabilityHeaderValidator.BuildMissingHeadersError(missingHeaders);
    var httpResult = Result.Failure(error).ToHttpResult(() => Results.Ok());
    await httpResult.ExecuteAsync(context);

    stopwatch.Stop();

    var entry = RequestLogEntryBuilder.Build(
        context.Request.Method,
        context.Request.Path,
        context.Response.StatusCode,
        stopwatch.ElapsedMilliseconds,
        traceId, sessionId, clientPlatform, clientVersion,
        userId: null, // UseAuthentication() ainda não rodou nesta rejeição
        fullPayloadLoggingEnabled: false, // irrelevante: 400 já força log de payload
        requestContentType: context.Request.ContentType,
        requestBody: requestBody,
        responseContentType: context.Response.ContentType,
        responseBody: null); // ver "Decisões técnicas relevantes", item 5

    logger.LogInformation("Requisição concluída: {@RequestLog}", entry);
}
```

Registro em `Program.cs`: **sem mudança** — mesma posição desde a
FEAT-38 (antes de `UseExceptionHandler()`).

## Recursos AWS

Nenhum recurso novo ou alterado. O CORS do API Gateway (`allow_headers`)
já libera `trace-id`, `client-platform` e `client-version` desde a
FEAT-38 — a mudança aqui é só de enforcement no lado da aplicação,
nenhum header novo é introduzido. Nenhum parâmetro novo no Parameter
Store, nenhuma mudança de IAM, nenhuma mudança de retenção de log
group.

## Mapeamento de erro

| Cenário | `ErrorType` | Status | `type` (URI) | `title` |
|---|---|---|---|---|
| 1+ dos 3 headers obrigatórios ausente, rota não isenta | `Validation` | 400 | `https://gastosapp.dev/errors/missing-observability-headers` | "Parâmetros inválidos" |

`detail` sempre presente, listando todos os headers ausentes (não só o
primeiro) — ex.: `"Header(s) obrigatório(s) ausente(s): client-version"`
ou `"Header(s) obrigatório(s) ausente(s): trace-id, client-platform, client-version"`.
Construído via `Error.Validation("missing-observability-headers", ...)`
+ `ResultHttpExtensions.ToHttpResult`, igual a qualquer outro erro de
validação do projeto — nenhum `ErrorType` novo, nenhuma mudança em
`ResultHttpExtensions.cs`.

`openapi.json`: mesma ressalva já registrada na FEAT-38 — o gerador
nativo (`AddOpenApi()`) documenta request/response *body* e status
codes por operação, não validação de middleware cross-cutting; rodar
`export-openapi.sh` ao final só para confirmar que sai idêntico (ou
identificar se algo mudou, ex.: se o Minimal APIs framework já injeta
um 400 genérico documentado por rota — a confirmar durante a
implementação).

## Decisões técnicas relevantes

1. **Validação acontece ANTES de qualquer coisa que dependa de
   `_next()`** — uma requisição com header obrigatório ausente nunca
   chega ao Mediator/handler/regra de negócio do endpoint. Short-circuit
   dentro do próprio `RequestObservabilityMiddleware`, sem middleware
   novo.
2. **Reaproveita `Result`/`Error`/`ResultHttpExtensions`
   (`GastosApp.Application.Common.Results`) para montar o
   `ProblemDetails`** em vez de construir um `Results.Json(new
   ProblemDetails{...})` manual no middleware — `ResultHttpExtensions.cs`
   já é a única fonte de verdade do formato de erro (RFC 9457) usada em
   toda a Api; evita um segundo lugar decidindo `status`/`title`/`type`
   para o mesmo `ErrorType.Validation`. `Api` já depende de
   `Application` hoje (mesmo `using` já existe em
   `ResultHttpExtensions.cs`), então não é uma dependência nova.
3. **Exceção de `/health` verificada por `PathString.StartsWithSegments`
   direto no middleware** (não via metadado do endpoint/
   `context.GetEndpoint()`) — mesmo estilo já usado hoje para o
   short-circuit de `OPTIONS` (`HttpMethods.IsOptions`), sem introduzir
   dependência de routing já ter resolvido o endpoint nesse ponto do
   pipeline.
4. **Fallback de geração de `trace-id` (`?? Guid.NewGuid()`) preservado,
   mas só para o valor ecoado na resposta** — deixou de "salvar" a
   requisição da validação (um `trace-id` ausente conta como ausente
   igual aos outros dois); serve só para a resposta de erro (e o log
   dela) continuarem correlacionáveis mesmo quando o próprio `trace-id`
   é um dos headers faltando (ver US3 do spec.md).
5. **Linha de log "Requisição concluída" também é emitida no caminho de
   rejeição** (mesma função `RequestLogEntryBuilder.Build`, reaproveitada
   como está) — preserva o invariante da FEAT-38 de que toda requisição
   gera uma linha de log. Assimetria proposta: corpo do **request** é
   capturado/logado se for JSON (mesma regra "todo erro loga payload" da
   FEAT-38); corpo da **resposta** não é capturado neste caminho rápido
   — reconstruir isso exigiria reintroduzir o buffer de
   `Response.Body` que este atalho existe justamente para evitar, e o
   corpo do erro já é 100% previsível a partir do próprio `detail`
   seria redundante logá-lo. Sinalizado abaixo para confirmação.
6. **Nenhum middleware novo, nenhum `Options`/parâmetro novo** — a
   validação vive dentro do `RequestObservabilityMiddleware` já
   existente, mantendo o único ponto de entrada estabelecido pela
   FEAT-38.

## Testes a criar/ajustar

**Unit (`GastosApp.UnitTests/Api/ObservabilityHeaderValidatorTests.cs`,
novo)**:
- `IsExemptPath_ShouldReturnTrue_ForHealthPath` (`/health`, variações de
  case)
- `IsExemptPath_ShouldReturnFalse_ForAnyOtherPath` (Theory: `/auth/login`,
  `/transactions`, `/`)
- `GetMissingHeaders_ShouldReturnEmpty_WhenAllThreePresent`
- `GetMissingHeaders_ShouldReturnTraceId_WhenOnlyTraceIdMissing`
  (Theory: cada um dos 3 headers isolado)
- `GetMissingHeaders_ShouldReturnAllThree_WhenNonePresent`
- `BuildMissingHeadersError_ShouldListAllMissingHeaders_InMessage`

**Componente (`GastosApp.ComponentTests/Observability/
RequestObservabilityMiddlewareTests.cs`, ajustar)**:
- Teste existente que hoje espera "sucesso mesmo sem
  session-id/client-platform/client-version" precisa ser dividido:
  `client-platform`/`client-version` ausentes agora esperam 400;
  `session-id` ausente continua esperando sucesso.
- `Requisicao_SemTraceId_Retorna400ComDetailIdentificandoTraceId`
- `Requisicao_SemClientPlatform_Retorna400`
- `Requisicao_SemClientVersion_Retorna400`
- `Requisicao_SemNenhumDosTresObrigatorios_Retorna400ComOsTresNoDetail`
- `Requisicao_Rejeitada_AindaAssimRecebeHeaderTraceIdNaResposta` (US3:
  gerado pela API quando o próprio `trace-id` também está ausente)
- `Health_SemNenhumHeaderDeObservabilidade_Continua200` (US4 — exemption
  explícita, distinta do teste genérico de trace-id já existente da
  FEAT-38)

**Integrado (`GastosApp.IntegrationTests/Observability/
ObservabilityFlowTests.cs`, ajustar)** — contra o binário Native AOT
real:
- Novo caso: uma rota não isenta chamada sem `client-version` recebe
  400 real do binário publicado (risco: comportamento do
  `Result.Failure(...).ToHttpResult(...).ExecuteAsync(...)` sob AOT).
- Teste existente de `/health` mantido — agora também comprova a
  exceção de propósito, não só o fallback de `trace-id`.

## Documentação a atualizar

- `backend/docs/backlog.md` — ao final da implementação, refinar o item
  "Headers de observabilidade continuam opcionais": passa a refletir
  que só `session-id` permanece opcional (e por quê — login/refresh sem
  sessão ainda, modo de navegação privada), conforme já previsto no
  critério de aceite do spec.md.
- `backend/docs/openapi.json` — regenerar via `export-openapi.sh`
  (confirmar se sai idêntico ou não, ver "Mapeamento de erro").
- `backend/infra/CLAUDE.md` / `backend/docs/data-model.md` — sem
  mudança (nenhum recurso AWS, nenhum item de DynamoDB envolvido).

## Pontos que precisam de confirmação antes do `/tasks`

1. **Assimetria de log no caminho de rejeição** (item 5 das decisões
   técnicas): request body capturado/logado quando JSON, response body
   não capturado. Confirmar se essa simplificação é aceitável, ou se
   vale reconstruir o corpo da resposta (ex.: serializar o
   `ProblemDetails` também para o log, sem precisar do buffer de
   stream) para manter os dois lados simétricos.
2. **Código do erro**: `missing-observability-headers` (usado no
   `type`, `https://gastosapp.dev/errors/missing-observability-headers`)
   — confirmar o nome, mesmo padrão kebab-case dos códigos já existentes
   em `AuthErrors`/`ValidationBehavior`.
3. **Exceção de rota via string literal `/health`** (não por metadado
   do endpoint) — confirmar que é aceitável a manutenção manual desse
   literal caso `/health` ganhe variações no futuro (ex.: `/health/live`),
   em vez de um mecanismo mais genérico (ex.: atributo/metadado no
   próprio endpoint marcando "isento de headers obrigatórios").
