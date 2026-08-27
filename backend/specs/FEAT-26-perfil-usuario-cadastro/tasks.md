# Tasks: FEAT-26 — Perfil do usuário no cadastro (nome, telefone, CPF)

- [ ] 1. Criar `Cpf` (`backend/src/GastosApp.Domain/Users/Cpf.cs`) — `IsValid(string digits)`: 11 dígitos numéricos, rejeita sequências com todos os dígitos iguais, valida os 2 dígitos verificadores pelo algoritmo oficial

- [ ] 2. Adicionar `Domain/Users/CpfTests.cs` (`backend/tests/GastosApp.UnitTests/`) — CPF válido conhecido (ex.: `11144477735`) → `true`; dígito verificador alterado → `false`; todos os dígitos iguais (ex.: `11111111111`) → `false`; menos/mais de 11 caracteres ou caractere não numérico → `false`

- [ ] 3. Criar `UserProfile` (`backend/src/GastosApp.Domain/Users/UserProfile.cs`) — entidade com `UserId`/`Name`/`PhoneNumber`/`Cpf`/`CreatedAt`, `Create`/`Restore` (mesmo padrão de `Account`)

- [ ] 4. Criar `IUserProfileRepository` e `CreateUserProfileResult` (`backend/src/GastosApp.Application/Common/Interfaces/IUserProfileRepository.cs`) — `CreateAsync(UserProfile, ct)`, `FindByUserIdAsync(userId, ct)`

- [ ] 5. Adicionar `DeleteAsync(string email, ct)` em `IAuthService` (`backend/src/GastosApp.Application/Common/Interfaces/IAuthService.cs`)

- [ ] 6. Adicionar `AuthErrors.CpfAlreadyExists` (`Error.Conflict("cpf-already-exists", "CPF já cadastrado")`) em `backend/src/GastosApp.Application/Auth/AuthErrors.cs`

- [ ] 7. Reescrever `RegisterUserCommand`/`RegisterUserCommandHandler`/`RegisterUserResult` (`backend/src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommand.cs`) — command ganha `Name`/`PhoneNumber`/`Cpf`; handler injeta `IUserProfileRepository`, remove a validação manual (`if`), cria `UserProfile.Create(...)` após o `SignUp`, e no `catch`/`CpfAlreadyExists` chama `IAuthService.DeleteAsync` (rollback) antes de relançar/retornar falha; `RegisterUserResult.FromEntity(RegisterResult, UserProfile)`

- [ ] 8. Criar `RegisterUserCommandValidator` (`backend/src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommandValidator.cs`) — migra as regras de `email`/`password` já existentes e adiciona `name` (obrigatório, 2-150 caracteres após `Trim()`), `phoneNumber` (obrigatório, só dígitos, 10 ou 11 caracteres), `cpf` (obrigatório, só dígitos, 11 caracteres, `Cpf.IsValid`)

- [ ] 9. Registrar `IValidator<RegisterUserCommand>` em `ApplicationServiceCollectionExtensions` (`backend/src/GastosApp.Application/DependencyInjection/`)

- [ ] 10. Adicionar `Application/Auth/RegisterUserCommandValidatorTests.cs` (`backend/tests/GastosApp.UnitTests/`) — `email`/`password` vazios ou senha curta (casos já cobertos hoje em `RegisterUserCommandHandlerTests`, migrados); `name` vazio, só espaços, menor que 2 ou maior que 150 caracteres; `phoneNumber` vazio, não numérico, com máscara/DDI, ou com menos/mais de 10-11 dígitos; `cpf` vazio, não numérico, fora de 11 dígitos, ou matematicamente inválido; combinação totalmente válida sem erro

- [ ] 11. Reescrever `Application/RegisterUserCommandHandlerTests.cs` (`backend/tests/GastosApp.UnitTests/`, mock `IAuthService` + `IUserProfileRepository`) — remover os casos de validação (migraram pra task 10); sucesso retorna `RegisterUserResult` com nome/telefone/cpf (nome com `Trim()` aplicado); falha do `IAuthService.RegisterAsync` (email duplicado) propaga o erro sem chamar `IUserProfileRepository`; `CreateAsync` retornando `CpfAlreadyExists: true` → `Result.Failure(AuthErrors.CpfAlreadyExists)` e `DeleteAsync` chamado; `CreateAsync` lançando exceção → exceção relançada e `DeleteAsync` chamado antes; sucesso não chama `DeleteAsync`

- [ ] 12. Criar `GetCurrentUserQuery`/`GetCurrentUserQueryHandler`/`UserInfoResult` (`backend/src/GastosApp.Application/Auth/Queries/GetCurrentUser/GetCurrentUserQuery.cs`) — busca `IUserProfileRepository.FindByUserIdAsync`, retorna `UserInfoResult.FromEntity(userId, email, profile)` com `Name`/`PhoneNumber`/`Cpf` nulos se não houver perfil

