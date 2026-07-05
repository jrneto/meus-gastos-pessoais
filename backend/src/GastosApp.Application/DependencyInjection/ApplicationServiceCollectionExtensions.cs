using GastosApp.Application.Abstractions;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Register;
using Microsoft.Extensions.DependencyInjection;

namespace GastosApp.Application.DependencyInjection
{
    public static class ApplicationExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services)
        {
            services.AddScoped<
                ICommandHandler<RegisterUserCommand, RegisterUserResult>,
                RegisterUserCommandHandler>();

            services.AddScoped<
                ICommandHandler<LoginUserCommand, LoginUserResult>,
                LoginUserCommandHandler>();

            return services;
        }
    }
}
