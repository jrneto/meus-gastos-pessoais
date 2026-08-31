# Tasks: FEAT-30 — Categoria: escopar busca por ID (GSI2) por conta

- [x] 1. Atualizar `CategoryItemMapper.BuildItem` (`backend/src/GastosApp.Infrastructure/Categories/CategoryItemMapper.cs`) — `GSI2PK` passa de `$"ID#{category.Id}"` para `$"ID#{category.AccountId}#{category.Id}"`

- [x] 2. Atualizar `LookupByIdAsync` em `DynamoDbCategoryRepository` (`backend/src/GastosApp.Infrastructure/Categories/DynamoDbCategoryRepository.cs`) — ganha o parâmetro `accountId`; a `Query` no `GSI2` passa a usar `GSI2PK = $"ID#{accountId}#{categoryId}"` em vez de `$"ID#{categoryId}"` (mantém `Limit = 1`, agora sem ambiguidade possível)

- [x] 3. Atualizar `GetByIdAsync`/`UpdateAsync`/`DeleteAsync` (mesmo arquivo) — passar `accountId` para `LookupByIdAsync` e remover o post-check `if (pk != $"ACCOUNT#{accountId}") return null/NotFound`, redundante agora que a busca já é escopada por conta na própria `Query`

- [x] 4. Atualizar `MapToCategory` (mesmo arquivo) — trocar `gsi2pk.IndexOf('#')` por `gsi2pk.LastIndexOf('#')` na extração do `id`, já que ele é sempre o último segmento

- [x] 5. Atualizar o comentário de cabeçalho de `DynamoDbCategoryRepository` (linhas ~15-26) — hoje descreve `GSI2PK = "ID#{id}"` compartilhado com `Transaction`; ajustar para deixar claro que `Category` passou a usar `ID#<accountId>#<categoryId>` (colisão de conta não é mais possível), mantendo só a explicação da colisão de **tipo** (`Category` vs. `Transaction`, resolvida pelo atributo `Tipo`)

- [ ] 6. Atualizar o helper `BuildItem` de teste e todos os itens simulados com `GSI2PK` em `DynamoDbCategoryRepositoryTests.cs` (`backend/tests/GastosApp.UnitTests/Infrastructure/`) para o formato novo (`$"ID#{accountId}#{id}"`), incluindo o assert de `CreateAsync_ShouldReturnSuccess_WhenPutItemSucceeds`

- [ ] 7. Substituir `GetByIdAsync_ShouldReturnNull_WhenCategoryBelongsToAnotherUser`, `UpdateAsync_ShouldReturnNotFound_WhenCategoryBelongsToAnotherUser` e `DeleteAsync_ShouldReturnFalse_WhenCategoryBelongsToAnotherUser` (mesmo arquivo) — o cenário que simulavam (`Query` devolvendo item de outra conta) deixa de ser representativo, já que a `Query` real agora nunca devolveria isso; trocar por testes que capturam o `QueryRequest` enviado (`Arg.Is<QueryRequest>`) e confirmam `ExpressionAttributeValues[":gsi2pk"].S == $"ID#{accountId}#{categoryId}"` para os três métodos — é o teste de regressão do bug em si

- [ ] 8. Rodar `dotnet test backend/GastosApp.sln --filter FullyQualifiedName~DynamoDbCategoryRepositoryTests` e confirmar tudo passando

- [ ] 9. Atualizar `CreateAsync_ShouldWriteAccountPointerAccountAndMembership_WhenNoConflict...` (ou nome atual do teste, `backend/tests/GastosApp.UnitTests/Infrastructure/DynamoDbAccountRepositoryTests.cs`, linha ~110) — assert de cada uma das 13 categorias padrão passa de `Put.Item["GSI2PK"].S == $"ID#{id}"` para `== $"ID#{accountId}#{id}"`

- [ ] 10. Atualizar `backend/docs/data-model.md` — seção `Category`: linha do `GSI2PK` (`ID#<id>` → `ID#<accountId>#<categoryId>`) e a frase que descreve o mecanismo de busca; seção "Espaço de chave compartilhado entre tipos de item de uma conta": ajustar para refletir que `Category` e `Transaction` não compartilham mais o mesmo formato de `GSI2PK` (ver `plan.md`, seção 5, para o texto orientador)

- [ ] 11. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` (suíte completa unit + componente) — sem regressão, incluindo `CategoryEndpointsTests`/`TransactionEndpointsTests` (mockam `ICategoryRepository`, não `DynamoDbCategoryRepository` real — não devem precisar de ajuste)

- [ ] 12. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que `backend/docs/openapi.json` não tem nenhuma diferença de contrato

- [ ] 13. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-30-categoria-gsi2-escopo-conta/spec.md` e preencher a seção "Status" (nova), resumindo o que foi implementado — inclui registrar explicitamente que não houve teste integrado nesta feature, por decisão do usuário

- [ ] 14. Remover a entrada "**BUG: busca de categoria por ID (`GSI2`) não é escopada por conta...**" de `backend/docs/backlog.md` (seção "Débitos técnicos e melhorias futuras") — bug resolvido, sai da lista
