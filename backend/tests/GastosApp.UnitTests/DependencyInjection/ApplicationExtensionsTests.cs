using FluentAssertions;
using FluentValidation;
using GastosApp.Application.Auth.Commands.Confirm;
using GastosApp.Application.Auth.Commands.ForgotPassword;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Auth.Commands.ResendConfirmation;
using GastosApp.Application.Auth.Commands.ResetPassword;
using GastosApp.Application.Categories.Commands.CreateCategory;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Categories.Queries.GetCategories;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.DependencyInjection;
using GastosApp.Application.Members.Commands.InviteMember;
using GastosApp.Application.Members.Commands.UpdateMemberRole;
using GastosApp.Application.Reports.Queries.GetReports;
using GastosApp.Application.Summary.Queries.GetSummary;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Application.Transactions.Queries.ExportTransactions;
using GastosApp.Application.Transactions.Queries.GetTransactions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace GastosApp.UnitTests.DependencyInjection;

// Regressão para o bug encontrado em homologação: AddValidatorsFromAssembly()
// escaneia o assembly via reflection, incompatível com trimming/Native AOT
// (o publish com PublishAot=true já acusa "warning IL2104" para o assembly
// FluentValidation) — sob Lambda, o scan não achava nenhum validator, e
// ValidationBehavior seguia direto pro Handler sem validar nada. A correção
// (ApplicationServiceCollectionExtensions.cs) registra cada IValidator<T>
// manualmente; este teste garante que toda classe AbstractValidator<T> que
// existir no projeto está de fato registrada — se alguém criar um validator
// novo e esquecer de adicioná-lo lá, este teste falha.
public class ApplicationExtensionsTests
{
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<ICategoryRepository>()); // dependência de Register/UpdateTransactionCommandValidator
        services.AddApplicationServices();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IValidator<RegisterUserCommand>), typeof(RegisterUserCommandValidator))]
    [InlineData(typeof(IValidator<CreateCategoryCommand>), typeof(CreateCategoryCommandValidator))]
    [InlineData(typeof(IValidator<UpdateCategoryCommand>), typeof(UpdateCategoryCommandValidator))]
    [InlineData(typeof(IValidator<GetCategoriesQuery>), typeof(GetCategoriesQueryValidator))]
    [InlineData(typeof(IValidator<RegisterTransactionCommand>), typeof(RegisterTransactionCommandValidator))]
    [InlineData(typeof(IValidator<UpdateTransactionCommand>), typeof(UpdateTransactionCommandValidator))]
    [InlineData(typeof(IValidator<GetTransactionsQuery>), typeof(GetTransactionsQueryValidator))]
    [InlineData(typeof(IValidator<InviteMemberCommand>), typeof(InviteMemberCommandValidator))]
    [InlineData(typeof(IValidator<UpdateMemberRoleCommand>), typeof(UpdateMemberRoleCommandValidator))]
    [InlineData(typeof(IValidator<GetSummaryQuery>), typeof(GetSummaryQueryValidator))]
    [InlineData(typeof(IValidator<GetReportsQuery>), typeof(GetReportsQueryValidator))]
    [InlineData(typeof(IValidator<ExportTransactionsQuery>), typeof(ExportTransactionsQueryValidator))]
    [InlineData(typeof(IValidator<ConfirmSignUpCommand>), typeof(ConfirmSignUpCommandValidator))]
    [InlineData(typeof(IValidator<ResendConfirmationCodeCommand>), typeof(ResendConfirmationCodeCommandValidator))]
    [InlineData(typeof(IValidator<ForgotPasswordCommand>), typeof(ForgotPasswordCommandValidator))]
    [InlineData(typeof(IValidator<ResetPasswordCommand>), typeof(ResetPasswordCommandValidator))]
    public void AddApplicationServices_ShouldRegisterEveryValidator_ExplicitlyNotByAssemblyScan(
        Type validatorInterface, Type expectedImplementation)
    {
        using var provider = BuildProvider();

        var resolved = provider.GetService(validatorInterface);

        resolved.Should().NotBeNull($"{validatorInterface.Name} precisa estar registrado em ApplicationServiceCollectionExtensions");
        resolved!.GetType().Should().Be(expectedImplementation);
    }

    [Fact]
    public void AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownSixteen()
    {
        // Defensivo: se uma nova classe AbstractValidator<T> for criada no
        // projeto sem entrar na lista acima (e sem ser registrada em
        // ApplicationServiceCollectionExtensions), este teste de contagem
        // não pega isso sozinho — mas documenta a lista fechada esperada,
        // pra quem for adicionar um validator novo saber que precisa mexer
        // nos dois lugares (registro + este teste).
        // FEAT-35: 12 + ConfirmSignUpCommandValidator + ResendConfirmationCodeCommandValidator = 14.
        // FEAT-36: 14 + ForgotPasswordCommandValidator + ResetPasswordCommandValidator = 16.
        var expectedValidatorCount = 16;

        var validatorTypesInAssembly = typeof(ApplicationExtensions).Assembly.GetTypes()
            .Count(t => !t.IsAbstract && t.BaseType is { IsGenericType: true } baseType
                && baseType.GetGenericTypeDefinition() == typeof(AbstractValidator<>));

        validatorTypesInAssembly.Should().Be(expectedValidatorCount,
            "encontrar um número diferente de validators no assembly indica que um novo " +
            "AbstractValidator<T> foi criado sem ser registrado manualmente em " +
            "ApplicationServiceCollectionExtensions.cs (nunca usar AddValidatorsFromAssembly, " +
            "incompatível com Native AOT)");
    }
}
