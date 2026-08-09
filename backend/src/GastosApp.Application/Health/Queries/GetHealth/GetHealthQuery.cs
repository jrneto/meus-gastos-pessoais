using GastosApp.Application.Common.Results;
using Mediator;
using Microsoft.Extensions.Configuration;

namespace GastosApp.Application.Health.Queries.GetHealth;

public sealed record GetHealthQuery : IQuery<Result<HealthResponse>>;

// Lê versão/commit/ambiente publicados na Lambda pelo pipeline de CI/CD
// (variáveis de ambiente APP_VERSION/APP_COMMIT_SHA/APP_ENVIRONMENT,
// setadas via `aws lambda update-function-configuration` — fora do
// Terraform, ver backend/specs/FEAT-14-cicd-github-actions/plan.md).
// Fora da Lambda (dev local), nenhuma delas existe: cai no fallback.
public sealed class GetHealthQueryHandler : IQueryHandler<GetHealthQuery, Result<HealthResponse>>
{
    private readonly IConfiguration _configuration;

    public GetHealthQueryHandler(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public ValueTask<Result<HealthResponse>> Handle(GetHealthQuery query, CancellationToken cancellationToken)
    {
        var response = new HealthResponse(
            Status: "ok",
            Version: _configuration["APP_VERSION"] ?? "local",
            CommitSha: _configuration["APP_COMMIT_SHA"] ?? "unknown",
            Environment: _configuration["APP_ENVIRONMENT"] ?? "local");

        return ValueTask.FromResult(Result.Success(response));
    }
}
