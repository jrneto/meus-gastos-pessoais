using FluentValidation;
using GastosApp.Application.Common.Results;
using Mediator;

namespace GastosApp.Application.Common.Behaviors;

public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : Result, IValidationFailureFactory<TResponse>
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators)
    {
        _validators = validators;
    }

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(message, cancellationToken);

        var context = new ValidationContext<TMessage>(message);
        FluentValidation.Results.ValidationFailure? firstFailure = null;
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                firstFailure = result.Errors[0];
                break;
            }
        }

        if (firstFailure is null)
            return await next(message, cancellationToken);

        return TResponse.ValidationFailure(Error.Validation("validation-error", firstFailure.ErrorMessage));
    }
}
