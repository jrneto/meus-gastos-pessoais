# Plan — FEAT-37: E-mail de boas-vindas

## Camadas afetadas

| Camada | O que muda |
|---|---|
| **Application** | Nova interface `IWelcomeEmailSender` (`Common/Interfaces/`). `EnsureAccountCommandHandler` passa a chamar essa interface, defensivamente, quando a conta é criada pela primeira vez (`AlreadyExisted: false`). |
| **Infrastructure** | Nova classe `SesWelcomeEmailSender` (implementa `IWelcomeEmailSender`, compõe `IEmailSender` + `IUserProfileRepository`), novo `WelcomeEmailTemplateProvider` (mesmo padrão de `PasswordChangedEmailTemplateProvider`), template `04-boas-vindas.html` copiado (com domínio corrigido) e embarcado como `EmbeddedResource`. Registro em `AddSesSdk`. |
| **CognitoTriggers** (Lambda de trigger de conta) | Nenhuma mudança de código — `AccountTriggerHandler` continua só despachando `EnsureAccountCommand`; o envio de e-mail fica encapsulado dentro do handler do Command (Application), não no trigger em si. Já usa `AddInfrastructure`, que já registra tudo que a nova dependência precisa. |
| **Domain** | Nenhuma mudança. |
| **Api** | Nenhuma mudança — feature não expõe nem altera endpoint HTTP. |
| **Terraform** (`backend/infra/terraform/environments/{hom,prod}`) | Variável de ambiente nova `Ses__SenderEmail` em `lambda-account-trigger.tf`. Nenhuma mudança de IAM (permissão já concedida na FEAT-33). |

## Contratos técnicos detalhados

### `IWelcomeEmailSender` (novo, `GastosApp.Application/Common/Interfaces/IWelcomeEmailSender.cs`)

```csharp
namespace GastosApp.Application.Common.Interfaces;

// Composto sobre IEmailSender + IUserProfileRepository — resolve nome
// (com fallback) e monta o HTML por dentro do Infrastructure, o
// Application só sabe "mandar o boas-vindas pra esse usuário/email".
public interface IWelcomeEmailSender
{
    Task SendAsync(string userId, string email, CancellationToken cancellationToken = default);
}
```

Mesma forma de `IPasswordChangedEmailSender` (FEAT-36): a Application
não conhece o template nem placeholders, só dispara pelo caso de uso.

### `EnsureAccountCommandHandler` (alterado, `Accounts/Commands/EnsureAccount/EnsureAccountCommand.cs`)

```csharp
public sealed class EnsureAccountCommandHandler : ICommandHandler<EnsureAccountCommand, Result<EnsureAccountResult>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly IWelcomeEmailSender _welcomeEmailSender;
    private readonly ILogger<EnsureAccountCommandHandler> _logger;

    public EnsureAccountCommandHandler(
        IAccountRepository accountRepository,
        IWelcomeEmailSender welcomeEmailSender,
        ILogger<EnsureAccountCommandHandler> logger)
    { ... }

    public async ValueTask<Result<EnsureAccountResult>> Handle(EnsureAccountCommand command, CancellationToken cancellationToken)
    {
        var existingAccountId = await _accountRepository.FindAccountIdByUserIdAsync(command.UserId, cancellationToken);
        if (existingAccountId is not null)
            return Result.Success(new EnsureAccountResult(existingAccountId, AlreadyExisted: true));

        var created = await _accountRepository.CreateAsync(command.UserId, command.Email, cancellationToken);

        if (!created.AlreadyExisted)
        {
            try
            {
                await _welcomeEmailSender.SendAsync(command.UserId, command.Email, cancellationToken);
            }
            catch (Exception ex)
            {
                // Nunca propaga: a conta já foi criada de fato (spec.md,
                // requisito de negócio) — falha no envio deste e-mail não
                // pode derrubar EnsureAccountCommand. Mesma filosofia
                // defensiva do ResetPasswordCommandHandler (FEAT-36).
                _logger.LogError(ex, "Falha ao enviar email de boas-vindas para o usuário {UserId}.", command.UserId);
            }
        }

        return Result.Success(new EnsureAccountResult(created.AccountId, created.AlreadyExisted));
    }
}
```

`created.AlreadyExisted` (retornado por `IAccountRepository.CreateAsync`)
já cobre o caso de corrida (dois triggers concorrentes resolvendo pra a
mesma conta) — só o branch que efetivamente ganhou a criação (`false`)
dispara o e-mail, o outro (`true`, vencedor de outra invocação) não.
Nenhuma mudança em `IAccountRepository`/`DynamoDbAccountRepository`.

