namespace GastosApp.Application.Common.Results;

public enum ErrorType
{
    Validation,
    Conflict,
    Unauthorized,
    NotFound,
    UnprocessableEntity,
    Failure
}