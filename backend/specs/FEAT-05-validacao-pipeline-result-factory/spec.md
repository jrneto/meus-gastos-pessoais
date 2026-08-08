# FEAT-05: Padronização de Validação (Pipeline Behavior) e Result (Factory Method)

## Objetivo
Padronizar dois aspectos internos da camada Application, hoje resolvidos
de forma ad hoc dentro de cada Handler, como convenção obrigatória para
todas as features (atuais e futuras):
1. Validação de entrada de Commands/Queries via Pipeline Behavior do
   Mediator + FluentValidation, executada antes do Handler.
2. Construção do valor de retorno (`Result<T>`) de cada Command/Query via
   factory method estático no próprio record de resultado, mapeando a
   partir da entidade de domínio.

## Contexto
Hoje (`RegisterUserCommandHandler`, `LoginUserCommandHandler`,
`RegisterExpenseCommandHandler`) cada Handler valida a entrada
manualmente com uma sequência de `if` no início do `Handle`, retornando
`Result.Failure` a cada regra violada, e constrói o record de retorno
campo a campo, inline. Funciona, mas mistura validação/mapeamento com
orquestração do caso de uso, e não escala: toda nova feature repete o
mesmo padrão sem nenhuma barreira que obrigue consistência.

## Regras / convenções (o que muda)

### Validação via Pipeline Behavior
- Toda validação de entrada de Command/Query passa a ocorrer antes do
  Handler ser executado, por um componente de pipeline do Mediator
  dedicado a validação — não mais dentro do `Handle`.
- A validação usa FluentValidation: cada Command/Query que precisa de
  validação ganha um Validator próprio, replicando as regras hoje
  hardcoded no Handler correspondente.
- Se a validação falhar, o Handler correspondente não é executado, e o
  chamador recebe a mesma falha que recebe hoje (`Result.Failure`/
  `Result<T>.Failure`, `ErrorType.Validation`, mesmo `Code`/mensagem já
  documentados na spec de cada feature) — nenhum contrato HTTP muda.
- Um Command/Query sem Validator registrado segue o fluxo normalmente.

### Result via factory method
- Todo record de retorno de Command/Query montado a partir de uma
  entidade de domínio passa a expor um factory method estático
  responsável por esse mapeamento, em vez de o Handler montar o record
  campo a campo.
- O Handler chama esse factory method para construir o valor de sucesso
  do `Result<T>`.

## Escopo da aplicação
- As duas convenções são aplicadas, nesta primeira etapa, apenas ao
  Command já implementado do módulo Expenses (`RegisterExpenseCommand`).
  Os Commands do módulo Auth (`RegisterUserCommand`, `LoginUserCommand`)
  **não** são migrados agora — ficam para uma decisão/feature futura (ver
  "Fora do escopo").
- Passam a ser documentadas como obrigatórias na documentação de
  convenções do backend, para que toda feature futura já nasça seguindo
  esse padrão — inclusive a futura migração dos Commands de Auth.
- Nenhum contrato HTTP observável externamente muda: mesmos paths,
  payloads, status codes e `type` de erro RFC 9457 já documentados em
  cada spec de feature (`FEAT-01-auth`, `FEAT-04-registro-despesa`).

## Cenários (Given/When/Then)

**C1 — Validação impede o Handler de rodar quando a entrada é inválida**
- Given um Command com um Validator registrado e dados que violam alguma regra
- When o Command é enviado ao Mediator
- Then a falha é retornada (`Result.Failure`/`Result<T>.Failure`,
  `ErrorType.Validation`) antes de o Handler ser chamado, preservando o
  mesmo `Code`/mensagem já documentados na spec da feature correspondente

**C2 — Command válido segue para o Handler normalmente**
- Given um Command com todos os campos válidos segundo seu Validator
- When o Command é enviado ao Mediator
- Then o Handler é executado normalmente, retornando `Result<T>.Success`
  com o valor construído via factory method

**C3 — Command sem Validator registrado não é bloqueado**
- Given um Command sem nenhum `IValidator<TCommand>` registrado
- When o Command é enviado ao Mediator
- Then a execução segue direto para o Handler, sem etapa de validação
  bloqueando

**C4 — Contrato HTTP não muda**
- Given o endpoint `/expenses` já existente
- When uma requisição inválida é enviada (mesmos casos já documentados em
  FEAT-04)
- Then a resposta HTTP (status code, corpo `ProblemDetails`, `type`) é
  idêntica à documentada antes desta mudança

## Critérios de aceite
- [x] FluentValidation referenciado no projeto Application
- [x] Existe um Pipeline Behavior de validação, executado antes de
      qualquer Handler
- [x] `RegisterExpenseCommand` tem um Validator dedicado, cobrindo as
      mesmas regras hoje hardcoded no `RegisterExpenseCommandHandler`
- [x] `RegisterExpenseCommandHandler` não contém mais validação manual —
      o `Handle` fica restrito à orquestração do caso de uso
- [x] `RegisterExpenseResult` é construído via factory method estático,
      não mais campo a campo dentro do Handler
- [x] Convenções documentadas como obrigatórias para novas features
      (incluindo a futura migração dos Commands de Auth)
- [x] Nenhum contrato HTTP muda: spec de `FEAT-04-registro-despesa`
      continua válida sem alteração
- [x] Suíte de testes existente continua passando; testes novos cobrem o
      Validator de `RegisterExpenseCommand` e o Pipeline Behavior de
      validação

## Status
Implementado. `IValidationFailureFactory<TSelf>` (static abstract
interface member, sem reflection) implementado por `Result`/`Result<T>`;
`ValidationBehavior<TMessage, TResponse>` registrado via
`options.PipelineBehaviors` no Mediator; `RegisterExpenseCommandValidator`
(FluentValidation) substitui a validação manual do
`RegisterExpenseCommandHandler`; `RegisterExpenseResult.FromExpense`
substitui a construção campo a campo. `ExpenseErrors.cs` removido (ficou
sem uso). Suíte completa (`dotnet test` na solução) passa: 67/67 (1
IntegrationTests placeholder + 19 ComponentTests, inalterados — sinal de
que o contrato HTTP de `POST /expenses` não mudou — + 47 UnitTests).

## Fora do escopo
- Mudanças na camada de Infrastructure/Persistence (DynamoDB, Cognito)
- Mudanças no formato RFC 9457 já adotado para erros HTTP
- Novas features ou novos Commands/Queries além do `RegisterExpenseCommand`
- Migração dos Commands do módulo Auth (`RegisterUserCommand`,
  `LoginUserCommand`) para os dois novos padrões — decisão/feature futura
- Migração de `IQuery`/`IQueryHandler` (ainda não há nenhuma Query
  implementada no projeto)
