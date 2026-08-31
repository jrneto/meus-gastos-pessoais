# FEAT-31: Login bloqueado quando o perfil está incompleto

## Objetivo

Corrigir bug em que `POST /auth/login` autentica normalmente um
usuário sem checar se ele tem perfil completo (`name`, `phoneNumber`,
`cpf`) gravado no DynamoDB — hoje só valida a identidade/senha no
Cognito.

## Contexto

Bug registrado em `backend/docs/backlog.md` (seção "Bugs"), levantado
em 2026-08-31, fora do escopo de qualquer FEAT em andamento.

**Causa raiz:** desde a FEAT-26, `POST /auth/register` exige `name`,
`phoneNumber` e `cpf` e só cria o usuário no Cognito **e** o perfil no
DynamoDB de forma transacional (`RegisterUserCommandHandler` reverte o
`SignUp` se a gravação do perfil falhar) — logo, todo usuário criado
por esse fluxo sempre tem perfil completo. Mas se um administrador
cria o usuário diretamente no Cognito (fora do `/auth/register`,
ex.: console AWS, `AdminCreateUser`) e já confirma o acesso,
`LoginUserCommandHandler`
(`backend/src/GastosApp.Application/Auth/Commands/Login/LoginUserCommand.cs`)
autentica via `IAuthService.LoginAsync` sem nunca consultar
`IUserProfileRepository` — o usuário loga normalmente, recebe tokens
válidos, e usa a API inteira sem nome/telefone/CPF cadastrados,
quebrando a garantia estabelecida na FEAT-26 de que todo usuário ativo
tem perfil completo.

**Decisões fechadas com o usuário durante o `/specify`:**

1. **Escopo**: esta FEAT só bloqueia o login quando o perfil está
   incompleto. Não cria nenhum endpoint para o próprio usuário
   completar o cadastro depois de bloqueado (ex.: `PATCH /users/me`)
   — um usuário bloqueado por este motivo hoje só é destravado por
   intervenção manual (ex.: um administrador grava o perfil dele
   direto no DynamoDB, ou o exclui do Cognito e pede que ele se
   registre de novo por `POST /auth/register`). Criar esse endpoint de
   auto-atendimento fica para uma FEAT futura, caso o usuário decida
   priorizar.
2. **Status code**: o erro segue o padrão já usado no projeto para
   "credenciais corretas, mas acesso negado por outro motivo"
   (`Error.Forbidden`, ex.: `MembershipErrors.InsufficientPermission`)
   — `403 Forbidden`, código `profile-incomplete`. Diferente de
   `AuthErrors.UserNotConfirmed`, que também bloqueia o login mas por
   um motivo do próprio Cognito (email não confirmado); aqui o motivo é
   a ausência de perfil no DynamoDB, uma checagem própria da aplicação.
3. **Definição de "perfil completo"**: como a FEAT-26 só grava o
   `UserProfile` de forma transacional com os três campos
   (`name`/`phoneNumber`/`cpf`) já validados, a existência do registro
   já implica perfil completo — não há caso hoje de perfil
   parcialmente gravado. A checagem no login é simplesmente:
   **existe `UserProfile` para este `userId`?** Se não existir, é
   tratado como perfil incompleto.

## Requisitos de negócio

- `POST /auth/login` passa a checar, depois de `IAuthService.LoginAsync`
  autenticar com sucesso (senha/usuário corretos, usuário confirmado
  no Cognito), se existe `UserProfile` para o `userId` retornado
  (`IUserProfileRepository.FindByUserIdAsync`, já usado por
  `GetCurrentUserQuery`/FEAT-26)
- Se não existir `UserProfile`, o login é bloqueado: nenhum token
  (access/refresh) é emitido, nenhum cookie de refresh token é
  definido, e a API retorna 403 (`profile-incomplete`)
- A checagem de perfil só roda **depois** de senha/usuário serem
  validados — credenciais inválidas continuam retornando 401
  (`invalid-credentials`) antes mesmo de qualquer consulta ao perfil,
  sem mudança de comportamento
- Efeitos colaterais de login que hoje rodam depois da autenticação
  (`EnsureAccountCommand` — garante Account própria, FEAT-19;
  `AcceptPendingInvitesCommand` — aceita convites pendentes, FEAT-20)
  **não devem rodar** quando o login é bloqueado por perfil
  incompleto — o usuário não deve ganhar efeitos colaterais de conta
  antes de ter um cadastro completo
- Usuários com perfil completo (fluxo normal via `POST /auth/register`)
  continuam logando exatamente como hoje — sem qualquer mudança de
  comportamento ou de contrato para esse caso

## User Stories

**US1 — Usuário com perfil completo loga normalmente**
- Given um usuário registrado via `POST /auth/register` (perfil
  completo: `name`, `phoneNumber`, `cpf` gravados)
- When ele chama `POST /auth/login` com email e senha corretos
- Then a API retorna 200 com `accessToken`, `expiresIn` e `userId`,
  exatamente como hoje — sem mudança de comportamento

**US2 — Usuário criado diretamente no Cognito, sem perfil, é bloqueado**
- Given um usuário criado diretamente no Cognito (ex.:
  `AdminCreateUser`, fora do `POST /auth/register`), já confirmado, e
  sem nenhum `UserProfile` gravado no DynamoDB
- When ele chama `POST /auth/login` com email e senha corretos
- Then a API retorna 403 (`profile-incomplete`), sem emitir tokens e
  sem definir o cookie de refresh token

