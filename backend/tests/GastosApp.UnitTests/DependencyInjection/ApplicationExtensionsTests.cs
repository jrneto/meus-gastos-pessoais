using FluentAssertions;
using FluentValidation;
using GastosApp.Application.Categories.Commands.CreateCategory;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Common.Interfaces;
using GastosApp.Application.DependencyInjection;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using GastosApp.Application.Expenses.Queries.GetExpenses;
using GastosApp.Application.Members.Commands.InviteMember;
using GastosApp.Application.Members.Commands.UpdateMemberRole;
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
        services.AddSingleton(Substitute.For<ICategoryRepository>()); // dependência de Register/UpdateExpenseCommandValidator
        services.AddApplicationServices();
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(typeof(IValidator<CreateCategoryCommand>), typeof(CreateCategoryCommandValidator))]
    [InlineData(typeof(IValidator<UpdateCategoryCommand>), typeof(UpdateCategoryCommandValidator))]
    [InlineData(typeof(IValidator<RegisterExpenseCommand>), typeof(RegisterExpenseCommandValidator))]
    [InlineData(typeof(IValidator<UpdateExpenseCommand>), typeof(UpdateExpenseCommandValidator))]
    [InlineData(typeof(IValidator<GetExpensesQuery>), typeof(GetExpensesQueryValidator))]
    [InlineData(typeof(IValidator<InviteMemberCommand>), typeof(InviteMemberCommandValidator))]
    [InlineData(typeof(IValidator<UpdateMemberRoleCommand>), typeof(UpdateMemberRoleCommandValidator))]
    public void AddApplicationServices_ShouldRegisterEveryValidator_ExplicitlyNotByAssemblyScan(
        Type validatorInterface, Type expectedImplementation)
    {
        using var provider = BuildProvider();

        var resolved = provider.GetService(validatorInterface);

        resolved.Should().NotBeNull($"{validatorInterface.Name} precisa estar registrado em ApplicationServiceCollectionExtensions");
        resolved!.GetType().Should().Be(expectedImplementation);
    }

    [Fact]
    public void AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownSeven()
    {
        // Defensivo: se uma nova classe AbstractValidator<T> for criada no
        // projeto sem entrar na lista acima (e sem ser registrada em
        // ApplicationServiceCollectionExtensions), este teste de contagem
        // não pega isso sozinho — mas documenta a lista fechada esperada,
        // pra quem for adicionar um validator novo saber que precisa mexer
        // nos dois lugares (registro + este teste).
        var expectedValidatorCount = 7;

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
