using System.Diagnostics;
using System.Text;
using GastosApp.Api.Common;
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
        var traceId = Truncate(context.Request.Headers[ObservabilityHeaderNames.TraceId].ToString())
            ?? Guid.NewGuid().ToString();
        var sessionId = Truncate(context.Request.Headers[ObservabilityHeaderNames.SessionId].ToString());
        var clientPlatform = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientPlatform].ToString());
        var clientVersion = Truncate(context.Request.Headers[ObservabilityHeaderNames.ClientVersion].ToString());

        // Setado ANTES de next() — seguro mesmo se algo mais adiante
        // lançar: Response.Headers pode ser escrito a qualquer momento
        // antes do corpo começar a ser gravado (não acontece ainda aqui).
        context.Response.Headers[ObservabilityHeaderNames.TraceId] = traceId;

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
                userId: context.User.FindFirst("sub")?.Value,
                fullPayloadLoggingEnabled: loggingOptions.Value.FullPayloadLoggingEnabled,
                requestContentType: context.Request.ContentType,
                requestBody: requestBody,
                responseContentType: context.Response.ContentType,
                responseBody: responseBytes.Length == 0 ? null : Encoding.UTF8.GetString(responseBytes));

            logger.LogInformation("Requisição concluída: {@RequestLog}", entry);
        }
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
