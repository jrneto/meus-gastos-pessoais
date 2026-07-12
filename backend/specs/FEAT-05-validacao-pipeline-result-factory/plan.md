# FEAT-05: Padronização de Validação (Pipeline Behavior) e Result (Factory Method) — Plano Técnico

## Escopo confirmado
Aplicado apenas a `RegisterExpenseCommand`/`RegisterExpenseCommandHandler`/
`RegisterExpenseResult` (módulo Expenses). Os Commands de Auth
(`RegisterUserCommand`, `LoginUserCommand`) não são tocados nesta feature.

## Camadas afetadas

Só **Application**. Nenhuma mudança em Domain, Infrastructure ou Api —
`ExpenseEndpoints.cs` continua chamando `sender.Send(command, ct)` e
mapeando o `Result` do mesmo jeito; o pipeline de validação é transparente
para quem chama o Mediator.

### Application (`GastosApp.Application`)

**Novo: `Common/Results/IValidationFailureFactory.cs`**
```csharp
namespace GastosApp.Application.Common.Results;

public interface IValidationFailureFactory<TSelf> where TSelf : IValidationFailureFactory<TSelf>
{
    static abstract TSelf ValidationFailure(Error error);
}
```
Interface CRTP (self-referencing) que permite ao `ValidationBehavior`
construir a falha no tipo concreto de retorno (`Result` ou `Result<T>`)
inteiramente resolvido em tempo de compilação via generic constraint —
sem reflection, compatível com Native AOT trimming.

**Alterado: `Common/Results/Result.cs`** — `Result` e `Result<T>` passam a
implementar a interface acima (implementação explícita, para não colidir
com os métodos públicos já existentes `Failure`/`Failure<T>`); nenhum
membro público existente muda:
```csharp
public class Result : IValidationFailureFactory<Result>
{
    // ... membros existentes inalterados ...
    static Result IValidationFailureFactory<Result>.ValidationFailure(Error error) => Failure(error);
}

public sealed class Result<T> : Result, IValidationFailureFactory<Result<T>>
{
    // ... membros existentes inalterados ...
    static Result<T> IValidationFailureFactory<Result<T>>.ValidationFailure(Error error) => Failure<T>(error);
}
```

**Novo: `Common/Behaviors/ValidationBehavior.cs`**
```csharp
namespace GastosApp.Application.Common.Behaviors;

public sealed class ValidationBehavior<TMessage, TResponse> : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
    where TResponse : Result, IValidationFailureFactory<TResponse>
{
    private readonly IEnumerable<IValidator<TMessage>> _validators;

    public ValidationBehavior(IEnumerable<IValidator<TMessage>> validators) => _validators = validators;

    public async ValueTask<TResponse> Handle(
        TMessage message, MessageHandlerDelegate<TMessage, TResponse> next, CancellationToken cancellationToken)
    {
        if (!_validators.Any())
            return await next(message, cancellationToken);

        var context = new ValidationContext<TMessage>(message);
        ValidationFailure? firstFailure = null;
        foreach (var validator in _validators)
        {
            var result = await validator.ValidateAsync(context, cancellationToken);
            if (!result.IsValid)
            {
                firstFailure = result.Errors[0];
                break;
            }
        }

        if (firstFailure is null)
            return await next(message, cancellationToken);

        return TResponse.ValidationFailure(Error.Validation("validation-error", firstFailure.ErrorMessage));
    }
}
```
- Constraint `where TResponse : Result, IValidationFailureFactory<TResponse>`
  cobre tanto `Result` quanto `Result<T>` (que herda de `Result`, ver
  `Result.cs`), então um único behavior serve para todos os
  Commands/Queries que já seguem o Result Pattern do projeto.
