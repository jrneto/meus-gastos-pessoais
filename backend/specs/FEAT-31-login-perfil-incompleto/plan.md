# Plan — FEAT-31: Login bloqueado quando o perfil está incompleto

## Camadas afetadas

- **Application** — única camada com mudança de comportamento:
  `LoginUserCommandHandler` ganha uma nova dependência
  (`IUserProfileRepository`, já existente desde a FEAT-26) e uma nova
  checagem entre a autenticação no Cognito e os efeitos colaterais de
  login (`EnsureAccountCommand`/`AcceptPendingInvitesCommand`). Novo
  erro `AuthErrors.ProfileIncomplete`.
- **Api** — `AuthEndpoints.MapAuthEndpoints` ganha
  `.ProducesProblem(StatusCodes.Status403Forbidden)` na definição de
  `POST /auth/login`, só para documentação OpenAPI. Nenhuma mudança de
  request/response shape, nenhum novo endpoint.
- **Domain** — sem mudança. `UserProfile` (FEAT-26) já é suficiente:
  "perfil completo" é definido como "existe um `UserProfile` gravado
  para este `userId`" (ver spec.md, decisão 3) — não precisa de
  validação campo a campo, porque a única forma de gravar um
  `UserProfile` hoje (`RegisterUserCommandHandler`) já exige os três
  campos preenchidos e válidos antes de gravar.
- **Infrastructure** — sem mudança de código. Reusa
  `DynamoDbUserProfileRepository.FindByUserIdAsync` (`GetItem` simples
  por `PK=USER#<userId>`/`SK=PROFILE#`, já implementado na FEAT-26)
  — nenhuma nova query, nenhum novo GSI, nenhuma nova tabela.

## Contratos técnicos

### `LoginUserCommandHandler` (Application)

```csharp
public sealed class LoginUserCommandHandler : ICommandHandler<LoginUserCommand, Result<LoginUserResult>>
{
    private readonly IAuthService _authService;
    private readonly IUserProfileRepository _userProfileRepository; // NOVO
    private readonly ISender _sender;
    private readonly ILogger<LoginUserCommandHandler> _logger;

    // ctor ganha IUserProfileRepository userProfileRepository (DI já registra
    // a implementação real desde a FEAT-26 — nenhuma mudança em
    // InfrastructureServiceCollectionExtensions)

    public async ValueTask<Result<LoginUserResult>> Handle(LoginUserCommand command, CancellationToken cancellationToken)
    {
        // 1. validação de email/senha vazios — inalterado

        // 2. autenticação no Cognito — inalterado
        var result = await _authService.LoginAsync(command.Email, command.Password, cancellationToken);
        if (result.IsFailure)
            return Result.Failure<LoginUserResult>(result.Error!);

        // 3. NOVO — checagem de perfil completo, logo após autenticar com
        // sucesso e antes de qualquer efeito colateral de conta/convite.
        var profile = await _userProfileRepository.FindByUserIdAsync(result.Value.UserId, cancellationToken);
        if (profile is null)
            return Result.Failure<LoginUserResult>(AuthErrors.ProfileIncomplete);

        // 4. EnsureAccountCommand (FEAT-19) — inalterado, mas só roda a
        //    partir daqui (depois do passo 3)
        // 5. AcceptPendingInvitesCommand (FEAT-20) — inalterado, mesma posição

        return Result.Success(LoginUserResult.FromLoginResult(result.Value));
    }
}
```

Não é necessário logar essa checagem como erro (`_logger.LogError`) —
diferente das falhas de `EnsureAccountCommand`/`AcceptPendingInvitesCommand`
(efeitos colaterais inesperados), perfil ausente é um retorno de
negócio esperado, do mesmo jeito que `AuthErrors.InvalidCredentials`
hoje não loga.

### `AuthErrors.cs` (Application)

```csharp
public static Error ProfileIncomplete => Error.Forbidden(
    "profile-incomplete",
    "Cadastro incompleto. Este usuário não possui perfil (nome, telefone e CPF) cadastrado.");
```

`Error.Forbidden` e `ErrorType.Forbidden` já existem (usados por
`MembershipErrors.InsufficientPermission`) — nenhuma mudança em
`Error.cs`/`ErrorType.cs`.

### `ResultHttpExtensions.BuildProblem` (Api)

Sem mudança de código — `ErrorType.Forbidden` já mapeia para
`403` / título fixo `"Acesso negado"` / `detail = error.Message`
desde que esse mapeamento foi introduzido (FEAT-20). O novo erro só
precisa existir em `AuthErrors.cs` pra já sair formatado corretamente.

### `AuthEndpoints.cs` (Api)