- [ ] 13. Adicionar `Application/Auth/GetCurrentUserQueryHandlerTests.cs` (`backend/tests/GastosApp.UnitTests/`, mock `IUserProfileRepository`) — perfil encontrado retorna os 3 campos preenchidos; perfil ausente (`null`) retorna os 3 campos `null` sem erro

- [ ] 14. Criar `DynamoDbUserProfileRepository` (`backend/src/GastosApp.Infrastructure/Users/DynamoDbUserProfileRepository.cs`) — `CreateAsync` via `TransactWriteItems` (item `CpfPointer` `PK=CPF#<cpf>`/`SK=CPF#` com `ConditionExpression: attribute_not_exists(PK)`, item `UserProfile` `PK=USER#<userId>`/`SK=PROFILE#`), captura `TransactionCanceledException` do item 0 → `CpfAlreadyExists: true`; `FindByUserIdAsync` via `GetItem`

- [ ] 15. Adicionar `DeleteAsync(string email, ct)` em `CognitoAuthService` (`backend/src/GastosApp.Infrastructure/Auth/CognitoAuthService.cs`) — `AdminDeleteUserAsync` com `UserPoolId` e `Username=email`

- [ ] 16. Adicionar caso de `DeleteAsync` em `Infrastructure/CognitoAuthServiceTests.cs` (`backend/tests/GastosApp.UnitTests/`) — chama `AdminDeleteUserAsync` com `UserPoolId`/`Username` corretos

- [ ] 17. Registrar `IUserProfileRepository` → `DynamoDbUserProfileRepository` em `InfrastructureServiceCollectionExtensions` (`backend/src/GastosApp.Infrastructure/DependencyInjection/`)

- [ ] 18. Atualizar `AuthEndpoints.cs` (`backend/src/GastosApp.Api/Endpoints/`) — `RegisterRequest`/`UserInfoResponse` ganham `Name`/`PhoneNumber`/`Cpf`; `RegisterUser` repassa os 3 campos ao `RegisterUserCommand`; `UserData` (`GET /me`) passa a montar `GetCurrentUserQuery(userId, email)` e enviar via `ISender`, mapeando o `Result` pra `UserInfoResponse` (mantém a checagem de claims/401 já existente antes do `sender.Send`)

- [ ] 19. Adicionar `cognito-idp:AdminDeleteUser` à policy `CognitoAccess` em `backend/infra/terraform/environments/prod/lambda.tf` e `environments/hom/lambda.tf` (aprovado no `/plan`)

- [ ] 20. Adicionar `IUserProfileRepository UserProfileRepositoryMock` + `ResetUserProfileRepositoryMock()` em `ComponentTestWebApplicationFactory` (`backend/tests/GastosApp.ComponentTests/Support/`) — mesmo padrão de `AccountRepositoryMock`/`MembershipRepositoryMock`, registrado em `ConfigureTestServices`

- [ ] 21. Atualizar `Auth/AuthEndpointsTests.cs` (`backend/tests/GastosApp.ComponentTests/`) — chamar `_factory.ResetUserProfileRepositoryMock()` no construtor; estender `Register_ComDadosValidos_Retorna201ComLocationEBody` com `name`/`phoneNumber`/`cpf` no request e no assert do body; estender o `[Theory]` de `Register_ComParametrosInvalidos_Retorna400SemChamarAuthService` com casos de `name`/`phoneNumber`/`cpf` ausentes/inválidos

- [ ] 22. Adicionar `Register_ComCpfJaCadastrado_Retorna409` em `Auth/AuthEndpointsTests.cs` — `UserProfileRepositoryMock.CreateAsync(...)` retorna `CpfAlreadyExists: true`; assert 409, `type=.../errors/cpf-already-exists`, e `AuthServiceMock.Received(1).DeleteAsync(...)`

- [ ] 23. Adicionar `Me_ComPerfilCadastrado_Retorna200ComNomeTelefoneCpf` e `Me_SemPerfilCadastrado_Retorna200ComCamposNulos` em `Auth/AuthEndpointsTests.cs` — `UserProfileRepositoryMock.FindByUserIdAsync(...)` retornando um `UserProfile` e `null`, respectivamente

- [ ] 24. Atualizar `ApplicationExtensionsTests.AddApplicationServices_ShouldNotRegisterAnyOtherValidator_BeyondTheKnownEleven` → `...BeyondTheKnownTwelve`, incluindo `RegisterUserCommandValidator` na lista fechada

- [ ] 25. Rodar `dotnet build backend/GastosApp.sln` e `dotnet test backend/GastosApp.sln` — suíte completa sem regressão

- [ ] 26. Rodar `./scripts/export-openapi.sh` e conferir via `git diff` que só `POST /auth/register` (request/response) e `GET /auth/me` (response) foram alterados em `backend/docs/openapi.json`

- [ ] 27. Marcar os critérios de aceite concluídos em `backend/specs/FEAT-26-perfil-usuario-cadastro/spec.md` e preencher a seção "Status", resumindo o que foi implementado
