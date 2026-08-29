# Tasks — FEAT-21: Categoria — tipo, orçamento e remoção de cor/ícone

Ordem pensada pra manter dependência antes de dependente (Domain →
Application → Infrastructure → Api → testes). Cada item é do tamanho
de um commit. Feature é uma extensão do CRUD já existente (FEAT-16) —
a maioria das tarefas é "atualizar", não "criar".

## Domain

- [x] 1. Atualizar `Category` (`backend/src/GastosApp.Domain/Categories/Category.cs`):
      remover `Cor`/`Icone`; adicionar `Tipo` (`string`) e
      `OrcamentoMensalCents` (`long?`); `Create`/`Restore` passam a
      receber `tipo`/`orcamentoMensalCents` no lugar de `cor`/`icone`.

## Application — contrato de repositório

- [x] 2. Atualizar `ICategoryRepository`
      (`backend/src/GastosApp.Application/Common/Interfaces/ICategoryRepository.cs`):
      `ListAsync` ganha parâmetro `string? tipo`; `UpdateAsync` troca
      `cor`/`icone` por `tipo`/`orcamentoMensalCents`.

## Application — Commands/Queries

- [x] 3. Atualizar `CreateCategoryCommand`+`CreateCategoryCommandHandler`+`CreateCategoryResult`
      (`.../CreateCategory/CreateCategoryCommand.cs`): trocar
      `Cor`/`Icone` por `Tipo`/`OrcamentoMensalCents` no Command, no
      `Category.Create(...)` chamado pelo Handler, e no `Result`/
      `FromEntity`.
- [x] 4. Atualizar `CreateCategoryCommandValidator`
      (`.../CreateCategory/CreateCategoryCommandValidator.cs`):
      remover as regras de `Cor` (regex `#RRGGBB`) e `Icone` por
      completo; manter as regras de `Nome`; adicionar `Tipo`
      (`NotEmpty` + `Must(t => t is "despesa" or "receita")`) e
      `OrcamentoMensalCents` (`GreaterThan(0).When(informado)`).
- [x] 5. Atualizar `UpdateCategoryCommand`+`UpdateCategoryCommandHandler`+`UpdateCategoryResult`
      (`.../UpdateCategory/UpdateCategoryCommand.cs`): mesma troca de
      `Cor`/`Icone` por `Tipo`/`OrcamentoMensalCents` no Command,
      repasse pro `_categoryRepository.UpdateAsync(...)`, e no
      `Result`/`FromEntity`.
- [x] 6. Atualizar `UpdateCategoryCommandValidator`
      (`.../UpdateCategory/UpdateCategoryCommandValidator.cs`): mesmas
      regras novas de `CreateCategoryCommandValidator` (`Nome`
      inalterado, `Cor`/`Icone` removidos, `Tipo`/`OrcamentoMensalCents`
      adicionados).
- [x] 7. Atualizar `GetCategoriesQuery`+`GetCategoriesQueryHandler`+`GetCategoriesResult`+`CategorySummary`
      (`backend/src/GastosApp.Application/Categories/Queries/GetCategories/GetCategoriesQuery.cs`):
      `GetCategoriesQuery` ganha `string? Tipo`, repassado pro
      `_categoryRepository.ListAsync(accountId, tipo, ct)`;
      `CategorySummary` troca `Cor`/`Icone` por `Tipo`/
      `OrcamentoMensalCents`.
- [x] 8. Criar `GetCategoriesQueryValidator`
      (`.../GetCategories/GetCategoriesQueryValidator.cs`, novo,
      mirror de `GetExpensesQueryValidator`): `Tipo` válido só se
      `null`, `"despesa"` ou `"receita"`.
- [x] 9. Registrar `services.AddScoped<IValidator<GetCategoriesQuery>, GetCategoriesQueryValidator>();`
      em `ApplicationServiceCollectionExtensions.AddApplicationServices`
      (`backend/src/GastosApp.Application/DependencyInjection/ApplicationServiceCollectionExtensions.cs`).

## Infrastructure

- [x] 10. Atualizar `DynamoDbCategoryRepository.BuildItem`
      (`backend/src/GastosApp.Infrastructure/Categories/DynamoDbCategoryRepository.cs`):
      parar de gravar `Cor`/`Icone`; gravar novo atributo
      `TipoLancamento` (sempre, valor de `category.Tipo` — distinto do
      discriminador `Tipo` já existente, que continua `"categoria"`);
      gravar `OrcamentoMensalCents` só quando `category.OrcamentoMensalCents`
      não for `null` (atributo `N`, omitido caso contrário).
- [x] 11. Atualizar `DynamoDbCategoryRepository.MapToCategory`: parar
      de ler `Cor`/`Icone`; ler `TipoLancamento` com `TryGetValue`,
      default `"despesa"` quando ausente (categoria gravada antes
      desta feature); ler `OrcamentoMensalCents` com `TryGetValue`,
      `null` quando ausente.
- [x] 12. Atualizar `DynamoDbCategoryRepository.ListAsync`: assinatura
      ganha `string? tipo`; `Query` por `PK`+`begins_with(SK, "CAT#")`
      inalterada, mapeamento via `MapToCategory`, filtro
      `.Where(c => c.Tipo == tipo)` aplicado em memória só quando
      `tipo` não for `null` (depois do mapeamento, pra respeitar o
      default do passo 11).
