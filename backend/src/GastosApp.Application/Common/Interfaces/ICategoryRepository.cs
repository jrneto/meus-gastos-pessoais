using GastosApp.Domain.Categories;

namespace GastosApp.Application.Common.Interfaces;

public enum CategoryWriteOutcome
{
    Success,
    NotFound,
    NameConflict
}

public sealed record CategoryWriteResult(CategoryWriteOutcome Outcome, Category? Category)
{
    public static CategoryWriteResult Success(Category category) => new(CategoryWriteOutcome.Success, category);
    public static CategoryWriteResult NotFound() => new(CategoryWriteOutcome.NotFound, null);
    public static CategoryWriteResult NameConflict() => new(CategoryWriteOutcome.NameConflict, null);
}

public interface ICategoryRepository
{
    Task<CategoryWriteResult> CreateAsync(Category category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Category>> ListAsync(string accountId, string? tipo, CancellationToken cancellationToken = default);
    Task<Category?> GetByIdAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
    Task<CategoryWriteResult> UpdateAsync(
        string accountId,
        string categoryId,
        string nome,
        string tipo,
        long? orcamentoMensalCents,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string accountId, string categoryId, CancellationToken cancellationToken = default);
}
