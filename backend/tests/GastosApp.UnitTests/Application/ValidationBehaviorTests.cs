using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Common.Behaviors;
using GastosApp.Application.Common.Results;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Domain.Transactions;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.Application;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_ShouldReturnValidationFailure_WithoutCallingNext_WhenValidatorFails()
    {
        var validator = Substitute.For<IValidator<RegisterTransactionCommand>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<RegisterTransactionCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Description", "Descrição é obrigatória.")]));

        var behavior = new ValidationBehavior<RegisterTransactionCommand, Result<RegisterTransactionResult>>([validator]);
        var command = new RegisterTransactionCommand("account-123", "", 100, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");

        var nextCalled = false;
        ValueTask<Result<RegisterTransactionResult>> Next(RegisterTransactionCommand _, CancellationToken __)
        {
            nextCalled = true;
            return new ValueTask<Result<RegisterTransactionResult>>(
                Result.Success(RegisterTransactionResult.FromEntity(
                    Transaction.Create("account-123", "x", 1, "category-1", "despesa", command.Date, "user-123"),
                    createdByLabel: "Você")));
        }

        var result = await behavior.Handle(command, Next, CancellationToken.None);

        nextCalled.Should().BeFalse();
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(ErrorType.Validation);
        result.Error.Code.Should().Be("validation-error");
        result.Error.Message.Should().Be("Descrição é obrigatória.");
    }

    [Fact]
    public async Task Handle_ShouldCallNext_WhenValidatorPasses()
    {
        var validator = Substitute.For<IValidator<RegisterTransactionCommand>>();
        validator
            .ValidateAsync(Arg.Any<ValidationContext<RegisterTransactionCommand>>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var behavior = new ValidationBehavior<RegisterTransactionCommand, Result<RegisterTransactionResult>>([validator]);
        var command = new RegisterTransactionCommand("account-123", "Almoço", 100, "category-1", "despesa", new DateOnly(2025, 6, 15), "user-123");
        var expected = Result.Success(RegisterTransactionResult.FromEntity(
            Transaction.Create("account-123", "Almoço", 100, "category-1", "despesa", command.Date, "user-123"),
            createdByLabel: "Você"));

        var result = await behavior.Handle(
            command, (_, _) => new ValueTask<Result<RegisterTransactionResult>>(expected), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task Handle_ShouldCallNextDirectly_WhenNoValidatorIsRegistered()
    {
        var behavior = new ValidationBehavior<LoginUserCommand, Result<LoginUserResult>>([]);
        var command = new LoginUserCommand("neto@email.com", "Senha123");
        var expected = Result.Success(new LoginUserResult("token", 3600, "user-id-123", "refresh-token-abc"));

        var result = await behavior.Handle(
            command, (_, _) => new ValueTask<Result<LoginUserResult>>(expected), CancellationToken.None);

        result.Should().BeSameAs(expected);
    }
}
