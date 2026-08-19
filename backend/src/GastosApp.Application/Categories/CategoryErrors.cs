using GastosApp.Application.Common.Results;

namespace GastosApp.Application.Categories;

public static class CategoryErrors
{
    public static Error NotFound => Error.NotFound("not-found", "Categoria não encontrada.");

    public static Error NameConflict =>
        Error.UnprocessableEntity("name-conflict", "Já existe uma categoria com esse nome.");

    public static Error CategoryInUse => Error.UnprocessableEntity(
        "category-in-use", "A categoria não pode ser excluída enquanto houver despesas associadas a ela.");
}
