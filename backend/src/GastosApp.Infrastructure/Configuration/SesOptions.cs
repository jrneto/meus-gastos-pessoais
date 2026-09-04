namespace GastosApp.Infrastructure.Configuration
{
    // Infra define o POCO — reflete o que vem do Parameter Store
    public sealed class SesOptions
    {
        public const string SectionName = "Ses";

        public string SenderEmail { get; init; } = default!;

        // Default "us-east-1" (mesmo padrão de DynamoDbOptions.Region) —
        // nunca nulo mesmo sem "Ses:Region" configurado. Corrige bug real da
        // FEAT-37: o cliente SES reaproveitava CognitoOptions.Region, que a
        // Lambda de trigger de conta nunca configura de propósito (FEAT-19,
        // "sem cognito-idp:*") — RegionEndpoint.GetBySystemName(null) lançava
        // ArgumentNullException na resolução de DI, abortando
        // EnsureAccountCommand inteiro antes mesmo de Handle() rodar.
        public string Region { get; init; } = "us-east-1";
    }
}
