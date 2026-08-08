# FEAT-05: Validação via Pipeline Behavior + Result via Factory Method — Tasks

## Dependências (pacotes)

- [x] 1. Adicionar `FluentValidation` (12.1.1) e `FluentValidation.DependencyInjectionExtensions` ao `GastosApp.Application.csproj`

## Result Pattern: base para construção de falha sem reflection

- [x] 2. Criar `GastosApp.Application/Common/Results/IValidationFailureFactory.cs` (interface CRTP com `static abstract TSelf ValidationFailure(Error error)`)
- [x] 3. Alterar `GastosApp.Application/Common/Results/Result.cs`: `Result` implementa `IValidationFailureFactory<Result>` (implementação explícita, delega para `Failure`)
- [x] 4. Alterar `GastosApp.Application/Common/Results/Result.cs`: `Result<T>` implementa `IValidationFailureFactory<Result<T>>` (implementação explícita, delega para `Failure<T>`)

## Pipeline Behavior de validação

- [x] 5. Criar `GastosApp.Application/Common/Behaviors/ValidationBehavior.cs` (`ValidationBehavior<TMessage, TResponse>`, constraint `where TResponse : Result, IValidationFailureFactory<TResponse>`, fail-fast na primeira `ValidationFailure`, sem validators registrados → segue para `next`)
- [x] 6. Registrar `AddValidatorsFromAssembly` e `options.PipelineBehaviors = [typeof(ValidationBehavior<,>)]` em `ApplicationServiceCollectionExtensions.AddApplicationServices`

## Migração de RegisterExpense

- [x] 7. Criar `GastosApp.Application/Expenses/Commands/RegisterExpense/RegisterExpenseCommandValidator.cs` (regras: descrição obrigatória/máx. 200 chars, valor > 0, categoria válida; `ClassLevelCascadeMode = CascadeMode.Stop`)
- [x] 8. Adicionar `RegisterExpenseResult.FromExpense(Expense expense)` (factory method estático) em `RegisterExpenseCommand.cs`
- [x] 9. Remover a validação manual (`if`s) de `RegisterExpenseCommandHandler.Handle`, trocar `Enum.TryParse` por `Enum.Parse` (categoria já validada pelo pipeline) e usar `RegisterExpenseResult.FromExpense(expense)` no retorno

## Documentação da convenção

- [x] 10. Atualizar `backend/docs/constitution.md` (seção "Padrões de Código e Arquitetura"): adicionar as duas novas regras obrigatórias (Validação via Pipeline Behavior + FluentValidation; Result via factory method)

## Testes unitários (`GastosApp.UnitTests`)

- [x] 11. Criar `RegisterExpenseCommandValidatorTests`: caso válido (`IsValid` true) + um caso por regra violada (descrição vazia/ausente, descrição > 200 chars, valor <= 0, categoria inválida)
- [x] 12. Criar `ValidationBehaviorTests`: validador configurado para falhar → `next` não é chamado, retorna `Result<T>.Failure` (`ErrorType.Validation`, código `validation-error`)
- [x] 13. `ValidationBehaviorTests`: validador configurado para passar → `next` é chamado e seu retorno é propagado
- [x] 14. `ValidationBehaviorTests`: mensagem sem nenhum `IValidator` registrado (ex.: `LoginUserCommand`) → segue direto para `next`, sem bloquear
- [x] 15. Atualizar `RegisterExpenseCommandHandlerTests`: remover os testes que chamam `Handle` diretamente esperando falha de validação (`Handle_ShouldReturnValidationFailure_WhenCommandIsInvalid`, `Handle_ShouldReturnValidationFailure_WhenDescriptionExceedsMaxLength`); manter os testes de sucesso e de data retroativa/futura, ajustando-os se necessário para o novo `RegisterExpenseResult.FromExpense`
- [x] 16. Rodar `ResultTests` para confirmar que a mudança em `Result.cs` não quebrou nenhum membro público existente

## Fechamento

- [x] 17. Rodar `dotnet test` na solução completa (UnitTests + ComponentTests + IntegrationTests) e confirmar que `ExpenseEndpointsTests` continua passando sem nenhuma alteração — sinal de que o contrato HTTP de `POST /expenses` não mudou
- [x] 18. Atualizar `backend/specs/FEAT-05-validacao-pipeline-result-factory/spec.md`: marcar os itens da seção "Critérios de aceite" como concluídos (`- [x]`) e adicionar seção "Status" resumindo a implementação (seguindo o padrão de `FEAT-02`/`FEAT-04`)