**Por que aqui e não em `AccountTriggerHandler`:** mesmo padrão já
usado por `ResetPasswordCommandHandler` (FEAT-36) — o disparo do
e-mail e seu próprio try/catch defensivo vivem dentro do Command
Handler (Application), não no chamador. `AccountTriggerHandler`
continua com seu try/catch amplo em volta de todo o `sender.Send(...)`
(defesa em profundidade — mesmo se o try/catch interno do handler
falhasse por algum motivo não prevido, o de fora ainda segura),
sem precisar saber que agora existe um e-mail sendo enviado.

### `SesWelcomeEmailSender` (novo, `GastosApp.Infrastructure/Email/SesWelcomeEmailSender.cs`)

```csharp
public sealed class SesWelcomeEmailSender : IWelcomeEmailSender
{
    private const string Subject = "Bem-vindo ao jrn.expenses"; // igual ao <title>

    private readonly IEmailSender _emailSender;
    private readonly IUserProfileRepository _profileRepository;

    public SesWelcomeEmailSender(IEmailSender emailSender, IUserProfileRepository profileRepository)
    { ... }

    public async Task SendAsync(string userId, string email, CancellationToken cancellationToken = default)
    {
        var profile = await _profileRepository.FindByUserIdAsync(userId, cancellationToken);

        // Substitui a sentença inteira (não só o valor de {{nome}}) pra
        // sempre fechar a frase corretamente com ou sem nome — ver
        // spec.md, requisito de negócio / US2.
        var greeting = profile is null ? "(a)." : $", {profile.Name}.";
        var html = WelcomeEmailTemplateProvider.Template
            .Replace(", {{nome}}.", greeting)
            .Replace("{{email}}", email);

        await _emailSender.SendAsync(email, Subject, html, cancellationToken);
    }
}
```

Nota de implementação: o texto exato no template é
`Bem-vindo, {{nome}}.` — a substituição busca a substring
`", {{nome}}."` (vírgula + espaço + placeholder + ponto), não só
`{{nome}}`, exatamente pra poder virar `"Bem-vindo(a)."` (sem vírgula
solta) quando não há nome. Se o template mudar essa pontuação no
futuro, a substituição precisa acompanhar — mesmo tipo de acoplamento
textual que já existe nos outros `*TemplateProvider`/`*EmailSender`
(substituição de string simples, sem template engine).

### `WelcomeEmailTemplateProvider` (novo, `GastosApp.Infrastructure/Email/WelcomeEmailTemplateProvider.cs`)

Idêntico em estrutura a `PasswordChangedEmailTemplateProvider`, só
troca o nome do arquivo carregado (`04-boas-vindas.html`).

### `AddSesSdk` (alterado, `GastosApp.Infrastructure/Extensions/AddSesSdk.cs`)

Acrescenta uma linha:
```csharp
services.AddScoped<IWelcomeEmailSender, SesWelcomeEmailSender>();
```
Registrado incondicionalmente (mesmo padrão de `IPasswordChangedEmailSender`)
— disponível tanto na Lambda da API quanto na Lambda de trigger de
conta, já que as duas chamam `AddInfrastructure`.

### Template — cópia + correção de domínio

`backend/src/GastosApp.Infrastructure/Email/Templates/04-boas-vindas.html`
é uma cópia de `frontend/design-system/emails/04-boas-vindas.html`
(mesma decisão da FEAT-34/36: duplicar em vez de referenciar por
caminho relativo, pra não acoplar o build do backend à árvore do
frontend), com os 3 pontos de domínio corrigidos:
- `https://app.jrnexpenses.com.br/dashboard` → `https://app.jrnexpenses.com/dashboard`
- `https://app.jrnexpenses.com.br/preferencias` → `https://app.jrnexpenses.com/preferencias`
- `suporte@jrnexpenses.com.br` → `suporte@jrnexpenses.com`

**A correção de domínio é aplicada nos dois lugares** — no arquivo
fonte (`frontend/design-system/emails/04-boas-vindas.html`, pra não
deixar a divergência documentada como "certa" no design system) e na
cópia embarcada no backend. `.csproj` ganha:
```xml
<EmbeddedResource Include="Email\Templates\04-boas-vindas.html" />
```

### DynamoDB

