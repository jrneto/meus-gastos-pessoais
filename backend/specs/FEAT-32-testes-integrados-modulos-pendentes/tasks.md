# Tasks: FEAT-32 — Testes integrados dos módulos pendentes

## Fase 0 — Extensão da infraestrutura de teste (pré-requisito dos módulos Membros/Transações)

- [x] 1. Em `TestAccountFixture.cs` (`backend/tests/GastosApp.IntegrationTests/Support/`), adicionar campo privado `_accountId` e capturá-lo em `SetupAsync` a partir do `AccountPointer` já resolvido em `CleanupDynamoDbAsync` (`Query PK=USER#<UserId>`, item `SK=ACCOUNT#`) — sem duplicar a consulta: mover essa resolução para logo após o login ter sucesso, guardar em `_accountId`, e reaproveitar o mesmo valor dentro de `CleanupDynamoDbAsync` (que hoje resolve de novo)

- [x] 2. Em `Support/Contracts.cs`, adicionar `MemberRequestDto(string Email, string Role)`, `MemberRoleRequestDto(string Role)`, `MemberResponseDto(string Id, string Email, string Role, string Status, string CreatedAt)`, `MemberListResponseDto(List<MemberResponseDto> Items)`

- [x] 3. Criar `Support/SecondaryTestAccount.cs` — tipo `sealed class SecondaryTestAccount : IAsyncDisposable` com `Transport`/`Email`/`Cpf`/`UserId`/`AccessToken` (ctor interno, recebe também o `accountId` da conta principal a excluir da limpeza); `DisposeAsync` ainda sem lógica de limpeza (só descarta `Transport`) — implementado na próxima task

- [x] 4. Implementar a limpeza em `SecondaryTestAccount.DisposeAsync`: `Query IndexName=GSI1, GSI1PK=USER#<UserId>` → localizar a `Membership` cujo `GSI1SK` **não** é `ACCOUNT#<accountId excluído>` (conta pessoal da segunda identidade); `Query PK=ACCOUNT#<contaPessoalId>` + `BatchWriteItem` apagando tudo dela (Account, Membership Titular, categorias padrão semeadas); apagar `USER#<UserId>` (AccountPointer + UserProfile); apagar `CPF#<Cpf>`; `AdminDeleteUserAsync` no Cognito — mesmo padrão best-effort (try/catch por etapa, log em stderr) de `TestAccountFixture.DisposeAsync`

- [x] 5. Implementar `TestAccountFixture.InviteAndAcceptAsync(string role, CancellationToken)`: `POST /members` (Titular convida `secondaryEmail`/`role`) → `POST /auth/register` da segunda identidade (novo `IApiTransport` via `ApiTransportFactory.Create(_env)`) → `AdminConfirmSignUpAsync` (mesmo `_userPoolId`) → `POST /auth/login` da segunda identidade → retorna `SecondaryTestAccount` populado, passando `_accountId` (task 1) como conta a excluir da limpeza

## Fase 1 — Módulo Membros (primeiro consumidor de `InviteAndAcceptAsync` — valida o mecanismo de segunda conta antes dos demais módulos dependerem dele)

- [x] 6. Criar `Members/MembersFlowTests.cs` com `[Trait("Category", "Integration")]` e o teste de fluxo de sucesso: Titular convida (`POST /members` → 201, `Status=ConvitePendente`) → `GET /members` lista o convite → `PUT /members/{id}` troca o papel → `DELETE /members/{id}` remove

- [x] 7. Adicionar `InviteAndAccept_ConvitePendenteAceitoNoLogin_MembershipFicaAtiva` — usa `await using var membro = await titular.InviteAndAcceptAsync("Leitura")`; após o login embutido, `GET /members` (chamado pelo Titular) mostra o membro com `Status=Ativo`; roda **localmente via `run-local.sh`** antes de seguir para as próximas tasks, para validar de ponta a ponta o mecanismo de segunda conta (fixture + limpeza) introduzido na Fase 0

- [x] 8. Adicionar `Members_ChamadoPorNaoTitular_Retorna403` — `membro.Transport` (papel não-Titular) tentando `POST`/`PUT`/`DELETE /members` recebe 403 em todos

## Fase 2 — Módulo Categorias

- [x] 9. Em `Support/Contracts.cs`, adicionar `CategoryRequestDto(string Nome, string Tipo, long? OrcamentoMensalCents)`, `CategoryResponseDto(string Id, string Nome, string Tipo, long? OrcamentoMensalCents, string CreatedAt)`, `CategoryListResponseDto(List<CategoryResponseDto> Items)`