- [x] 13. Atualizar `DynamoDbCategoryRepository.UpdateAsync`:
      assinatura ganha `tipo`/`orcamentoMensalCents` no lugar de
      `cor`/`icone`, repassados pro `Category.Restore(...)`/
      `BuildItem(...)` já existentes — lógica de slug/rename/
      `TransactWriteItems` permanece igual.

## Api

- [x] 14. Atualizar `CategoryEndpoints.cs`
      (`backend/src/GastosApp.Api/Endpoints/CategoryEndpoints.cs`):
      `CreateCategoryRequest`/`UpdateCategoryRequest` trocam
      `Cor`/`Icone` por `Tipo`/`OrcamentoMensalCents`; handlers
      `CreateCategory`/`UpdateCategory` repassam os novos campos pro
      Command.
- [x] 15. Atualizar `CategoryEndpoints.GetCategories`: trocar a
      assinatura pra `[AsParameters] GetCategoriesRequest request`
      (novo record `GetCategoriesRequest(string Tipo = "")`), montar
      `GetCategoriesQuery` com `NullIfEmpty(request.Tipo)` (novo
      helper local, mirror do já existente em `ExpenseEndpoints.cs`),
      e adicionar `.ProducesProblem(StatusCodes.Status400BadRequest)`
      no `MapGet("/")`.
- [x] 16. Rodar `dotnet build backend/GastosApp.sln` e corrigir todos
      os erros de compilação (Domain/Application/Infrastructure/Api)
      antes de seguir para os testes.

## Testes unitários (`backend/tests/GastosApp.UnitTests/`)

- [x] 17. Atualizar `Domain/CategoryTests.cs`: remover asserções de
      `Cor`/`Icone`; `Create`/`Restore` cobrindo `tipo` e
      `orcamentoMensalCents` (incluindo `null`).
- [x] 18. Atualizar `Application/CreateCategoryCommandValidatorTests.cs`:
      remover casos de `Cor`/`Icone`; novos casos de `Tipo`
      (ausente/vazio/fora de `despesa`\|`receita` → inválido; válido
      nos dois valores) e `OrcamentoMensalCents` (`null` → válido;
      `0`/negativo → inválido; positivo → válido).
- [x] 19. Atualizar `Application/UpdateCategoryCommandValidatorTests.cs`:
      mesmos casos novos de `CreateCategoryCommandValidatorTests`.
- [x] 20. Criar `Application/GetCategoriesQueryValidatorTests.cs`:
      `Tipo` `null`/`"despesa"`/`"receita"` → válido; qualquer outro
      valor → inválido.
- [x] 21. Atualizar `Application/CreateCategoryCommandHandlerTests.cs`:
      ajustar construção do Command/mock pros novos parâmetros (sem
      `cor`/`icone`); outcomes inalterados (`Success`/`NameConflict`).
- [x] 22. Atualizar `Application/UpdateCategoryCommandHandlerTests.cs`:
      mesma atualização de parâmetros; outcomes inalterados
      (`Success`/`NotFound`/`NameConflict`).
- [x] 23. Atualizar `Application/GetCategoriesQueryHandlerTests.cs`:
      novo caso repassando `Tipo` pro `ICategoryRepository.ListAsync`
      mockado e conferindo o argumento recebido.
- [x] 24. Atualizar `Infrastructure/DynamoDbCategoryRepositoryTests.cs`:
      remover asserções de `Cor`/`Icone` em `BuildItem`/`MapToCategory`;
      novos casos — `BuildItem` grava `TipoLancamento` sempre e
      `OrcamentoMensalCents` só quando informado; `MapToCategory`
      default `"despesa"` quando `TipoLancamento` ausente do item
      (simulando categoria antiga) e ignora `Cor`/`Icone` caso ainda
      estejam presentes; `ListAsync` com `tipo` filtra corretamente
      após o mapeamento (incluir um item sem `TipoLancamento` na lista
      simulada, pra provar que o default participa do filtro).

## Teste de componente (`backend/tests/GastosApp.ComponentTests/Categories/CategoryEndpointsTests.cs`)

- [x] 25. Atualizar o arquivo: remover os casos de validação de
      `Cor`/`Icone` (formato hex, ausência); adicionar cenários de
      `POST`/`PUT /categories` com `tipo`/`orcamentoMensalCents`
      válidos (com e sem orçamento), `tipo` inválido/ausente (400),
      `orcamentoMensalCents` `0`/negativo (400), `PUT` removendo um
      orçamento existente (→ `null`), `GET /categories?tipo=despesa`/
      `?tipo=receita`/sem filtro/valor inválido (400), e `POST`/`PUT`
      enviando `cor`/`icone` no corpo (sucesso normal, resposta sem
      esses campos — US11). Confirmar que o caso já existente de 403
      para role sem permissão continua passando com o novo shape de
      request.

## Fechamento

- [x] 26. Rodar `dotnet test backend/GastosApp.sln` — suíte completa
      100% passando, sem regressão em `Expenses`/`Members`
      (`[[feedback_tests_must_pass]]`).
- [x] 27. Rodar `./scripts/export-openapi.sh` e conferir
      `backend/docs/openapi.json`: `git diff` deve mostrar remoção de
      `cor`/`icone` e adição de `tipo`/`orcamentoMensalCents` nos
      schemas de `Category`, e o novo parâmetro de query `tipo` em
      `GET /categories`; commitar o arquivo atualizado.
- [x] 28. Atualizar `spec.md`: marcar todos os critérios de aceite
      concluídos (`- [x]`) e adicionar a seção "Status" (mesmo padrão
      de `backend/specs/FEAT-20-membros-convites-permissoes/spec.md`)
      resumindo o que foi implementado.
