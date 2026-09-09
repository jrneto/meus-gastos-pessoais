using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using GastosApp.Api.Common;
using GastosApp.Application.Common.Results;
using GastosApp.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace GastosApp.Api.Middlewares;

// Lê/gera os 4 headers de observabilidade (FEAT-38), enriquece o log de
// toda requisição via Serilog LogContext e loga uma linha "requisição
// concluída" ao final, decidindo se inclui o payload completo. Registrado
// ANTES de UseExceptionHandler() em Program.cs — assim, quando next()
// retorna, context.Response.StatusCode já reflete o resultado final
// (200/4xx do Result pattern, ou 500 já escrito pelo
// GlobalExceptionHandler), sem duplicar lógica de log no catch.
public sealed class RequestObservabilityMiddleware
{
    private readonly RequestDelegate _next;

    public RequestObservabilityMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(
        HttpContext context,
        IOptions<LoggingOptions> loggingOptions,
        ILogger<RequestObservabilityMiddleware> logger)
    {
        // Preflight de CORS: sempre 204, sem corpo, sem userId — nenhum
        // dado de negócio a mais. Logar cada um só infla custo de
        // armazenamento no CloudWatch à toa; UseCors (adiante no pipeline)
        // resolve o preflight sozinho, sem passar pelos endpoints reais.
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        var rawTraceId = Truncate(context.Request.Headers[ObservabilityHeaderNames.TraceId].ToString());
        var sessionId = Truncate(context.Request.Headers[ObservabilityHeaderNames.SessionId].ToString());
        var clientPlatform = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientPlatform].ToString());
        var clientVersion = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientVersion].ToString());

        // Fallback de geração (FEAT-38) preservado só para o valor
        // ecoado na resposta/log (FEAT-39) — nunca "resgata" a
        // requisição da validação abaixo: um trace-id ausente conta como
        // ausente igual aos outros dois, numa rota não isenta.
        var traceId = rawTraceId ?? Guid.NewGuid().ToString();

        // Setado ANTES de next() — seguro mesmo se algo mais adiante
        // lançar: Response.Headers pode ser escrito a qualquer momento
        // antes do corpo começar a ser gravado (não acontece ainda aqui).
        context.Response.Headers[ObservabilityHeaderNames.TraceId] = traceId;

        // FEAT-39: trace-id/client-platform/client-version passam a ser
        // obrigatórios em toda rota não isenta — GET /health continua
        // aceitando qualquer combinação (checagem manual de versão via
        // curl simples, ver ObservabilityHeaderValidator).
        if (!ObservabilityHeaderValidator.IsExemptPath(context.Request.Path))
        {
            var missingHeaders = ObservabilityHeaderValidator.GetMissingHeaders(rawTraceId, clientPlatform, clientVersion);
            if (missingHeaders.Count > 0)
            {
                await RejectMissingHeadersAsync(context, logger, traceId, sessionId, clientPlatform, clientVersion, missingHeaders);
                return; // next() nunca é chamado — nenhum Command/Query roda
            }
        }

        var requestBody = await CaptureRequestBodyIfJsonAsync(context.Request);

        // Swap do Response.Body por um MemoryStream: só assim dá pra
        // decidir DEPOIS (com o status code final em mãos) se o corpo da
        // resposta entra no log — copiado de volta pro stream original ao
        // final, senão nada chega no client de verdade.
        var originalResponseBody = context.Response.Body;
        await using var capturedResponseBody = new MemoryStream();
        context.Response.Body = capturedResponseBody;

        using var traceIdScope = LogContext.PushProperty("TraceId", traceId);
        using var sessionIdScope = LogContext.PushProperty("SessionId", sessionId);
        using var clientPlatformScope = LogContext.PushProperty("ClientPlatform", clientPlatform);
        using var clientVersionScope = LogContext.PushProperty("ClientVersion", clientVersion);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            await _next(context); // UseExceptionHandler roda aqui dentro — status code final já vem pronto
        }
        finally
        {
            stopwatch.Stop();

            var responseBytes = capturedResponseBody.ToArray();
            await originalResponseBody.WriteAsync(responseBytes);
            context.Response.Body = originalResponseBody;

            var entry = RequestLogEntryBuilder.Build(
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                traceId, sessionId, clientPlatform, clientVersion,
                // Mesmo fallback já usado em AuthEndpoints.cs/
                // ResolveAccountEndpointFilter.cs — dependendo do
                // mapeamento de claims do JwtBearerHandler, "sub" pode
                // chegar como ClaimTypes.NameIdentifier.
                userId: context.User.FindFirst("sub")?.Value
                    ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                fullPayloadLoggingEnabled: loggingOptions.Value.FullPayloadLoggingEnabled,
                requestContentType: context.Request.ContentType,
                requestBody: requestBody,
                responseContentType: context.Response.ContentType,
                responseBody: responseBytes.Length == 0 ? null : Encoding.UTF8.GetString(responseBytes));

            logger.LogInformation("Requisição concluída: {@RequestLog}", entry);
        }
    }

    // FEAT-39: request sem 1+ dos 3 headers obrigatórios (trace-id,
    // client-platform, client-version) numa rota não isenta — rejeitada
    // antes de next(), nenhum Command/Query chega a rodar. Reaproveita
    // Result/ResultHttpExtensions (mesma fábrica de ProblemDetails usada
    // em qualquer outro 400 do projeto) em vez de montar um Results.Json
    // manual aqui.
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

        // Preserva o invariante da FEAT-38 de que toda requisição gera
        // uma linha de log, mesmo esta sendo rejeitada antes de next().
        // responseBody: null de propósito — reconstruí-lo exigiria o
        // mesmo buffer de Response.Body que este atalho existe pra
        // evitar; o corpo do erro já é 100% previsível a partir do
        // próprio detail do ProblemDetails (ver plan.md, decisão técnica 5).
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
            responseBody: null);

        logger.LogInformation("Requisição concluída: {@RequestLog}", entry);
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        return value.Length > RequestLogEntryBuilder.MaxHeaderValueLength
            ? value[..RequestLogEntryBuilder.MaxHeaderValueLength]
            : value;
    }

    // Só bufferiza/lê o corpo da requisição quando Content-Type é JSON —
    // evita ler à toa corpo vazio (GET) ou de outro formato.
    // EnableBuffering() + reset de Position: o corpo continua legível
    // normalmente pelo model binding do endpoint, mais adiante.
    private static async Task<string?> CaptureRequestBodyIfJsonAsync(HttpRequest request)
    {
        if (request.ContentLength is null or 0)
            return null;

        if (!RequestLogEntryBuilder.IsJsonContentType(request.ContentType))
        {
            return null;
        }

        request.EnableBuffering();

        using var reader = new StreamReader(
            request.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;

        return body;
    }
}
