# Plan — FEAT-32: Testes integrados dos módulos pendentes

Não introduz nenhuma regra de negócio, `Command`/`Query`, endpoint ou
mudança de contrato — é só o preenchimento da suíte de testes
integrados já estruturada pela FEAT-29. Por isso este plano não tem
`Error`/`ErrorType` novo nem mudança de PK/SK de escrita de negócio —
só um novo padrão de **leitura administrativa** (limpeza da segunda
conta de teste), sobre índice já provisionado.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| `tests/GastosApp.IntegrationTests/Support/Contracts.cs` | Ganha os DTOs de request/response dos 7 módulos (espelhando `backend/docs/openapi.json`, mesma convenção já usada por `RegisterRequestDto`/`LoginResponseDto`) |
| `tests/GastosApp.IntegrationTests/Support/TestAccountFixture.cs` | Ganha um método novo, `InviteAndAcceptAsync`, que cria/convida/loga uma **segunda conta de teste** (usada pelos módulos Membros e Transações) |
| `tests/GastosApp.IntegrationTests/Support/SecondaryTestAccount.cs` (novo) | Tipo devolvido por `InviteAndAcceptAsync` — mesmo papel de `TestAccountFixture`, mas para o membro convidado, com limpeza própria |
| `tests/GastosApp.IntegrationTests/Categories/CategoriesFlowTests.cs` (novo) | Módulo Categorias |
| `tests/GastosApp.IntegrationTests/Transactions/TransactionsFlowTests.cs` (novo) | Módulo Transações |
| `tests/GastosApp.IntegrationTests/Transactions/ExportFlowTests.cs` (novo) | Módulo Exportação CSV |
| `tests/GastosApp.IntegrationTests/Members/MembersFlowTests.cs` (novo) | Módulo Membros/convites |
| `tests/GastosApp.IntegrationTests/Summary/SummaryFlowTests.cs` (novo) | Módulo Resumo mensal |
| `tests/GastosApp.IntegrationTests/Reports/ReportsFlowTests.cs` (novo) | Módulo Relatórios |
| `tests/GastosApp.IntegrationTests/Auth/AuthFlowTests.cs` | Ganha asserção de perfil em `GET /auth/me` e um teste de CPF duplicado (409) — módulo Perfil |
| `backend/docs/backlog.md` | Item de débito "Módulos sem teste integrado ainda" marcado como concluído, apontando para esta FEAT |

Nenhuma camada de produção (`Api`/`Application`/`Domain`/`Infrastructure`)
muda. Nenhum workflow de CI/CD muda — `integration-tests`
(`backend-deploy-hom.yml`) e `backend-integration-tests-prod.yml`
(FEAT-29) já rodam `dotnet test tests/GastosApp.IntegrationTests` no
projeto inteiro, então os arquivos novos passam a rodar nesses gates
automaticamente, sem tocar em nenhum `.yml`.

## Contratos técnicos detalhados

### `Support/Contracts.cs` — DTOs novos

Mesma convenção já usada (records `sealed`, só os campos que os testes
efetivamente leem/enviam, espelhando `backend/docs/openapi.json` — não
os records internos da Api):