- `TResponse.ValidationFailure(error)` é uma chamada de static abstract
  interface member (C# 11/.NET 7+), resolvida em tempo de compilação —
  sem `MakeGenericMethod`, sem `Invoke`, sem reflection em runtime.
- Fail-fast: para no primeiro `IValidator<TMessage>` que falhar,
  reportando só a primeira `ValidationFailure` (mesmo comportamento hoje
  produzido pelo `if` sequencial no Handler — só um erro por vez).
- **Código do erro**: fixo em `"validation-error"` dentro do behavior —
  mesmo slug já usado por `ExpenseErrors.Validation`, então o contrato de
  `POST /expenses` não muda. Passa a ser a convenção padrão do projeto
  para toda validação orientada a pipeline (Auth usa `"bad-request"` hoje,
  mas Auth não é migrado nesta feature — quando for, deve adotar
  `"validation-error"` também, unificando o slug).

**Novo: `Expenses/Commands/RegisterExpense/RegisterExpenseCommandValidator.cs`**
```csharp
public sealed class RegisterExpenseCommandValidator : AbstractValidator<RegisterExpenseCommand>
{
    private const int MaxDescriptionLength = 200;

    public RegisterExpenseCommandValidator()
    {
        ClassLevelCascadeMode = CascadeMode.Stop; // preserva o comportamento fail-fast atual

        RuleFor(c => c.Description)
            .NotEmpty().WithMessage("Descrição é obrigatória.")
            .MaximumLength(MaxDescriptionLength).WithMessage($"Descrição deve ter no máximo {MaxDescriptionLength} caracteres.");

        RuleFor(c => c.AmountInCents)
            .GreaterThan(0).WithMessage("Valor deve ser maior que zero.");

        RuleFor(c => c.Category)
            .Must(BeAValidCategory).WithMessage("Categoria inválida.");
    }

    private static bool BeAValidCategory(string category) =>
        Enum.TryParse<ExpenseCategory>(category, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed);
}
```
Regras idênticas às hoje hardcoded no Handler (ver `backend/specs/FEAT-04-registro-despesa/plan.md`).

**Alterado: `Expenses/Commands/RegisterExpense/RegisterExpenseCommand.cs`**
- `RegisterExpenseResult` ganha factory method:
  ```csharp
  public record RegisterExpenseResult(
      string Id, string Description, long AmountInCents,
      string Category, DateOnly ExpenseDate, DateTimeOffset CreatedAt)
  {
      public static RegisterExpenseResult FromExpense(Expense expense) => new(
          expense.Id, expense.Description, expense.AmountInCents,
          expense.Category.ToString(), expense.ExpenseDate, expense.CreatedAt);
  }
  ```
- `RegisterExpenseCommandHandler.Handle` perde toda a validação manual
  (os 4 `if`) e passa a assumir que `command` já é válido (garantido pelo
  `ValidationBehavior`, que roda antes). Como a categoria já foi validada
  pelo Validator, o parse pode usar `Enum.Parse` (não mais `TryParse`)
  sem checagem adicional:
  ```csharp
  public async ValueTask<Result<RegisterExpenseResult>> Handle(RegisterExpenseCommand command, CancellationToken cancellationToken)
  {
      var category = Enum.Parse<ExpenseCategory>(command.Category, ignoreCase: true);
      var expense = Expense.Create(command.UserId, command.Description, command.AmountInCents, category, command.ExpenseDate);

      await _expenseRepository.SaveAsync(expense, cancellationToken);

      return Result.Success(RegisterExpenseResult.FromExpense(expense));
  }
  ```

**Alterado: `DependencyInjection/ApplicationServiceCollectionExtensions.cs`**
```csharp
public static IServiceCollection AddApplicationServices(this IServiceCollection services)
{
    services.AddValidatorsFromAssembly(typeof(ApplicationExtensions).Assembly);

    services.AddMediator(options =>
    {
        options.ServiceLifetime = ServiceLifetime.Scoped;
        options.PipelineBehaviors = [typeof(ValidationBehavior<,>)];
    });

    return services;
}
```
`options.PipelineBehaviors` é a forma recomendada pela lib `Mediator`
para registrar pipeline behaviors compatível com o source generator (ver
README do pacote, seção 3.3/3.4) — evita registro via DI de tipo genérico
aberto, que a lib desaconselha para cenários AOT.

**`GastosApp.Application.csproj`**: adicionar
- `FluentValidation` (12.1.1 — já em cache local, compatível com `net10.0`)
- `FluentValidation.DependencyInjectionExtensions` (mesma versão major,
  fornece `AddValidatorsFromAssembly`)

## Documentação da convenção

Adicionar em `backend/docs/constitution.md`, seção "Padrões de Código e
Arquitetura" (mesmo lugar onde já estão as regras de Mediator e Result
Pattern), duas novas regras:
- **Validação (Pipeline Behavior + FluentValidation)**: toda validação de
  entrada de Command/Query é feita via `IValidator<TCommand>`
  (FluentValidation), executado automaticamente pelo `ValidationBehavior`
  do pipeline do Mediator antes do Handler. Handlers não devem conter
  validação manual (`if`) de entrada.
