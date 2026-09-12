using System.Text.Json;
using FluentAssertions;
using GastosApp.Api.Common;

namespace GastosApp.UnitTests.Api;

public class SensitiveFieldRedactorTests
{
    [Fact]
    public void Redact_ShouldMaskPassword_WhenPresentAtTopLevel()
    {
        var json = """{"email":"neto@email.com","password":"Senha@123"}""";

        var redacted = SensitiveFieldRedactor.Redact(json);

        using var document = JsonDocument.Parse(redacted);
        document.RootElement.GetProperty("password").GetString().Should().Be("***");
        document.RootElement.GetProperty("email").GetString().Should().Be("neto@email.com");
    }

    [Theory]
    [InlineData("password")]
    [InlineData("newPassword")]
    [InlineData("code")]
    [InlineData("token")]
    [InlineData("refreshToken")]
    public void Redact_ShouldMaskMultipleSensitiveFields_WhenPresentTogether(string sensitiveFieldName)
    {
        var json = $$"""{"email":"neto@email.com","{{sensitiveFieldName}}":"valor-secreto"}""";

        var redacted = SensitiveFieldRedactor.Redact(json);

        using var document = JsonDocument.Parse(redacted);
        document.RootElement.GetProperty(sensitiveFieldName).GetString().Should().Be("***");
        document.RootElement.GetProperty("email").GetString().Should().Be("neto@email.com");
    }

    [Fact]
    public void Redact_ShouldMaskNestedSensitiveField_WhenPresentInsideObject()
    {
        var json = """{"email":"neto@email.com","credentials":{"password":"Senha@123","note":"ok"}}""";

        var redacted = SensitiveFieldRedactor.Redact(json);

        using var document = JsonDocument.Parse(redacted);
        var credentials = document.RootElement.GetProperty("credentials");
        credentials.GetProperty("password").GetString().Should().Be("***");
        credentials.GetProperty("note").GetString().Should().Be("ok");
    }

    [Fact]
    public void Redact_ShouldNotChangeNonSensitiveFields()
    {
        var json = """{"email":"neto@email.com","name":"Neto","age":30,"active":true,"note":null}""";

        var redacted = SensitiveFieldRedactor.Redact(json);

        using var document = JsonDocument.Parse(redacted);
        document.RootElement.GetProperty("email").GetString().Should().Be("neto@email.com");
        document.RootElement.GetProperty("name").GetString().Should().Be("Neto");
        document.RootElement.GetProperty("age").GetInt32().Should().Be(30);
        document.RootElement.GetProperty("active").GetBoolean().Should().BeTrue();
        document.RootElement.GetProperty("note").ValueKind.Should().Be(JsonValueKind.Null);
    }

    [Fact]
    public void Redact_ShouldReturnOriginal_WhenBodyIsNotValidJson()
    {
        const string notJson = "isto não é um JSON válido {";

        var result = SensitiveFieldRedactor.Redact(notJson);

        result.Should().Be(notJson);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Redact_ShouldReturnOriginal_WhenBodyIsNullOrEmpty(string? body)
    {
        var result = SensitiveFieldRedactor.Redact(body!);

        result.Should().Be(body);
    }
}
