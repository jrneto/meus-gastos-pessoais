# Tasks: FEAT-28 — Seed de categorias padrão

- [ ] 1. Criar `DefaultCategorySeed` (`backend/src/GastosApp.Domain/Categories/DefaultCategorySeed.cs`) — `const string Tipo = "despesa"` e `IReadOnlyList<(string Id, string Nome)> Items` com as 13 entradas fixas (ids/nomes conforme `plan.md`, seção 1)

- [ ] 2. Adicionar `Domain/Categories/DefaultCategorySeedTests.cs` (`backend/tests/GastosApp.UnitTests/`) — `Items` tem exatamente 13 entradas; todos os `Id` são `Guid` válidos e distintos entre si; todos os `Nome` são distintos entre si (nenhum duplicado, nem por slug)

- [ ] 3. Criar `CategoryItemMapper` (`backend/src/GastosApp.Infrastructure/Categories/CategoryItemMapper.cs`, `internal static`) — mover `BuildSk(nome)` e `BuildItem(Category, sk)` de `DynamoDbCategoryRepository` pra cá, sem mudar a lógica

- [ ] 4. Refatorar `DynamoDbCategoryRepository` (`backend/src/GastosApp.Infrastructure/Categories/`) — remover os métodos privados `BuildSk`/`BuildItem` movidos na task 3 e chamar `CategoryItemMapper.BuildSk`/`BuildItem` no lugar; rodar `DynamoDbCategoryRepositoryTests` e confirmar que passam sem alteração (refactor puro, sem mudança de comportamento)

- [ ] 5. Atualizar `DynamoDbAccountRepository.CreateAsync` (`backend/src/GastosApp.Infrastructure/Accounts/DynamoDbAccountRepository.cs`) — adicionar à `TransactWriteItemsRequest.TransactItems` um `Put` por entrada de `DefaultCategorySeed.Items` (13 itens novos, usando o mesmo `createdAt` já gerado, `Category.Restore(id, accountId, nome, DefaultCategorySeed.Tipo, null, createdAt)` + `CategoryItemMapper.BuildItem`, `ConditionExpression: attribute_not_exists(PK)`); total da transação passa de 3 para 16 itens

- [ ] 6. Atualizar o doc-comment de `EnsureAccountCommand` (`backend/src/GastosApp.Application/Accounts/Commands/EnsureAccount/EnsureAccountCommand.cs`) — mencionar que a criação também semeia as 13 categorias padrão atomicamente (comentário apenas, sem mudança de código executável)

- [ ] 7. Atualizar `CreateAsync_ShouldWriteAccountPointerAccountAndMembership_WhenNoConflict` em `DynamoDbAccountRepositoryTests.cs` (`backend/tests/GastosApp.UnitTests/Infrastructure/`) — renomear para `...AndDefaultCategories`; assert `TransactItems.Count == 16`; para cada uma das 13 entradas de `DefaultCategorySeed.Items`, conferir `Put.Item["GSI2PK"].S == $"ID#{id}"`, `["Nome"].S == nome`, `["Tipo"].S == "categoria"`, `["TipoLancamento"].S == "despesa"`, `SK` correspondente ao slug do nome, e `ConditionExpression == "attribute_not_exists(PK)"`

- [ ] 8. Ajustar `CreateAsync_ShouldRecoverWinnerAccountId_WhenAccountPointerConditionFails` e `CreateAsync_ShouldRethrow_WhenTransactionCanceledForAnotherReason` (`DynamoDbAccountRepositoryTests.cs`) — expandir a lista `CancellationReasons` simulada para 16 posições (só o índice relevante de cada teste muda de valor); comportamento esperado não muda

- [ ] 9. Revisar `EnsureAccountCommandHandlerTests.cs` e `AccountTriggerHandlerTests.cs` (`backend/tests/GastosApp.UnitTests/`) — confirmar que passam sem alteração (nenhuma mudança de assinatura pública; o seed é interno a `DynamoDbAccountRepository`, que esses testes mockam via `IAccountRepository`)

- [ ] 10. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` — suíte completa sem regressão (inclui confirmar que `CategoryEndpointsTests`/`AuthEndpointsTests` — que usam `CategoryRepositoryMock`/`AccountRepositoryMock` isolados, não a `DynamoDbAccountRepository` real — continuam passando sem ajuste)

- [ ] 11. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que `backend/docs/openapi.json` não tem nenhuma diferença de contrato

- [ ] 12. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-28-seed-categorias-padrao/spec.md` e preencher a seção "Status", resumindo o que foi implementado

- [ ] 13. Marcar a FEAT-28 como concluída em `backend/docs/roadmap.md`
