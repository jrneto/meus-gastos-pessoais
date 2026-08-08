using FluentValidation;
using GastosApp.Application.Common.Behaviors;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace GastosApp.Application.DependencyInjection
{
    public static class ApplicationExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);

            services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
            });

            return services;
        }
    }
}
