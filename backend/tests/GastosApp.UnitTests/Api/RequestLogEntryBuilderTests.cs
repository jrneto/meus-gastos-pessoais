using FluentAssertions;
using GastosApp.Api.Common;

namespace GastosApp.UnitTests.Api;

public class RequestLogEntryBuilderTests
{
    [Theory]
    [InlineData(400)]
    [InlineData(404)]
    [InlineData(500)]
    public void Build_ShouldIncludeBody_WhenStatusCodeIsError(int statusCode)
    {
        var entry = RequestLogEntryBuilder.Build(
            "POST", "/transactions", statusCode, 12,
            traceId: "trace-1", sessionId: null, clientPlatform: null, clientVersion: null,
            userId: null,
            fullPayloadLoggingEnabled: false,
            requestContentType: "application/json", requestBody: """{"valor":100}""",
            responseContentType: "application/problem+json", responseBody: """{"title":"erro"}""");

        entry["RequestBody"].Should().NotBeNull();
        entry["ResponseBody"].Should().NotBeNull();
    }

    [Fact]
    public void Build_ShouldNotIncludeBody_WhenSuccessAndToggleDisabled()
    {
        var entry = RequestLogEntryBuilder.Build(
            "GET", "/health", 200, 5,
            traceId: "trace-1", sessionId: null, clientPlatform: null, clientVersion: null,
            userId: null,
            fullPayloadLoggingEnabled: false,
            requestContentType: null, requestBody: null,
            responseContentType: "application/json", responseBody: """{"status":"ok"}""");

        entry.Should().NotContainKey("RequestBody");
        entry.Should().NotContainKey("ResponseBody");
    }

    [Fact]
    public void Build_ShouldIncludeBody_WhenSuccessAndToggleEnabled()
    {
        var entry = RequestLogEntryBuilder.Build(
            "GET", "/health", 200, 5,
            traceId: "trace-1", sessionId: null, clientPlatform: null, clientVersion: null,
            userId: null,
            fullPayloadLoggingEnabled: true,
            requestContentType: null, requestBody: null,
            responseContentType: "application/json", responseBody: """{"status":"ok"}""");

        entry["ResponseBody"].Should().Be("""{"status":"ok"}""");
    }

    [Theory]
    [InlineData("text/csv")]
    [InlineData(null)]
    public void Build_ShouldNotIncludeBody_WhenContentTypeIsNotJson(string? contentType)
    {
        var entry = RequestLogEntryBuilder.Build(
            "GET", "/transactions/export", 500, 8,
            traceId: "trace-1", sessionId: null, clientPlatform: null, clientVersion: null,
            userId: null,
            fullPayloadLoggingEnabled: true,
            requestContentType: null, requestBody: null,
            responseContentType: contentType, responseBody: "data,que,nao,e,json");

        entry["ResponseBody"].Should().BeNull();
    }

    [Fact]
    public void Build_ShouldTruncateBody_WhenLongerThanMaxLength()
    {
        var hugeValue = new string('a', RequestLogEntryBuilder.MaxLoggedBodyLength + 500);
        var json = $$"""{"campo":"{{hugeValue}}"}""";

        var entry = RequestLogEntryBuilder.Build(
            "POST", "/transactions", 400, 10,
            traceId: "trace-1", sessionId: null, clientPlatform: null, clientVersion: null,
            userId: null,
            fullPayloadLoggingEnabled: false,
            requestContentType: "application/json", requestBody: json,
            responseContentType: null, responseBody: null);

        var loggedBody = (string)entry["RequestBody"]!;
        loggedBody.Length.Should().BeLessThan(json.Length);
        loggedBody.Should().EndWith("...(truncado)");
    }

    [Fact]
    public void Build_ShouldIncludeAllFourObservabilityFields_WhenPresent()
    {
        var entry = RequestLogEntryBuilder.Build(
            "GET", "/transactions", 200, 3,
            traceId: "trace-1", sessionId: "session-1", clientPlatform: "web", clientVersion: "1.2.3",
            userId: "user-1",
            fullPayloadLoggingEnabled: false,
            requestContentType: null, requestBody: null,
            responseContentType: null, responseBody: null);

        entry["TraceId"].Should().Be("trace-1");
        entry["SessionId"].Should().Be("session-1");
        entry["ClientPlatform"].Should().Be("web");
        entry["ClientVersion"].Should().Be("1.2.3");
        entry["UserId"].Should().Be("user-1");
    }

    [Fact]
    public void Build_ShouldAllowNullFields_WhenSessionIdClientPlatformClientVersionAbsent()
    {
        var entry = RequestLogEntryBuilder.Build(
            "GET", "/health", 200, 3,
            traceId: "trace-1", sessionId: null, clientPlatform: null, clientVersion: null,
            userId: null,
            fullPayloadLoggingEnabled: false,
            requestContentType: null, requestBody: null,
            responseContentType: null, responseBody: null);

        entry["SessionId"].Should().BeNull();
        entry["ClientPlatform"].Should().BeNull();
        entry["ClientVersion"].Should().BeNull();
        entry["UserId"].Should().BeNull();
    }
}