Nenhum acesso novo além do já existente: `IUserProfileRepository.
FindByUserIdAsync` já faz `GetItem` por `PK=USER#<userId>`,
`SK=PROFILE#` (chave primária, sem GSI) — mesmo access pattern já
usado por `GetCurrentUserQuery`. Nenhuma mudança de schema, PK/SK ou
GSI.

## Decisões técnicas relevantes

1. **E-mail disparado dentro do `EnsureAccountCommandHandler`
   (Application), não dentro de `AccountTriggerHandler` (o próprio
   trigger/composition root da Lambda)** — mantém a mesma fronteira já
   estabelecida pela FEAT-36 (`ResetPasswordCommandHandler`): o Command
   Handler é quem decide se um efeito colateral de e-mail acontece após
   o caso de uso principal ter sucesso, com seu próprio try/catch
   defensivo. `AccountTriggerHandler` continua simples, sem precisar
   saber que um e-mail passou a ser enviado.
2. **Nome resolvido via `IUserProfileRepository`, dentro do
   `SesWelcomeEmailSender` (Infrastructure), não no Application** —
   mesma separação já usada por `SesPasswordChangedEmailSender`: a
   Application só entrega `userId`/`email`, quem sabe montar o HTML
   (incluindo buscar dados auxiliares pro template) é a Infrastructure.
3. **Fallback sem nome faz `.Replace(", {{nome}}.", "(a).")`** — decisão
   já fechada no `/specify` (US2). Evita um `if` ramificado no template
   (não existe template engine no projeto) mantendo uma frase
   gramaticalmente correta nos dois casos.
4. **Correção do domínio do template feita nos dois arquivos** (design
   system + cópia embarcada) — evita a mesma divergência se propagar
   pra uma eventual FEAT futura que reutilize o arquivo do design
   system como referência visual.
5. **`Ses__SenderEmail` só em Terraform, sem mudança de CI/CD** —
   confirmado que `backend-deploy-account-trigger-{hom,prod}.yml` só
   chama `aws lambda update-function-code` (nunca
   `update-function-configuration`); variáveis de ambiente dessa Lambda
   vêm inteiramente do bloco `environment{}` do próprio
   `lambda-account-trigger.tf`. Diferente da Lambda da API (que tem
   `APP_VERSION`/`APP_COMMIT_SHA`/`APP_ENVIRONMENT` mesclados pelo
   workflow em cima do que o Terraform já define), aqui basta o
   `.tf` — sem necessidade de tocar nenhum workflow.
6. **Mesmo literal fixo do `parameter-store.tf`, não referência ao
   atributo ao vivo do Cognito** — reaproveita a lição da FEAT-36
   (`email_configuration` do User Pool devolve encoding MIME diferente
   do literal do `.tf` quando o texto tem acento, gerando diff
   perpétuo em qualquer apply sem `-target`). `Ses__SenderEmail` recebe
   o mesmo texto literal já usado nos 2 parâmetros SSM
   (`"jrn.expenses <no-reply@jrnexpenses.com>"` em prod,
   `"jrn.expenses (homologação) <no-reply@hom.jrnexpenses.com>"` em
   hom).
7. **`EnsureAccountCommandHandlerTests` existentes precisam de
   `IWelcomeEmailSender`/`ILogger` mockados** no construtor — os 3
   testes atuais continuam válidos (nenhum comportamento de
   `EnsureAccountCommand` em si muda), só o setup do construtor cresce.
   Novos testes cobrem: e-mail disparado quando `AlreadyExisted: false`,
   e-mail **não** disparado quando `AlreadyExisted: true` (idempotência
   e corrida), e falha do `IWelcomeEmailSender` não propaga.

## Recursos AWS usados/afetados

- **Nenhum recurso novo.** Reaproveita a identidade SES já verificada
  (FEAT-33), a permissão IAM `ses:SendEmail`/`ses:SendRawEmail` já
  concedida à role `account_trigger_lambda_exec` (FEAT-33, confirmado
  durante o `/specify`), e a mesma tabela DynamoDB.
- **Mudança em recurso existente**: `aws_lambda_function.
  account_trigger` (hom e prod) ganha uma variável de ambiente nova
  (`Ses__SenderEmail`) no bloco `environment{}` de
  `lambda-account-trigger.tf`. Sem custo, sem mudança de superfície de
  segurança (mesmo valor já público no remetente de outros e-mails) —
  mesmo assim, **exige `terraform apply` em cada ambiente**, então
  segue a mesma regra de aprovação explícita do usuário antes de
  aplicar (ver `backend/infra/CLAUDE.md`), confirmada ao final deste
  plano.

