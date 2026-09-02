namespace GastosApp.IntegrationTests.Support;

// DTOs de request/response — espelham backend/docs/openapi.json (fonte
// de verdade do contrato de wire), não os records internos da API
// (GastosApp.Api.Endpoints.AuthEndpoints etc.). Deliberadamente
// duplicados: esta suíte é black-box, não deve depender de tipos de
// produção — se o contrato divergir, o teste falha na desserialização,
// que é exatamente o tipo de regressão que um teste integrado deve
// pegar.

public sealed record RegisterRequestDto(string Email, string Password, string Name, string PhoneNumber, string Cpf);
public sealed record RegisterResponseDto(string UserId, string Email, string Name, string PhoneNumber, string Cpf);

public sealed record LoginRequestDto(string Email, string Password);
public sealed record LoginResponseDto(string AccessToken, int ExpiresIn, string UserId);

/// <summary>Corpo de erro RFC 9457 (ProblemDetails) devolvido por GastosApp.Api.Common.ResultHttpExtensions.</summary>
public sealed record ProblemDetailsDto(string? Type, string? Title, int? Status, string? Detail);

// Confirmação de cadastro via OTP (FEAT-35) — ver
// backend/specs/FEAT-35-confirmacao-cadastro-otp/spec.md
public sealed record ConfirmRequestDto(string Email, string Code);
public sealed record ResendConfirmationRequestDto(string Email);

// Membros (FEAT-20) — ver backend/specs/FEAT-20-membros-convites-permissoes/spec.md
public sealed record MemberRequestDto(string Email, string Role);
public sealed record MemberRoleRequestDto(string Role);
public sealed record MemberResponseDto(string Id, string Email, string Role, string Status, string CreatedAt);
public sealed record MemberListResponseDto(List<MemberResponseDto> Items);

// Categorias (FEAT-16 + FEAT-21) — ver backend/specs/FEAT-21-categoria-tipo-orcamento/spec.md
public sealed record CategoryRequestDto(string Nome, string Tipo, long? OrcamentoMensalCents);
public sealed record CategoryResponseDto(string Id, string Nome, string Tipo, long? OrcamentoMensalCents, string CreatedAt);
public sealed record CategoryListResponseDto(List<CategoryResponseDto> Items);

// Transações (FEAT-22) — ver backend/specs/FEAT-22-transacoes-receita-despesa/spec.md
public sealed record TransactionRequestDto(string Description, long AmountInCents, string CategoryId, string Tipo, string Date);
public sealed record TransactionResponseDto(string Id, string Description, long AmountInCents, string CategoryId, string Tipo, string Date, string CreatedByUserId, string CreatedByLabel, string CreatedAt);
public sealed record TransactionListResponseDto(List<TransactionResponseDto> Items, string? NextCursor);

// Resumo mensal (FEAT-23) — ver backend/specs/FEAT-23-resumo-mensal-dashboard/spec.md
public sealed record CategorySummaryItemDto(string CategoryId, string Nome, long GastoCents, long? OrcamentoMensalCents);
public sealed record SummaryResponseDto(string Month, long SaldoCents, long ReceitasCents, long GastoCents, long OrcamentoTotalCents, long RestanteCents, List<CategorySummaryItemDto> PorCategoria, List<TransactionResponseDto> UltimosLancamentos);

// Relatórios (FEAT-24) — ver backend/specs/FEAT-24-relatorios-por-periodo/spec.md
public sealed record ReportCategoryItemDto(string CategoryId, string Nome, long GastoCents);
public sealed record ReportTopCategoryDto(string CategoryId, string Nome, long GastoCents, decimal? PercentualOrcamento);
public sealed record ReportsResponseDto(string Period, string StartDate, string EndDate, long TotalCents, decimal? VariacaoPercentual, List<ReportCategoryItemDto> PorCategoria, ReportTopCategoryDto? MaiorGasto);