```csharp
group.MapPost("/login", Login)
    .Produces<LoginUserResult>(StatusCodes.Status200OK)
    .ProducesProblem(StatusCodes.Status400BadRequest)
    .ProducesProblem(StatusCodes.Status401Unauthorized)
    .ProducesProblem(StatusCodes.Status403Forbidden); // NOVO
```

`Login` (o delegate do endpoint) não muda — `result.ToHttpResult` já
trata qualquer `Result.Failure` (incluindo o novo `Forbidden`) sem
chamar o `onSuccess` que define o cookie de refresh token, então
"nenhum cookie definido em caso de bloqueio" já é garantido pelo
comportamento existente do `ToHttpResult`, sem código novo.

### DynamoDB

Nenhum recurso novo, nenhuma mudança de acesso a dado:
`FindByUserIdAsync` já faz `GetItem` (não `Query`, não precisa de GSI)
por `PK=USER#<userId>` / `SK=PROFILE#`, exatamente como já usado por
`GetCurrentUserQuery` (FEAT-26). O login passa a chamar o mesmo método
já existente do mesmo repositório já injetado no projeto.

## Recursos AWS

**Nenhum recurso AWS novo ou alterado.** Sem mudança em Cognito,
Parameter Store, tabela DynamoDB (schema) ou Terraform — reuso total
de infraestrutura já provisionada desde a FEAT-19/FEAT-26.

## Mapeamento de erro

| Cenário | `Error` | `ErrorType` | HTTP | `type` |
|---|---|---|---|---|
| Perfil ausente (`UserProfile` não encontrado) para usuário autenticado com sucesso | `AuthErrors.ProfileIncomplete` (novo) | `Forbidden` | 403 | `profile-incomplete` |

Nenhum outro mapeamento de erro muda (`invalid-credentials` → 401,
`user-not-confirmed` → 401, validação de email/senha vazios → 400
continuam exatamente como hoje).

## Decisões técnicas relevantes

1. **Ordem da checagem**: depois de `IAuthService.LoginAsync` (para não
   vazar a existência de um `UserProfile` antes de validar a senha —
   mesma lógica de segurança que já vale hoje entre "email inexistente"
   e "senha errada", ambos colapsados em `invalid-credentials`) e antes
   de `EnsureAccountCommand`/`AcceptPendingInvitesCommand` (para que um
   login bloqueado não gere efeitos colaterais de conta).
2. **Sem nova camada de validação**: como "perfil completo" é só
   "`UserProfile` existe" (decisão 3 do spec.md), não há necessidade de
   um `IValidator`/regra de domínio nova — é uma leitura + checagem de
   nulidade, direto no Handler, no mesmo espírito de
   `GetCurrentUserQueryHandler` (FEAT-26).
3. **Testes de componente — troca do "default esperto" do mock**: hoje
   `ComponentTestWebApplicationFactory.BuildDefaultUserProfileRepositoryMock()`
   deixa `FindByUserIdAsync` retornar `null` por padrão (comentário:
   "perfil ausente não é erro", válido só no contexto de `GET /auth/me`
   antes desta feature). Isso faria **todo teste de `Login_*` que hoje
   espera 200** (`Login_ComCredenciaisValidas_Retorna200`,
   `Login_ComUsuarioSemContaAinda_CriaAccountViaFallback`,
   `Login_ComUsuarioComContaExistente_NaoCriaDuplicata`,
   `Login_ComCredenciaisInvalidas_NaoCriaAccount` — este já espera
   falha por outro motivo, sem impacto —,
   `Login_ComConvitePendenteParaOEmail_AceitaETrocaContaAtiva`,
   `Login_SemConvitePendente_NaoTrocaContaAtiva`) passar a receber 403
   em vez de 200, quebrando sem relação com o que cada teste
   realmente verifica.

   Decisão: inverter o "default esperto" — `BuildDefaultUserProfileRepositoryMock()`
   passa a devolver, por padrão, um `UserProfile` completo (mesmo
   padrão já usado por `AccountRepositoryMock`/`MembershipRepositoryMock`:
   resolver o caminho feliz por padrão, e sobrescrever explicitamente
   só nos testes que simulam o caso de borda). Só dois testes precisam
   de ajuste explícito:
   - `Me_SemPerfilCadastrado_Retorna200ComCamposNulos` (já existente,
     FEAT-26) passa a configurar explicitamente
     `FindByUserIdAsync(...).Returns((UserProfile?)null)` em vez de
     depender do default.
   - Os novos testes desta feature (`Login_*Perfil*`) configuram
     explicitamente `null` para simular o cenário do bug.

   Nenhum teste de `Login_*` que hoje espera 200 precisa ser tocado.
