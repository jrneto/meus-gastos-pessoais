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
            services.AddScoped<RegisterUserCommandHandler>();
            services.AddScoped<LoginUserCommandHandler>();

            return services;
        }
    }
}
