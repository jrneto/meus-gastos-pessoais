namespace GastosApp.Application.Common.Results;

public interface IValidationFailureFactory<TSelf> where TSelf : IValidationFailureFactory<TSelf>
{
    static abstract TSelf ValidationFailure(Error error);
}
