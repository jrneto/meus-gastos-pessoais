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

// Membros (FEAT-20) — ver backend/specs/FEAT-20-membros-convites-permissoes/spec.md
public sealed record MemberRequestDto(string Email, string Role);
public sealed record MemberRoleRequestDto(string Role);
public sealed record MemberResponseDto(string Id, string Email, string Role, string Status, string CreatedAt);
public sealed record MemberListResponseDto(List<MemberResponseDto> Items);
