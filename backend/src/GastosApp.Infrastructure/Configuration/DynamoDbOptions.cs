namespace GastosApp.Infrastructure.Configuration
{
    public sealed class DynamoDbOptions
    {
        public const string SectionName = "DynamoDb";

        public string TableName { get; init; } = "GastosApp";
        public string Region { get; init; } = "us-east-1";
        public string? ServiceURL { get; init; } // só para dev local se precisar
        public string? AccessKey { get; init; }
        public string? SecretKey { get; init; }
    }
}