4. **Testes unitários — mesma estratégia local**: `LoginUserCommandHandlerTests`
   não usa a factory de componente (mocks próprios via `NSubstitute`
   direto no construtor do teste). Pra não editar os 9 testes já
   existentes um por um, o construtor da classe de teste passa a
   configurar um "default esperto" equivalente:
   `_userProfileRepositoryMock.FindByUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())`
   retornando um `UserProfile` fixo válido (ex.:
   `UserProfile.Restore("user-id-123", "Fulano da Silva", "11999998888", "11144477735", DateTimeOffset.UtcNow)`),
   e os novos testes desta feature sobrescrevem para `null`
   explicitamente.

## Testes a criar/alterar

**Unit (`GastosApp.UnitTests/Application/LoginUserCommandHandlerTests.cs`):**
- Alterar construtor: novo campo `_userProfileRepositoryMock`
  (`IUserProfileRepository`), passado ao `_handler`, com default
  "perfil completo" (decisão técnica 4).
- Novo: `Handle_ShouldReturnForbiddenFailure_WhenProfileDoesNotExist`
  — `FindByUserIdAsync` retorna `null`, espera `Result.IsFailure`,
  `ErrorType.Forbidden`, código `profile-incomplete`.
- Novo: `Handle_ShouldNotDispatchEnsureAccountCommand_WhenProfileIsIncomplete`
- Novo: `Handle_ShouldNotDispatchAcceptPendingInvitesCommand_WhenProfileIsIncomplete`
- Novo: `Handle_ShouldCheckProfile_OnlyAfterCredentialsAreValidated` —
  credenciais inválidas retorna 401 sem nunca chamar
  `FindByUserIdAsync` (garante a ordem da decisão técnica 1).

**Componente (`GastosApp.ComponentTests`):**
- `Support/ComponentTestWebApplicationFactory.cs`:
  `BuildDefaultUserProfileRepositoryMock()` passa a devolver perfil
  completo por padrão (decisão técnica 3); atualizar o comentário que
  hoje descreve o default como "retorna null".
- `Auth/AuthEndpointsTests.cs`:
  - Ajustar `Me_SemPerfilCadastrado_Retorna200ComCamposNulos` para
    configurar `null` explicitamente.
  - Novo: `Login_ComUsuarioSemPerfil_Retorna403ComProfileIncomplete`
    — `AuthServiceMock.LoginAsync` sucesso +
    `UserProfileRepositoryMock.FindByUserIdAsync` retornando `null`;
    espera 403, `problem.type == ".../profile-incomplete"`, e
    `Set-Cookie` **ausente**.
  - Novo: `Login_ComUsuarioSemPerfil_NaoCriaAccountNemAceitaConvite`
    — mesmo arranjo acima; garante
    `AccountRepositoryMock.FindAccountIdByUserIdAsync`/`CreateAsync` e
    `MembershipRepositoryMock.AcceptPendingInvitesByEmailAsync` nunca
    chamados.

**Integrado (`GastosApp.IntegrationTests`):** nenhuma mudança
necessária — `TestAccountFixture` sempre registra via
`POST /auth/register` antes de logar, e esse fluxo já garante perfil
completo desde a FEAT-26; o cenário do bug (usuário criado fora do
`/auth/register`) não é reproduzível pela suíte de integração atual
(sem acesso a `AdminCreateUser` direto no Cognito a partir dos testes)
— cobertura desse cenário fica só no nível de componente, mesmo
padrão já aceito na FEAT-30 para casos que a infra de teste integrado
não alcança.

## Documentação a atualizar

- `backend/docs/openapi.json` — regenerar via
  `backend/scripts/export-openapi.sh` para refletir o novo
  `403 Forbidden` documentado em `POST /auth/login`.
- `backend/docs/backlog.md` — já atualizado no `/specify` (item movido
  de "Bugs" apontando para esta FEAT); nenhuma ação adicional aqui.
- `backend/docs/data-model.md` — sem mudança (nenhum item novo, nenhum
  formato de chave alterado).

## Pontos que precisam de confirmação antes do `/tasks`

1. Nome dos dois novos testes de componente e da estratégia de "default
   esperto" (decisões técnicas 3 e 4) — confirmar que inverter o
   default do mock (em vez de editar cada teste de `Login_*`
   individualmente) é aceitável.
2. `AuthErrors.ProfileIncomplete` como nome do erro/código
   `profile-incomplete` — confirmar que não conflita com nenhuma
   convenção de nomenclatura já usada em outro contexto (buscado no
   código, sem ocorrência prévia).
