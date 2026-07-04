namespace GastosApp.Infrastructure.Configuration
{
    // Infra define o POCO — reflete o que vem do Parameter Store
    public sealed class CognitoOptions
    {
        public const string SectionName = "Cognito";

        public string Region { get; init; } = default!;
        public string UserPoolId { get; init; } = default!;
        public string ClientId { get; init; } = default!;
        public string? ServiceURL { get; init; } // só para dev local se precisar
        public string? AccessKey { get; init; }
        public string? SecretKey { get; init; }
    }
}
