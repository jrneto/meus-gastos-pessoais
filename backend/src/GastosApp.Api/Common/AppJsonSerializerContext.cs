using System.Text.Json.Serialization;
using GastosApp.Api.Endpoints;
using GastosApp.Application.Auth.Commands.Login;
using GastosApp.Application.Auth.Commands.Register;
using GastosApp.Application.Expenses.Commands.RegisterExpense;
using GastosApp.Application.Expenses.Commands.UpdateExpense;
using GastosApp.Application.Expenses.Queries.GetExpenses;
using GastosApp.Application.Health;

namespace GastosApp.Api.Common;

// Contexto de serialização gerado em tempo de compilação — obrigatório
// em Native AOT, já que o serializador de System.Text.Json baseado em
// reflection lança NotSupportedException em runtime para tipos não
// conhecidos previamente (achado durante a implementação da FEAT-10,
// só reproduzido rodando de fato na Lambda).
[JsonSerializable(typeof(RegisterRequest))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(RegisterExpenseRequest))]
[JsonSerializable(typeof(UpdateExpenseRequest))]
[JsonSerializable(typeof(RegisterUserResult))]
[JsonSerializable(typeof(LoginUserResult))]
[JsonSerializable(typeof(RegisterExpenseResult))]
[JsonSerializable(typeof(UpdateExpenseResult))]
[JsonSerializable(typeof(GetExpensesResult))]
[JsonSerializable(typeof(ExpenseSummary))]
[JsonSerializable(typeof(UserInfoResponse))]
[JsonSerializable(typeof(GetExpensesRequest))]
[JsonSerializable(typeof(HealthResponse))]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
