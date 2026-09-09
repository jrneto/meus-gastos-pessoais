using FluentAssertions;
using GastosApp.Api.Common;
using Microsoft.AspNetCore.Http;

namespace GastosApp.UnitTests.Api;

public class ObservabilityHeaderValidatorTests
{
    [Theory]
    [InlineData("/health")]
    [InlineData("/HEALTH")]
    [InlineData("/Health")]
    public void IsExemptPath_ShouldReturnTrue_ForHealthPath(string path)
    {
        ObservabilityHeaderValidator.IsExemptPath(new PathString(path)).Should().BeTrue();
    }

    [Theory]
    [InlineData("/auth/login")]
    [InlineData("/transactions")]
    [InlineData("/")]
    [InlineData("/healthcheck")]
    public void IsExemptPath_ShouldReturnFalse_ForAnyOtherPath(string path)
    {
        ObservabilityHeaderValidator.IsExemptPath(new PathString(path)).Should().BeFalse();
    }

    [Fact]
    public void GetMissingHeaders_ShouldReturnEmpty_WhenAllThreePresent()
    {
        var missing = ObservabilityHeaderValidator.GetMissingHeaders(
            traceId: "trace-1", clientPlatform: "web", clientVersion: "1.2.3");

        missing.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null, "web", "1.2.3")]
    [InlineData("", "web", "1.2.3")]
    public void GetMissingHeaders_ShouldReturnTraceId_WhenOnlyTraceIdMissing(
        string? traceId, string clientPlatform, string clientVersion)
    {
        var missing = ObservabilityHeaderValidator.GetMissingHeaders(traceId, clientPlatform, clientVersion);

        missing.Should().ContainSingle().Which.Should().Be(ObservabilityHeaderNames.TraceId);
    }

    [Theory]
    [InlineData("trace-1", null, "1.2.3")]
    [InlineData("trace-1", "", "1.2.3")]
    public void GetMissingHeaders_ShouldReturnClientPlatform_WhenOnlyClientPlatformMissing(
        string traceId, string? clientPlatform, string clientVersion)
    {
        var missing = ObservabilityHeaderValidator.GetMissingHeaders(traceId, clientPlatform, clientVersion);

        missing.Should().ContainSingle().Which.Should().Be(ObservabilityHeaderNames.ClientPlatform);
    }

    [Theory]
    [InlineData("trace-1", "web", null)]
    [InlineData("trace-1", "web", "")]
    public void GetMissingHeaders_ShouldReturnClientVersion_WhenOnlyClientVersionMissing(
        string traceId, string clientPlatform, string? clientVersion)
    {
        var missing = ObservabilityHeaderValidator.GetMissingHeaders(traceId, clientPlatform, clientVersion);

        missing.Should().ContainSingle().Which.Should().Be(ObservabilityHeaderNames.ClientVersion);
    }

    [Fact]
    public void GetMissingHeaders_ShouldReturnAllThreeInOrder_WhenNonePresent()
    {
        var missing = ObservabilityHeaderValidator.GetMissingHeaders(
            traceId: null, clientPlatform: null, clientVersion: null);

        missing.Should().Equal(
            ObservabilityHeaderNames.TraceId,
            ObservabilityHeaderNames.ClientPlatform,
            ObservabilityHeaderNames.ClientVersion);
    }

    [Fact]
    public void BuildMissingHeadersError_ShouldListSingleMissingHeader_InMessage()
    {
        var error = ObservabilityHeaderValidator.BuildMissingHeadersError([ObservabilityHeaderNames.ClientVersion]);

        error.Code.Should().Be("missing-observability-headers");
        error.Message.Should().Be("Header(s) obrigatório(s) ausente(s): client-version");
    }

    [Fact]
    public void BuildMissingHeadersError_ShouldListAllMissingHeaders_InMessage()
    {
        var error = ObservabilityHeaderValidator.BuildMissingHeadersError(
        [
            ObservabilityHeaderNames.TraceId,
            ObservabilityHeaderNames.ClientPlatform,
            ObservabilityHeaderNames.ClientVersion
        ]);

        error.Message.Should().Be("Header(s) obrigatório(s) ausente(s): trace-id, client-platform, client-version");
    }
}