```csharp
// Categorias
public sealed record CategoryRequestDto(string Nome, string Tipo, long? OrcamentoMensalCents);
public sealed record CategoryResponseDto(string Id, string Nome, string Tipo, long? OrcamentoMensalCents, string CreatedAt);
public sealed record CategoryListResponseDto(List<CategoryResponseDto> Items);

// Transações
public sealed record TransactionRequestDto(string Description, long AmountInCents, string CategoryId, string Tipo, string Date);
public sealed record TransactionResponseDto(string Id, string Description, long AmountInCents, string CategoryId, string Tipo, string Date, string CreatedByUserId, string CreatedByLabel, string CreatedAt);
public sealed record TransactionListResponseDto(List<TransactionResponseDto> Items, string? NextCursor);

// Membros
public sealed record MemberRequestDto(string Email, string Role);
public sealed record MemberRoleRequestDto(string Role);
public sealed record MemberResponseDto(string Id, string Email, string Role, string Status, string CreatedAt);
public sealed record MemberListResponseDto(List<MemberResponseDto> Items);

// Resumo mensal
public sealed record CategorySummaryItemDto(string CategoryId, string Nome, long GastoCents, long? OrcamentoMensalCents);
public sealed record SummaryResponseDto(string Month, long SaldoCents, long ReceitasCents, long GastoCents, long OrcamentoTotalCents, long RestanteCents, List<CategorySummaryItemDto> PorCategoria, List<TransactionResponseDto> UltimosLancamentos);

// Relatórios
public sealed record ReportCategoryItemDto(string CategoryId, string Nome, long GastoCents);
public sealed record ReportTopCategoryDto(string CategoryId, string Nome, long GastoCents, decimal? PercentualOrcamento);
public sealed record ReportsResponseDto(string Period, string StartDate, string EndDate, long TotalCents, decimal? VariacaoPercentual, List<ReportCategoryItemDto> PorCategoria, ReportTopCategoryDto? MaiorGasto);
```

`GET /transactions/export` não desserializa `TransportResponse.Body`
como JSON — o teste lê `response.Body` como texto CSV cru (`string`)
e faz asserção sobre linhas/colunas diretamente (`Split('\n')`/
`StartsWith("data;descricao;...")`), sem DTO novo. Confirma também
`response.Headers["Content-Type"]` e `Content-Disposition` (já
expostos por `TransportResponse.Headers`, sem mudança em
`IApiTransport`/`TransportResponse`).

### `TestAccountFixture.InviteAndAcceptAsync` — segunda conta de teste

Necessário para os únicos dois cenários que exigem duas identidades
reais simultâneas: aceite de convite no login (módulo Membros) e
autorização por autoria do papel `Lancar` (módulo Transações — editar/
excluir só o que o próprio membro criou, 403 na transação de outro).
Reaproveita a mesma conta principal (`TestAccountFixture`, sempre
`Titular` da conta ativa) como convidador.

```csharp
public sealed class TestAccountFixture : IAsyncDisposable
{
    // ...membros existentes inalterados...

    public async Task<SecondaryTestAccount> InviteAndAcceptAsync(
        string role, CancellationToken cancellationToken = default)
    {
        var secondaryEmail = $"int-test+{Guid.NewGuid():N}@jrnexpenses.com";
        var secondaryCpf = CpfGenerator.GenerateUnique();

        // 1) Titular (esta conta) convida o e-mail — POST /members
        var inviteResponse = await Transport.SendAsync(
            HttpMethod.Post, "/members",
            new MemberRequestDto(secondaryEmail, role),
            bearerToken: AccessToken, cancellationToken);
        if (inviteResponse.StatusCode != 201)
            throw new InvalidOperationException($"Convite falhou ({inviteResponse.StatusCode}): {inviteResponse.Body}");

        // 2) Segunda identidade real: register + AdminConfirmSignUp (mesmo
        //    padrão do setup principal, usando o UserPoolId já resolvido).
        var secondaryTransport = ApiTransportFactory.Create(_env);
        var registerResponse = await secondaryTransport.SendAsync(
            HttpMethod.Post, "/auth/register",
            new RegisterRequestDto(secondaryEmail, Password, "Membro Convidado (Teste Integrado)", "11988888888", secondaryCpf),
            cancellationToken: cancellationToken);
        // ...valida 201, extrai UserId, AdminConfirmSignUpAsync (mesmo _userPoolId)...

        // 3) Login da segunda identidade: dispara EnsureAccountCommand
        //    (cria a conta pessoal dela, idempotente) + AcceptPendingInvitesCommand
        //    (aceita o convite do passo 1, troca a conta ativa dela pra
        //    esta conta — a mais recente) — mesmo efeito colateral já
        //    coberto pela FEAT-20.
        var loginResponse = await secondaryTransport.SendAsync(
            HttpMethod.Post, "/auth/login",
            new LoginRequestDto(secondaryEmail, Password), cancellationToken: cancellationToken);
        // ...valida 200, extrai AccessToken...

        return new SecondaryTestAccount(
            _env, secondaryTransport, secondaryEmail, secondaryCpf,
            userId, accessToken,
            excludedAccountId: /* AccountId desta conta principal, já resolvido no setup */ _accountId);
    }
}
```

