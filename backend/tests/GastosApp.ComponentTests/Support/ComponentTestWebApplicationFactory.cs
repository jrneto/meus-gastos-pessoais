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

    public void ResetAuthServiceMock() => AuthServiceMock = Substitute.For<IAuthService>();
    public void ResetExpenseRepositoryMock() => ExpenseRepositoryMock = Substitute.For<IExpenseRepository>();

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
