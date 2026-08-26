using FluentAssertions;
using GastosApp.Application.Transactions.Queries.ExportTransactions;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class TransactionCsvBuilderTests
{
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private static ExportTransactionRow Row(
        DateOnly? date = null,
        string description = "Almoço no restaurante",
        string categoryNome = "Alimentacao",
        string tipo = "despesa",
        long amountInCents = 4590,
        string createdByLabel = "Você") =>
        new(date ?? new DateOnly(2026, 8, 15), description, categoryNome, tipo, amountInCents, createdByLabel);

    private static string DecodeWithoutBom(byte[] bytes) =>
        System.Text.Encoding.UTF8.GetString(bytes.Skip(Bom.Length).ToArray());

    [Fact]
    public void Build_ShouldReturnOnlyHeaderLine_WhenRowsIsEmpty()
    {
        var bytes = TransactionCsvBuilder.Build([]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Be("data;descricao;categoria;tipo;valor;lancadoPor\r\n");
    }

    [Fact]
    public void Build_ShouldFormatColumnsInOrder_ForASingleRow()
    {
        var bytes = TransactionCsvBuilder.Build([Row()]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Be(
            "data;descricao;categoria;tipo;valor;lancadoPor\r\n" +
            "2026-08-15;Almoço no restaurante;Alimentacao;despesa;45,90;Você\r\n");
    }

    [Theory]
    [InlineData(4590, "45,90")]
    [InlineData(100, "1,00")]
    [InlineData(500000, "5000,00")]
    [InlineData(0, "0,00")]
    public void Build_ShouldFormatValorAsReaisWithCommaDecimal_WithoutThousandSeparator(long amountInCents, string expectedValor)
    {
        var bytes = TransactionCsvBuilder.Build([Row(amountInCents: amountInCents)]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Contain($";{expectedValor};");
    }

    [Fact]
    public void Build_ShouldQuoteField_WhenDescriptionContainsDelimiter()
    {
        var bytes = TransactionCsvBuilder.Build([Row(description: "Almoço; sobremesa")]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Contain("\"Almoço; sobremesa\"");
    }

    [Fact]
    public void Build_ShouldQuoteFieldAndDoubleInternalQuotes_WhenDescriptionContainsQuote()
    {
        var bytes = TransactionCsvBuilder.Build([Row(description: "Almoço \"extra\"")]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Contain("\"Almoço \"\"extra\"\"\"");
    }

    [Theory]
    [InlineData("Almoço\ncom sobremesa")]
    [InlineData("Almoço\rcom sobremesa")]
    public void Build_ShouldQuoteField_WhenDescriptionContainsLineBreak(string description)
    {
        var bytes = TransactionCsvBuilder.Build([Row(description: description)]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Contain($"\"{description}\"");
    }

    [Fact]
    public void Build_ShouldNotQuoteField_WhenItHasNoSpecialCharacter()
    {
        var bytes = TransactionCsvBuilder.Build([Row(description: "Almoço no restaurante")]);

        var content = DecodeWithoutBom(bytes);
        content.Should().Contain(";Almoço no restaurante;");
        content.Should().NotContain("\"Almoço no restaurante\"");
    }

    [Fact]
    public void Build_ShouldPrependUtf8Bom_ToTheReturnedBytes()
    {
        var bytes = TransactionCsvBuilder.Build([]);

        bytes.Take(3).Should().Equal(Bom);
    }
}