`_accountId` (novo campo privado, hoje não guardado — só `UserId`) passa
a ser capturado em `SetupAsync` a partir do `AccountPointer` resolvido
via `Query PK=USER#<UserId>` (mesma consulta que `CleanupDynamoDbAsync`
já faz — só passa a guardar o valor em vez de só usá-lo na hora da
limpeza).

### `SecondaryTestAccount` — limpeza da segunda conta

```csharp
public sealed class SecondaryTestAccount : IAsyncDisposable
{
    public IApiTransport Transport { get; }
    public string Email { get; }
    public string Cpf { get; }
    public string UserId { get; }
    public string AccessToken { get; }

    // ctor interno, só chamado por TestAccountFixture.InviteAndAcceptAsync

    public async ValueTask DisposeAsync()
    {
        // 1) GSI1PK=USER#<UserId> → todas as Memberships Ativas deste
        //    usuário (a pessoal + a da conta convidada). Filtra a que
        //    NÃO é a conta convidada (GSI1SK != "ACCOUNT#{excludedAccountId}")
        //    — essa é a conta PESSOAL da segunda identidade, criada pelo
        //    EnsureAccountCommand no login do passo 3 acima.
        // 2) Query PK=ACCOUNT#<contaPessoalId> → apaga Account, Membership
        //    (Titular) e as 13 categorias padrão semeadas nela (mesmo
        //    BatchWriteItem já usado por TestAccountFixture.CleanupDynamoDbAsync).
        //    NÃO mexe na conta convidada (ACCOUNT#<excludedAccountId>) — a
        //    Membership desta segunda identidade lá dentro já é removida
        //    pela limpeza da conta PRINCIPAL (TestAccountFixture.DisposeAsync,
        //    que apaga toda a partição ACCOUNT#<accountId>, Membership
        //    incluída).
        // 3) Query PK=USER#<UserId> → apaga AccountPointer + UserProfile.
        // 4) DeleteItem PK=CPF#<Cpf>, SK=CPF#.
        // 5) AdminDeleteUserAsync no Cognito (Email).
        // Mesmo padrão best-effort (try/catch por etapa + log em stderr)
        // já usado por TestAccountFixture.DisposeAsync.
    }
}
```

Uso num teste (`await using` aninhado — ordem de disposição não importa,
as duas limpezas operam em partições disjuntas):

```csharp
await using var titular = await TestAccountFixture.CreateAsync();
await using var membro = await titular.InviteAndAcceptAsync("Lancar");
// titular.Transport / titular.AccessToken → chamadas como Titular
// membro.Transport / membro.AccessToken → chamadas como o convidado
```

### Padrão de acesso ao DynamoDB usado pela limpeza (novo, só leitura administrativa)

| # | Query | Mecanismo |
|---|---|---|
| Memberships do usuário convidado (para achar sua conta pessoal) | `IndexName=GSI1, GSI1PK=USER#<userId>` (sem condição em `GSI1SK` — traz as duas: a pessoal e a convidada) | Índice `GSI1` já provisionado (FEAT-19/20), mesmo access pattern documentado em `backend/docs/data-model.md` ("Papel do chamador na conta ativa"), só sem o `AND GSI1SK=...` porque aqui o objetivo é justamente **descobrir** as duas contas, não confirmar uma específica |

