using FluentValidation;
using GastosApp.Application.Categories.Commands.CreateCategory;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Categories.Queries.GetCategories;
using GastosApp.Application.Common.Behaviors;
using GastosApp.Application.Members.Commands.InviteMember;
using GastosApp.Application.Members.Commands.UpdateMemberRole;
using GastosApp.Application.Summary.Queries.GetSummary;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Application.Transactions.Queries.GetTransactions;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace GastosApp.Application.DependencyInjection
{
    public static class ApplicationExtensions
    {
        public static IServiceCollection AddApplicationServices(
            this IServiceCollection services)
        {
            // Registro explícito, um por um — NÃO usar AddValidatorsFromAssembly().
            // Esse helper escaneia o assembly via reflection (Assembly.GetTypes()),
            // incompatível com trimming/Native AOT: o publish com PublishAot=true
            // já acusa "warning IL2104: Assembly 'FluentValidation' produced trim
            // warnings" para ele. Sob Native AOT (Lambda de hom/prod) o scan não
            // encontra os validators e falha silenciosamente — nenhuma exceção,
            // só ValidationBehavior vendo a lista de validators vazia e pulando
            // reto pro Handler. Mesma categoria de bug já resolvida antes pra
            // Configure<T>() sob AOT (ver backend/infra/CLAUDE.md) — a correção
            // aqui é a mesma: nunca reflection, sempre registro manual. Toda
            // classe nova de AbstractValidator<T> precisa ser adicionada aqui
            // também (nada mais descobre isso sozinho).
            services.AddScoped<IValidator<CreateCategoryCommand>, CreateCategoryCommandValidator>();
            services.AddScoped<IValidator<UpdateCategoryCommand>, UpdateCategoryCommandValidator>();
            services.AddScoped<IValidator<GetCategoriesQuery>, GetCategoriesQueryValidator>();
            services.AddScoped<IValidator<RegisterTransactionCommand>, RegisterTransactionCommandValidator>();
            services.AddScoped<IValidator<UpdateTransactionCommand>, UpdateTransactionCommandValidator>();
            services.AddScoped<IValidator<GetTransactionsQuery>, GetTransactionsQueryValidator>();
            services.AddScoped<IValidator<InviteMemberCommand>, InviteMemberCommandValidator>();
            services.AddScoped<IValidator<UpdateMemberRoleCommand>, UpdateMemberRoleCommandValidator>();
            services.AddScoped<IValidator<GetSummaryQuery>, GetSummaryQueryValidator>();

            services.AddMediator(options =>
            {
                options.ServiceLifetime = ServiceLifetime.Scoped;
                options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
            });

            return services;
        }
    }
}
