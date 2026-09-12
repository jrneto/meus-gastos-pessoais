namespace GastosApp.Infrastructure.Configuration
{
    // Infra define o POCO — reflete o que vem do Parameter Store
    public sealed class LoggingOptions
    {
        public const string SectionName = "Logging";

        // Toggle global de log de payload completo (FEAT-38) — não por
        // sessão específica (decisão do /specify, virou débito técnico).
        // "true"/"false" como string no Parameter Store, convertido na
        // leitura manual (InfrastructureServiceCollectionExtensions);
        // ausente ou qualquer valor != "true" = false.
        public bool FullPayloadLoggingEnabled { get; init; }
    }
}
