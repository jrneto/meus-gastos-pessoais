using System.Text.Json.Serialization;
using GastosApp.Api.Endpoints;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Refresh;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Categories.Commands.CreateCategory;
using GastosApp.Application.Categories.Commands.UpdateCategory;
using GastosApp.Application.Categories.Queries.GetCategories;
using GastosApp.Application.Health;
using GastosApp.Application.Members;
using GastosApp.Application.Reports.Queries.GetReports;
using GastosApp.Application.Summary.Queries.GetSummary;
using GastosApp.Application.Transactions.Commands.RegisterTransaction;
using GastosApp.Application.Transactions.Commands.UpdateTransaction;
using GastosApp.Application.Transactions.Queries.GetTransactions;
using Microsoft.AspNetCore.Mvc;

namespace GastosApp.Api.Common;

// Contexto de serialização gerado em tempo de compilação — obrigatório
// em Native AOT, já que o serializador de System.Text.Json baseado em
// reflection lança NotSupportedException em runtime para tipos não
// conhecidos previamente (achado durante a implementação da FEAT-10,
// só reproduzido rodando de fato na Lambda).
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(RegisterTransactionRequest))]
[JsonSerializable(typeof(UpdateTransactionRequest))]
[JsonSerializable(typeof(RegisterUserResult))]
[JsonSerializable(typeof(LoginUserResult))]
[JsonSerializable(typeof(RefreshTokenResult))]
[JsonSerializable(typeof(RegisterTransactionResult))]
[JsonSerializable(typeof(UpdateTransactionResult))]
[JsonSerializable(typeof(GetTransactionsResult))]
[JsonSerializable(typeof(TransactionSummary))]
[JsonSerializable(typeof(UserInfoResponse))]
[JsonSerializable(typeof(GetTransactionsRequest))]
[JsonSerializable(typeof(HealthResponse))]
[JsonSerializable(typeof(ProblemDetails))]
[JsonSerializable(typeof(CreateCategoryRequest))]
[JsonSerializable(typeof(UpdateCategoryRequest))]
[JsonSerializable(typeof(CreateCategoryResult))]
[JsonSerializable(typeof(UpdateCategoryResult))]
[JsonSerializable(typeof(GetCategoriesResult))]
[JsonSerializable(typeof(CategorySummary))]
[JsonSerializable(typeof(InviteMemberRequest))]
[JsonSerializable(typeof(UpdateMemberRoleRequest))]
[JsonSerializable(typeof(MemberResult))]
[JsonSerializable(typeof(GetMembersResult))]
[JsonSerializable(typeof(GetSummaryResult))]
[JsonSerializable(typeof(CategorySummaryItem))]
[JsonSerializable(typeof(GetSummaryRequest))]
[JsonSerializable(typeof(GetReportsResult))]
[JsonSerializable(typeof(ReportCategoryItem))]
[JsonSerializable(typeof(ReportTopCategory))]
[JsonSerializable(typeof(GetReportsRequest))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