- **Result via factory method**: todo record de retorno de Command/Query
  construído a partir de uma entidade de domínio expõe um factory method
  estático (ex.: `FromEntity`) responsável pelo mapeamento; o Handler
  chama esse factory method em vez de montar o record campo a campo.

## Recursos AWS afetados
Nenhum. Mudança inteiramente interna à camada Application — não toca
DynamoDB, Cognito nem Parameter Store.

## Mapeamento de erros → `ErrorType` → HTTP

Sem mudança em relação ao já documentado em
`backend/specs/FEAT-04-registro-despesa/plan.md`: validação continua
`ErrorType.Validation` → 400 → `type` = `https://gastosapp.dev/errors/validation-error`.
Só muda **onde** o erro é produzido (pipeline em vez do Handler).

## Testes

### Novos
- `RegisterExpenseCommandValidatorTests` (`GastosApp.UnitTests`): um caso
  válido (`IsValid` true) + um caso por regra violada (descrição
  vazia/ausente, descrição > 200 chars, valor <= 0, categoria inválida),
  espelhando os casos hoje cobertos em `RegisterExpenseCommandHandlerTests`.
- `ValidationBehaviorTests` (`GastosApp.UnitTests`): usando um
  `IValidator<TMessage>` fake (NSubstitute) e uma mensagem/response de
  teste (ou reaproveitando `RegisterExpenseCommand`/`Result<RegisterExpenseResult>`):
  - validador configurado para falhar → `next` nunca é chamado, retorno é
    `Result<T>.Failure` com `ErrorType.Validation` e código
    `validation-error`.
  - validador configurado para passar → `next` é chamado e seu retorno é
    propagado.
  - nenhum validador registrado para o tipo de mensagem → `next` é
    chamado diretamente (usar `LoginUserCommand`, que hoje não tem
    nenhum `IValidator` registrado, como exemplo real do cenário C3 da
    spec — sem precisar de nenhuma mudança em Auth).

### Alterados
- `RegisterExpenseCommandHandlerTests`: os testes que hoje chamam
  `_handler.Handle(...)` diretamente esperando `Result.Failure` de
  validação (`Handle_ShouldReturnValidationFailure_WhenCommandIsInvalid`,
  `Handle_ShouldReturnValidationFailure_WhenDescriptionExceedsMaxLength`)
  deixam de fazer sentido — o Handler não valida mais nada quando chamado
  isoladamente (a validação só acontece no pipeline do Mediator, que
  esses testes não exercitam). Essa cobertura passa a viver em
  `RegisterExpenseCommandValidatorTests` (nível de Validator) e nos
  testes de componente existentes de `POST /expenses`
  (`ExpenseEndpointsTests`, que já passam pelo Mediator completo — nenhuma
  mudança neles é esperada, e continuar verdes é o principal sinal de que
  o contrato HTTP não mudou).
  Mantidos como estão: os testes de sucesso e o teste de datas
  retroativa/futura, já que continuam testando o Handler com um `command`
  válido.

### Inalterados (sinal de regressão)
- `ExpenseEndpointsTests` (`GastosApp.ComponentTests`): não deveria
  precisar de nenhuma alteração — validam o fluxo HTTP completo
  (`POST /expenses` → Mediator → pipeline → Handler), que é justamente o
  contrato que não pode mudar. Rodar a suíte completa ao final é a
  verificação principal desta feature.

## Decisões confirmadas
- `ValidationBehavior` usa static abstract interface member
  (`IValidationFailureFactory<TSelf>`), sem reflection — ver seção
  "Application" acima.
- `"validation-error"` fixo no `ValidationBehavior` é a nova convenção
  padrão do projeto para toda validação via pipeline (Auth, quando
  migrado futuramente, também deve adotar esse slug em vez de
  `"bad-request"`).

## Impacto em `Result.cs` para testes existentes
- `Result`/`Result<T>` ganham implementação explícita de
  `IValidationFailureFactory<TSelf>` — não altera nenhum membro público
  existente (`Success`, `Failure`, `IsSuccess`, `Value`, etc.), então
  `ResultTests` (`GastosApp.UnitTests`) não deveria precisar de nenhuma
  alteração. Vale rodar essa suíte específica após a mudança como
  verificação rápida de que nada quebrou em `Result.cs`.