**US3 — Credenciais inválidas continuam tendo prioridade sobre a checagem de perfil**
- Given um usuário qualquer (com ou sem perfil completo)
- When ele chama `POST /auth/login` com senha incorreta
- Then a API retorna 401 (`invalid-credentials`), como já acontecia
  antes desta feature — a checagem de perfil nunca chega a rodar

**US4 — Login bloqueado por perfil incompleto não garante Account nem aceita convites**
- Given um usuário sem `UserProfile` (cenário da US2), com convites de
  conta pendentes para o seu email
- When ele chama `POST /auth/login` com credenciais corretas
- Then a API retorna 403 (`profile-incomplete`), nenhuma Account é
  criada para ele e nenhum convite pendente é aceito — esses efeitos
  só acontecem em login bem-sucedido

## Contratos da API

### POST /auth/login

Request e responses de sucesso (200) e credenciais inválidas (401)
continuam idênticos aos já documentados (`backend/docs/openapi.json`,
FEAT-01/FEAT-15).

Response 403 (perfil incompleto — novo nesta feature):
```json
{
  "type": "https://gastosapp.dev/errors/profile-incomplete",
  "title": "Acesso negado",
  "status": 403,
  "detail": "Cadastro incompleto. Este usuário não possui perfil (nome, telefone e CPF) cadastrado."
}
```

## Critérios de aceite

- [x] `POST /auth/login` com credenciais corretas e perfil completo
      (`UserProfile` existente) continua retornando 200 com os mesmos
      campos de hoje (US1)
- [x] `POST /auth/login` com credenciais corretas e **sem**
      `UserProfile` retorna 403 (`profile-incomplete`), sem emitir
      tokens e sem definir cookie de refresh token (US2)
- [x] `POST /auth/login` com senha incorreta continua retornando 401
      (`invalid-credentials`) antes de qualquer checagem de perfil,
      com ou sem perfil completo (US3)
- [x] Login bloqueado por perfil incompleto não dispara
      `EnsureAccountCommand` nem `AcceptPendingInvitesCommand` (US4)
- [x] Novo endpoint/campo de erro coberto por teste de componente
- [x] `backend/docs/openapi.json` regenerado refletindo o novo status
      code 403 de `POST /auth/login`
- [x] Suíte completa de testes (unitário + componente) passando
- [x] `backend/docs/backlog.md` atualizado: item "BUG — Login não
      exige perfil completo..." sai da seção "Bugs" e passa a apontar
      para esta FEAT

## Status

Implementado conforme `plan.md`/`tasks.md`. `LoginUserCommandHandler`
ganhou a dependência `IUserProfileRepository` (já registrada desde a
FEAT-26, sem mudança de DI) e, logo após `IAuthService.LoginAsync`
autenticar com sucesso, consulta `FindByUserIdAsync(userId)`; se
retornar `null`, o login é bloqueado com o novo
`AuthErrors.ProfileIncomplete` (`Error.Forbidden`, 403,
`profile-incomplete`) — antes de qualquer efeito colateral
(`EnsureAccountCommand`/`AcceptPendingInvitesCommand`). Nenhuma mudança
em Domain/Infrastructure/DynamoDB — reuso total do que já existia
desde a FEAT-26.

`AuthEndpoints.MapAuthEndpoints` passou a documentar
`.ProducesProblem(StatusCodes.Status403Forbidden)` em `POST /auth/login`.

**Estratégia de teste (decisão técnica 3/4 do `plan.md`):** o "default
esperto" de `IUserProfileRepository` nos testes de componente
(`ComponentTestWebApplicationFactory.BuildDefaultUserProfileRepositoryMock`)
foi invertido — `FindByUserIdAsync` passou a devolver um `UserProfile`
completo por padrão, em vez de `null`. Isso evitou editar os ~6 testes
de `Login_*` que já esperavam 200; só `Me_SemPerfilCadastrado_Retorna200ComCamposNulos`
precisou passar a configurar `null` explicitamente. Mesma estratégia
aplicada localmente em `LoginUserCommandHandlerTests` (mock configurado
no construtor da classe de teste, sem tocar os 12 testes já
existentes).

Suíte completa: 473 testes unitários + 207 de componente passando
(680/680), mais 3/3 testes integrados (`AuthFlowTests`) rodados
localmente via `backend/infra/lambda/run-local.sh` (binário Native AOT
publicado, via Runtime Interface Emulator) — confirma que o fluxo
normal de registro+login continua funcionando com a checagem de
perfil. `backend/docs/openapi.json` regenerado — `git diff` confirma
que só o `403` de `POST /auth/login` mudou.

## Fora do escopo

- Endpoint para o usuário completar o próprio perfil depois de
  bloqueado (ex.: `PATCH /users/me`) — usuário bloqueado por este
  motivo continua dependendo de intervenção manual (decisão fechada
  com o usuário, ver "Contexto"); pode virar uma FEAT futura
- Qualquer mudança no fluxo de `POST /auth/register` (já exige perfil
  completo desde a FEAT-26) ou no `PostConfirmation` trigger do
  Cognito
- Qualquer mudança de contrato em `GET /auth/me`, `POST /auth/refresh`
  ou `POST /auth/logout`
- Migração/backfill de usuários hoje sem perfil — eles simplesmente
  passam a ser bloqueados no próximo login, sem ação automática da API
