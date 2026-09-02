namespace GastosApp.Infrastructure.Configuration
{
    // Infra define o POCO — reflete o que vem do Parameter Store
    public sealed class SesOptions
    {
        public const string SectionName = "Ses";

        public string SenderEmail { get; init; } = default!;
    }
}
