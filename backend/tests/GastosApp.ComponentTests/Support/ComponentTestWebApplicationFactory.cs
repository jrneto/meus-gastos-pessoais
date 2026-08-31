using GastosApp.Application.Common.Interfaces;
using GastosApp.Domain.Accounts;
using GastosApp.Domain.Users;
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
    public ITransactionRepository TransactionRepositoryMock { get; private set; } = Substitute.For<ITransactionRepository>();
    public ICategoryRepository CategoryRepositoryMock { get; private set; } = Substitute.For<ICategoryRepository>();
    public IAccountRepository AccountRepositoryMock { get; private set; } = BuildDefaultAccountRepositoryMock();
    public IMembershipRepository MembershipRepositoryMock { get; private set; } = BuildDefaultMembershipRepositoryMock();
    public IUserProfileRepository UserProfileRepositoryMock { get; private set; } = BuildDefaultUserProfileRepositoryMock();

    public void ResetAuthServiceMock() => AuthServiceMock = Substitute.For<IAuthService>();
    public void ResetTransactionRepositoryMock() => TransactionRepositoryMock = Substitute.For<ITransactionRepository>();
    public void ResetCategoryRepositoryMock() => CategoryRepositoryMock = Substitute.For<ICategoryRepository>();
    public void ResetAccountRepositoryMock() => AccountRepositoryMock = BuildDefaultAccountRepositoryMock();
    public void ResetMembershipRepositoryMock() => MembershipRepositoryMock = BuildDefaultMembershipRepositoryMock();
    public void ResetUserProfileRepositoryMock() => UserProfileRepositoryMock = BuildDefaultUserProfileRepositoryMock();

    // Default "esperto": resolve accountId = userId (mesmo valor) pra todo
    // usuário — ResolveAccountEndpointFilter passa a exigir essa resolução
    // em toda rota de /categories e /transactions (FEAT-19), e a maioria dos
    // testes já existentes usa o próprio userId autenticado como o valor
    // esperado de tenant nos mocks de Category/TransactionRepository. Manter
    // os dois iguais por padrão evita reescrever esses testes um por um;
    // testes que precisam simular ausência de conta (401 account-not-found)
    // ou accountId diferente do userId sobrescrevem isso explicitamente.
    private static IAccountRepository BuildDefaultAccountRepositoryMock()
    {
        var mock = Substitute.For<IAccountRepository>();
        mock.FindAccountIdByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<string>());
        return mock;
    }

    // Default "esperto" (mesmo espírito do AccountRepositoryMock acima, FEAT-19):
    // resolve o Membership do chamador como Titular na conta default, pra
    // ResolveMembershipQuery (e os filtros de papel, FEAT-20) não quebrarem os
    // testes de Category/Transaction já existentes, que não conhecem papel algum.
    // Testes que precisam simular Leitura/Lancar/Total sobrescrevem isso
    // explicitamente.
    private static IMembershipRepository BuildDefaultMembershipRepositoryMock()
    {
        var mock = Substitute.For<IMembershipRepository>();
        mock.FindByAccountAndUserIdAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Membership.CreateTitular(call.ArgAt<string>(0), call.ArgAt<string>(1), "titular@email.com"));
        return mock;
    }

    // Default "esperto" (mesmo espírito do AccountRepositoryMock/MembershipRepositoryMock
    // acima, FEAT-26): CreateAsync sempre "sucesso, sem CPF duplicado" — testes que
    // precisam simular CPF já cadastrado sobrescrevem isso explicitamente.
    // FindByUserIdAsync sempre retorna um UserProfile completo por padrão (FEAT-31:
    // POST /auth/login passou a bloquear o login quando o perfil não existe) — testes
    // que precisam simular perfil ausente (ex.: usuário criado direto no Cognito,
    // ou usuário anterior à FEAT-26) sobrescrevem isso explicitamente para null.
    private static IUserProfileRepository BuildDefaultUserProfileRepositoryMock()
    {
        var mock = Substitute.For<IUserProfileRepository>();
        mock.CreateAsync(Arg.Any<UserProfile>(), Arg.Any<CancellationToken>())
            .Returns(new CreateUserProfileResult(CpfAlreadyExists: false));
        mock.FindByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => UserProfile.Restore(call.Arg<string>(), "Fulano da Silva", "11999998888", "11144477735", DateTimeOffset.UtcNow));
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

            services.RemoveAll<ITransactionRepository>();
            services.AddScoped(_ => TransactionRepositoryMock);

            services.RemoveAll<ICategoryRepository>();
            services.AddScoped(_ => CategoryRepositoryMock);

            services.RemoveAll<IAccountRepository>();
            services.AddScoped(_ => AccountRepositoryMock);

            services.RemoveAll<IMembershipRepository>();
            services.AddScoped(_ => MembershipRepositoryMock);

            services.RemoveAll<IUserProfileRepository>();
            services.AddScoped(_ => UserProfileRepositoryMock);

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