Nenhum GSI novo, nenhum atributo novo — reaproveita exatamente o que
`backend/docs/data-model.md` já documenta para `Membership`.

### Arquivos de teste — cobertura por módulo (nomes ilustrativos, `plan.md` não fixa a assinatura exata de cada `[Fact]`, só o que precisa estar coberto — ver `spec.md`, "Cobertura por módulo")

| Módulo | Arquivo | Cenários mínimos |
|---|---|---|
| Categorias | `Categories/CategoriesFlowTests.cs` | Criar (com/sem orçamento) → listar → editar → excluir (sucesso); excluir com transação associada (422, exige criar transação real antes); `Leitura` em escrita (403); categoria de uma conta invisível pra outra |
| Transações | `Transactions/TransactionsFlowTests.cs` | Registrar despesa e receita (categoria do tipo certo) → listar → consultar por id → editar → excluir; `tipo` divergente da categoria (400); `Lancar` edita/exclui a própria (`InviteAndAcceptAsync("Lancar")`) e recebe 403 na do Titular; isolamento entre contas |
| Membros | `Members/MembersFlowTests.cs` | Convidar (201, `ConvitePendente`) → listar → trocar papel → remover; convite aceito de verdade via `InviteAndAcceptAsync` (`Status=Ativo` após o login da segunda identidade); não-Titular recebe 403 |
| Resumo mensal | `Summary/SummaryFlowTests.cs` | Mês com transação(ões) e categoria com orçamento retorna totais corretos; mês sem dado retorna 200 zerado; `Leitura` recebe 200; isolamento entre contas |
| Relatórios | `Reports/ReportsFlowTests.cs` | `period=month` com despesa real retorna `totalCents`/`porCategoria`/`maiorGasto` corretos; `Leitura` recebe 200; isolamento entre contas |
| Exportação CSV | `Transactions/ExportFlowTests.cs` | Exportar com transação real retorna CSV com cabeçalho + linha correta (`valor` em vírgula decimal); filtro sem resultado retorna CSV só de cabeçalho; `Leitura` recebe 200 |
| Perfil | `Auth/AuthFlowTests.cs` (teste novo, mesmo arquivo) | `GET /auth/me` reflete `name`/`phoneNumber`/`cpf` do registro; segundo registro com mesmo CPF retorna 409 (`cpf-already-exists`) |

## Mapeamento de erros de negócio (nenhum novo — só o que já existe, por módulo)

| Módulo | `ProblemDetailsDto.Type` exercitado | Status |
|---|---|---|
| Categorias | `category-in-use` | 422 |
| Categorias/Transações | `insufficient-permission` | 403 |
| Transações | `validation-error` (tipo divergente da categoria) | 400 |
| Membros | `insufficient-permission` | 403 |
| Perfil | `cpf-already-exists` | 409 |

Resumo e Relatórios não exercitam nenhum `Type` de erro novo nesta
feature (cobertura mínima é sucesso + 200 pra `Leitura` + isolamento,
ver `spec.md`) — 400 de parâmetro ausente já é responsabilidade da
suíte de componente.

## Recursos AWS usados/afetados

**Nenhum recurso novo.** A única leitura administrativa nova
(`Query IndexName=GSI1` em `SecondaryTestAccount`) usa o mesmo índice
já provisionado e a mesma permissão IAM `dynamodb:Query` já concedida
pela FEAT-29 à role `gastosapp-backend-cicd`
(`backend/infra/terraform/cicd/iam-policy.tf`), escopada à ARN da
tabela (`arn:aws:dynamodb:...:table/GastosApp{-Hom}`) — sem `/index/*`
no `resources`. **Ponto a confirmar durante o `/tasks`**: `Query`
contra um GSI é autorizado pela mesma ARN de tabela (comportamento
padrão documentado pela AWS para a action `Query` sem condições
`dynamodb:LeadingKeys`), mas isso só será validado de fato rodando a
suíte contra hom real (mesmo espírito do achado real da FEAT-29 sobre
`terraform import` — confirmar na prática, não só na documentação). Se
a permissão faltar, o fix é ampliar `resources` da policy existente
com a ARN do índice — não criar recurso novo, e ainda assim exige
aprovação explícita antes de qualquer `terraform apply` (mesma regra
já vigente).