## Mapeamento de erros

Não aplicável — esta feature não introduz nenhum endpoint HTTP nem
novo tipo de `Error`/`ErrorType`. Qualquer falha (perfil não encontrado,
SES indisponível, exceção inesperada) é absorvida dentro do próprio
`SendAsync`/`try-catch` do handler e apenas logada — nunca vira um
`Result.Failure` nem afeta o retorno de `EnsureAccountCommand`
(sempre `Result.Success`, igual antes desta feature).

## Testes

- **Unitário** (`GastosApp.UnitTests`):
  - `EnsureAccountCommandHandlerTests`: 3 testes existentes ajustados
    (novo mock no construtor) + 3 novos (dispara e-mail só na criação
    nova; não dispara quando já existia; falha no envio não propaga
    nem muda o resultado).
  - `SesWelcomeEmailSenderTests` (novo, mesma pasta/padrão de
    `SesPasswordChangedEmailSenderTests`): monta HTML certo com perfil
    encontrado, monta HTML de fallback sem perfil, chama
    `IEmailSender.SendAsync` com destinatário/assunto certos.
  - `AccountTriggerHandlerTests`: sem mudança esperada (o handler em si
    não muda — só o que o `EnsureAccountCommand` faz por trás).
- **Componente** (`GastosApp.ComponentTests`): não aplicável — feature
  não introduz endpoint HTTP.
- **Integrado** (`GastosApp.IntegrationTests`): **não viável como teste
  automatizado**, mesma limitação já aceita para o restante de
  `AccountTriggerHandler`/`EnsureAccountCommand` desde a FEAT-19 — a
  suíte é black-box contra a API (Lambda `GastosApp.Api`), nunca invoca
  a Lambda de trigger de conta (invocada pelo Cognito via
  `PostConfirmation`, fora do fluxo HTTP). Confirmado que nenhum teste
  integrado hoje cobre `AccountTriggerHandler` — a única forma de
  simular localmente é `AccountTriggerHandlerManualDebug.cs` (manual,
  `Skip` fixo, já documentado como tal). Este plano usa o mesmo recurso
  (com breakpoint/log) pra validar manualmente em ambiente local antes
  da feature ser considerada concluída, sem virar teste automatizado
  novo — mesma ressalva explícita que `spec.md` já antecipa no critério
  de aceite correspondente.
- **Canário de DI**: nenhum validator novo (`EnsureAccountCommand` não
  tem `IValidator` — não é acionado via HTTP/`ValidationBehavior`), não
  deve afetar `ApplicationExtensionsTests`.

## Ordem sugerida de implementação (para o `/tasks`)

1. Corrigir domínio em `frontend/design-system/emails/04-boas-vindas.html`.
2. Copiar o template corrigido para
   `backend/src/GastosApp.Infrastructure/Email/Templates/04-boas-vindas.html`
   + `EmbeddedResource` no `.csproj`.
3. `WelcomeEmailTemplateProvider`.
4. `IWelcomeEmailSender` (Application) + `SesWelcomeEmailSender`
   (Infrastructure) + registro em `AddSesSdk`.
5. Alterar `EnsureAccountCommandHandler` (injeção + disparo condicional
   + try/catch).
6. Testes unitários (handler + sender).
7. `Ses__SenderEmail` em `lambda-account-trigger.tf` (hom, depois prod)
   — `terraform apply`, com aprovação explícita antes de cada um.
8. Validação manual local via `AccountTriggerHandlerManualDebug.cs`
   (LocalStack + cognito-local já no ar).
9. Deploy (push em `develop` dispara
   `backend-deploy-account-trigger-hom.yml`) e validação em hom (e-mail
   chegando de verdade, remetente/assunto/link corretos).

## Pontos que precisam de confirmação do usuário antes do `/tasks`

1. **Aprovação para os 2 `terraform apply`** (hom e prod) que
   adicionam `Ses__SenderEmail` ao `environment{}` da Lambda de
   trigger — mudança de infraestrutura existente, sem custo e sem
   mudança de superfície de segurança, mas ainda assim sob a regra de
   aprovação explícita do projeto.
2. **Confirmar o texto de fallback "Bem-vindo(a)."** como aceitável
   (já validado na íntegra no `/specify`, só reconfirmando antes de
   virar código) — outra opção seria omitir a vírgula e ponto e usar
   algo como "Bem-vindo!" com pontuação própria, mas isso mudaria a
   pontuação do H1 fora do padrão atual do template.
