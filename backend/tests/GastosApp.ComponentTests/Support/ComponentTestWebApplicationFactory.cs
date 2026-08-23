using GastosApp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace GastosApp.ComponentTests.Support;

public sealed class ComponentTestWebApplicationFactory : WebApplicationFactory<Program>
{
    public IAuthService AuthServiceMock { get; private set; } = Substitute.For<IAuthService>();
    public IExpenseRepository ExpenseRepositoryMock { get; private set; } = Substitute.For<IExpenseRepository>();
    public ICategoryRepository CategoryRepositoryMock { get; private set; } = Substitute.For<ICategoryRepository>();
    public IAccountRepository AccountRepositoryMock { get; private set; } = BuildDefaultAccountRepositoryMock();

    public void ResetAuthServiceMock() => AuthServiceMock = Substitute.For<IAuthService>();
    public void ResetExpenseRepositoryMock() => ExpenseRepositoryMock = Substitute.For<IExpenseRepository>();
    public void ResetCategoryRepositoryMock() => CategoryRepositoryMock = Substitute.For<ICategoryRepository>();
    public void ResetAccountRepositoryMock() => AccountRepositoryMock = BuildDefaultAccountRepositoryMock();

    // Default "esperto": resolve accountId = userId (mesmo valor) pra todo
    // usuário — ResolveAccountEndpointFilter passa a exigir essa resolução
    // em toda rota de /categories e /expenses (FEAT-19), e a maioria dos
    // testes já existentes usa o próprio userId autenticado como o valor
    // esperado de tenant nos mocks de Category/ExpenseRepository. Manter os
    // dois iguais por padrão evita reescrever esses testes um por um; testes
    // que precisam simular ausência de conta (401 account-not-found) ou
    // accountId diferente do userId sobrescrevem isso explicitamente.
    private static IAccountRepository BuildDefaultAccountRepositoryMock()
    {
        var mock = Substitute.For<IAccountRepository>();
        mock.FindAccountIdByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>());
        return mock;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cognito:Region"] = "us-east-1",
                ["Cognito:UserPoolId"] = "us-east-1_componentTests",
                ["Cognito:ClientId"] = "component-tests-client-id",
                ["DynamoDb:TableName"] = "GastosApp-componentTests",
                ["DynamoDb:Region"] = "us-east-1"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAuthService>();
            services.AddScoped(_ => AuthServiceMock);

            services.RemoveAll<IExpenseRepository>();
            services.AddScoped(_ => ExpenseRepositoryMock);

            services.RemoveAll<ICategoryRepository>();
            services.AddScoped(_ => CategoryRepositoryMock);

            services.RemoveAll<IAccountRepository>();
            services.AddScoped(_ => AccountRepositoryMock);

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.PostConfigure<AuthenticationOptions>(options =>
            {
                options.DefaultScheme = TestAuthHandler.SchemeName;
                options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
            });
        });
    }
}
