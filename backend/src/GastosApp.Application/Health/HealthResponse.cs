namespace GastosApp.Application.Health;

// DTO simples de infraestrutura de deploy (versão/commit publicados) —
// não é construído a partir de uma entidade de domínio, então não usa
// o padrão FromEntity (ver GetHealthQueryHandler).
public sealed record HealthResponse(
    string Status,
    string Version,
    string CommitSha,
    string Environment);
