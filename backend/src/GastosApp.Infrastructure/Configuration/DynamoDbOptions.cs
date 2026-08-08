namespace GastosApp.Infrastructure.Configuration
{
    public sealed class DynamoDbOptions
    {
        public const string SectionName = "DynamoDb";

        public string TableName { get; init; } = "GastosApp";
        public string Region { get; init; } = "us-east-1";
    }
}