- [x] 10. Criar `Categories/CategoriesFlowTests.cs` com o fluxo de sucesso: `POST /categories` (com e sem `orcamentoMensalCents`) → `GET /categories` lista → `PUT /categories/{id}` edita → `DELETE /categories/{id}` exclui (204)

- [x] 11. Adicionar `Categories_ChamadoPorLeitura_Retorna403EmEscrita` — usa `titular.InviteAndAcceptAsync("Leitura")`; `POST`/`PUT`/`DELETE /categories` pelo convidado recebem 403

- [x] 12. Adicionar `Categories_IsolamentoEntreContas_CategoriaDeUmaContaNaoApareceNaOutra` — duas instâncias de `TestAccountFixture.CreateAsync()`; categoria criada numa não aparece em `GET /categories` nem é acessível por `GET/PUT/DELETE /categories/{id}` da outra (404)

## Fase 3 — Módulo Transações (depende dos DTOs de Categoria da Fase 2)

- [x] 13. Em `Support/Contracts.cs`, adicionar `TransactionRequestDto(string Description, long AmountInCents, string CategoryId, string Tipo, string Date)`, `TransactionResponseDto(string Id, string Description, long AmountInCents, string CategoryId, string Tipo, string Date, string CreatedByUserId, string CreatedByLabel, string CreatedAt)`, `TransactionListResponseDto(List<TransactionResponseDto> Items, string? NextCursor)`

- [x] 14. Criar `Transactions/TransactionsFlowTests.cs` com o fluxo de sucesso: cria categoria `despesa` e categoria `receita` (via `CategoryRequestDto`) → `POST /transactions` de uma despesa e de uma receita (cada uma contra a categoria do tipo certo, `createdByLabel="Você"`) → `GET /transactions` lista ambas → `GET /transactions/{id}` → `PUT /transactions/{id}` → `DELETE /transactions/{id}`

- [x] 15. Adicionar `Transactions_TipoDivergenteDaCategoria_Retorna400` — `POST /transactions` com `tipo="receita"` referenciando categoria `tipo="despesa"` (ou vice-versa) retorna 400

- [x] 16. Adicionar `Transactions_PapelLancar_EditaEExcluiApenasAPropria` — usa `titular.InviteAndAcceptAsync("Lancar")`; o convidado cria uma transação própria e consegue `PUT`/`DELETE` nela; tentar `PUT`/`DELETE` numa transação criada pelo Titular retorna 403

- [x] 17. Adicionar `Transactions_IsolamentoEntreContas_TransacaoDeUmaContaNaoApareceNaOutra` — mesmo padrão da task 12, para transações

- [x] 18. Voltar em `Categories/CategoriesFlowTests.cs` (Fase 2) e adicionar `DeleteCategories_ComTransacaoAssociada_Retorna422` — agora com `TransactionRequestDto` disponível: cria categoria + transação vinculada a ela, `DELETE /categories/{id}` retorna 422 e a categoria permanece

## Fase 4 — Módulo Resumo mensal

- [x] 19. Em `Support/Contracts.cs`, adicionar `CategorySummaryItemDto(string CategoryId, string Nome, long GastoCents, long? OrcamentoMensalCents)`, `SummaryResponseDto(string Month, long SaldoCents, long ReceitasCents, long GastoCents, long OrcamentoTotalCents, long RestanteCents, List<CategorySummaryItemDto> PorCategoria, List<TransactionResponseDto> UltimosLancamentos)`

- [x] 20. Criar `Summary/SummaryFlowTests.cs` com o fluxo de sucesso: cria categoria de despesa com orçamento + uma transação de despesa e uma de receita no mês corrente → `GET /summary?month=YYYY-MM` retorna `saldoCents`/`receitasCents`/`gastoCents`/`orcamentoTotalCents`/`restanteCents`/`porCategoria`/`ultimosLancamentos` corretos

- [x] 21. Adicionar `Summary_MesSemDados_RetornaZerado` — `GET /summary` para um mês sem nenhuma transação retorna 200 com todos os totais zerados (não 404)

- [x] 22. Adicionar `Summary_ChamadoPorLeitura_Retorna200` (usa `InviteAndAcceptAsync("Leitura")`) e `Summary_IsolamentoEntreContas_NaoRefleteDadosDeOutraConta` (mesmo padrão das tasks 12/17)

## Fase 5 — Módulo Relatórios