## Achados reais durante a implementação

- **Paralelismo do xUnit derruba o container RIE** — descoberto ao rodar
  `run-local.sh` com a primeira segunda classe de teste
  (`MembersFlowTests`, além de `AuthFlowTests` já existente): por
  padrão o xUnit roda classes de teste diferentes em paralelo (só
  serializa métodos dentro da mesma classe), e isso nunca foi um
  problema enquanto só existia uma classe (FEAT-29). Contra o modo
  local (`LambdaRieTransport`), duas classes disparando requisições
  simultâneas contra o mesmo container do Runtime Interface Emulator
  derrubam a conexão (`HttpIOException: The response ended
  prematurely`) — o RIE emula o modelo de execução do Lambda real (uma
  invocação de cada vez), sem suportar concorrência. **Fix**: novo
  `Support/AssemblyInfo.cs` com
  `[assembly: CollectionBehavior(DisableTestParallelization = true)]`,
  desabilitando paralelismo pra todo o assembly — resolve pra este e
  qualquer módulo futuro, e é consistente com o alvo real da suíte
  (API real compartilhada, hom/prod incluídos, também não desenhada
  pra concorrência de uma mesma execução de teste). Efeito colateral
  aceito: a suíte roda sequencialmente (mais lenta conforme mais
  módulos entrarem) — irrelevante no volume atual.

- **`LambdaRieTransport` nunca separava query string do path** —
  descoberto ao rodar os módulos Resumo/Relatórios/Exportação (os
  primeiros a exercitar `GET` com query string em modo local — o único
  módulo anterior, Auth, nunca usava). `SendAsync` recebia `path` (ex.:
  `/summary?month=2026-08`) e usava a string inteira como `RawPath`/
  `Http.Path` do evento do API Gateway v2, com `RawQueryString` sempre
  `""` — o roteamento da Api recebia o `?...` como parte literal do
  path e nunca casava nenhuma rota (404 em toda chamada com query
  string). **Fix**: `SendAsync` agora separa `path` em `rawPath` +
  `rawQueryString` (no primeiro `?`) antes de montar o evento —
  `DirectHttpTransport` (hom/prod) nunca teve esse problema, já que usa
  `HttpClient` com a URL completa. Efeito colateral: nenhum — é
  correção de um bug latente da infraestrutura de teste da FEAT-29,
  sem mudar contrato nem comportamento observável de nenhum teste já
  existente (Auth/Membros/Categorias/Transações não usam query string).

## Pontos que precisam de confirmação antes do `/tasks`

1. **Nome do arquivo `Transactions/ExportFlowTests.cs` vs.
   `Categories`/`Transactions` como pasta comum** — a spec já fixa essa
   convenção (endpoint é `/transactions/export`, então o teste vive na
   pasta `Transactions/`, arquivo próprio por ser um módulo de débito
   separado no backlog); sinalizado aqui só pra confirmar que não há
   objeção antes do `/tasks` detalhar os `[Fact]`s.
2. **Permissão IAM de `Query` em GSI** (ver seção "Recursos AWS" acima)
   — validar empiricamente ao rodar os testes do módulo Membros/
   Transações (que usam `InviteAndAcceptAsync`) contra hom pela
   primeira vez; se faltar, tratar como ajuste de infraestrutura com
   aprovação prévia, não como bloqueio do `/tasks`.
3. **Senha da segunda identidade** — reaproveita a mesma constante
   `Password` já privada em `TestAccountFixture` (não precisa ser
   diferente da conta principal; contas de teste nunca coexistem além
   da execução do teste).