- [x] 23. Em `Support/Contracts.cs`, adicionar `ReportCategoryItemDto(string CategoryId, string Nome, long GastoCents)`, `ReportTopCategoryDto(string CategoryId, string Nome, long GastoCents, decimal? PercentualOrcamento)`, `ReportsResponseDto(string Period, string StartDate, string EndDate, long TotalCents, decimal? VariacaoPercentual, List<ReportCategoryItemDto> PorCategoria, ReportTopCategoryDto? MaiorGasto)`

- [x] 24. Criar `Reports/ReportsFlowTests.cs` com o fluxo de sucesso: cria categoria de despesa com orçamento + uma transação de despesa dentro do mês corrente → `GET /reports?period=month&date=<hoje>` retorna `totalCents`/`porCategoria`/`maiorGasto` (incluindo `percentualOrcamento`) corretos

- [x] 25. Adicionar `Reports_ChamadoPorLeitura_Retorna200` (usa `InviteAndAcceptAsync("Leitura")`) e `Reports_IsolamentoEntreContas_NaoRefleteDadosDeOutraConta`

## Fase 6 — Módulo Exportação CSV

- [x] 26. Criar `Transactions/ExportFlowTests.cs` com o fluxo de sucesso: cria categoria + transação de despesa → `GET /transactions/export` retorna 200, `Content-Type: text/csv`, corpo (`response.Body`, lido como texto) começando pelo cabeçalho `data;descricao;categoria;tipo;valor;lancadoPor` e contendo a linha da transação com `valor` em vírgula decimal (ex.: `45,90`, não `4590`)

- [x] 27. Adicionar `Export_SemResultado_RetornaCsvSoComCabecalho` — `GET /transactions/export?tipo=receita` numa conta só com despesas retorna 200 com CSV de uma única linha (cabeçalho)

- [x] 28. Adicionar `Export_ChamadoPorLeitura_Retorna200` (usa `InviteAndAcceptAsync("Leitura")`)

## Fase 7 — Módulo Perfil (extensão de `AuthFlowTests.cs`)

- [x] 29. Em `Auth/AuthFlowTests.cs`, estender `RegisterConfirmLogin_FluxoCompleto_RetornaAccessTokenValido` (ou adicionar um teste próprio) para afirmar que `GET /auth/me` retorna `name`, `phoneNumber` e `cpf` idênticos aos enviados no `POST /auth/register` do `TestAccountFixture.SetupAsync` — ajustar `MeResponseDto` interno se necessário

- [x] 30. Adicionar `Register_CpfJaCadastrado_Retorna409` — segunda tentativa de registro com o mesmo `Cpf` de uma `TestAccountFixture` já criada (e-mail diferente) retorna 409 com `problem.Type == "https://gastosapp.dev/errors/cpf-already-exists"`

## Fase 8 — Validação final e fechamento

- [x] 31. Rodar `backend/infra/lambda/run-local.sh` (binário Native AOT via Runtime Interface Emulator) com a suíte completa de `GastosApp.IntegrationTests` — confirmar todos os testes novos (Fases 1-7) e os já existentes de `AuthFlowTests` passando, sem falha nem rastro deixado no ambiente local (LocalStack/cognito-local limpos ao final)

- [x] 32. Rodar `dotnet test GastosApp.sln` (suíte unitário + componente) e confirmar zero regressão e que `GastosApp.IntegrationTests` continua fora do escopo padrão (filtro `Category!=Integration` já configurado pela FEAT-29)

- [x] 33. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-32-testes-integrados-modulos-pendentes/spec.md` e preencher uma seção "Status", resumindo o que foi implementado (incluir o achado real sobre a permissão IAM de `Query` em GSI, ver `plan.md` "Pontos que precisam de confirmação")

- [x] 34. Atualizar `backend/docs/backlog.md` — mover o item "DÉBITO — Módulos sem teste integrado ainda" da seção "Débitos técnicos e melhorias futuras" para fora dela (ou marcar `[x]` apontando para esta FEAT, mesmo padrão já usado pelo item "BUG" da FEAT-31)

- [x] 35. Confirmar que a suíte integrada roda com sucesso no próximo push em `develop` (job `integration-tests` de `backend-deploy-hom.yml`, contra hom real) — validar ao vivo o ponto em aberto do `plan.md` sobre a permissão IAM de `Query` em GSI (task Fase 0); se faltar permissão, tratar como ajuste de infraestrutura à parte, com aprovação explícita antes de qualquer `terraform apply`
