# Plan — FEAT-29: Testes integrados multiambiente + gate de CI/CD

Não introduz nenhuma regra de negócio, `Command`/`Query`, endpoint ou
mudança de contrato — é infraestrutura de teste e pipeline. Por isso
este plano não tem seção de "Mapeamento de erros" (nenhum `Error`/
`ErrorType` novo) nem PK/SK de escrita de negócio — só leitura/limpeza
administrativa do modelo já documentado em `backend/docs/data-model.md`.

## Camadas afetadas

| Camada | O que muda |
|---|---|
| `tests/GastosApp.IntegrationTests` | Deixa de ser esqueleto (`UnitTest1.cs` vazio); ganha cliente HTTP próprio (não referencia mais `GastosApp.Api`), testes do módulo Auth, e utilitários de setup/cleanup via SDK AWS |
| `backend/infra/lambda/` | Novo Dockerfile/estágio pra rodar o binário Native AOT publicado como container consultável localmente (imagem base real da Lambda + Runtime Interface Emulator), e um script `run-local.sh` |
| `.github/workflows/backend-deploy-hom.yml` | Novo job `integration-tests` entre `deploy` e `draft-release` |
| `.github/workflows/backend-deploy-prod.yml` | Novo job `check-hom-integration-tests` antes de `quality` |
| `.github/workflows/backend-integration-tests-prod.yml` | Novo workflow, só `workflow_dispatch` |
| `backend/infra/terraform/cicd/iam-policy.tf` | Permissões IAM novas na role `gastosapp-backend-cicd` — **requer aprovação explícita antes de aplicar** (ver seção "Recursos AWS") |
| `backend/docs/constitution.md` | Nova regra: endpoint novo exige teste integrado, além do teste de componente |
| `backend/docs/backlog.md` | Débito técnico: módulos ainda sem teste integrado (categorias, transações, membros, resumo, relatórios, export, perfil) |
| `backend/CLAUDE.md` / `backend/infra/CLAUDE.md` | Estrutura de testes e seção CI/CD atualizadas |

Nenhuma camada de produção (`Api`/`Application`/`Domain`/`Infrastructure`)
muda.

## Contratos técnicos detalhados

### `GastosApp.IntegrationTests` — reformulação do projeto

- Remove `<ProjectReference Include="...GastosApp.Api.csproj" />` do
  `.csproj` (herdado do esqueleto original, FEAT-03) — os testes são
  **black-box** via HTTP; não referenciam código de produção nem
  reaproveitam `IAuthService`/repositórios internos. DTOs de
  request/response são records próprios do projeto de teste,
  espelhando `backend/docs/openapi.json` (fonte de verdade do
  contrato).
- Pacotes novos: `AWSSDK.CognitoIdentityProvider`,
  `AWSSDK.DynamoDBv2` (mesmas libs já usadas em
  `GastosApp.Infrastructure`) — usados **só** pelos utilitários de
  setup/cleanup administrativo (`AdminConfirmSignUp`,
  `AdminDeleteUser`, `Query`/`DeleteItem`/`BatchWriteItem`), nunca
  pelo fluxo de negócio testado (que é sempre via HTTP, como um
  cliente real da API).
- Estrutura de pastas:
  ```
  tests/GastosApp.IntegrationTests/
  ├── Support/
  │   ├── IApiTransport.cs            # abstração de transporte (ver abaixo)
  │   ├── DirectHttpTransport.cs      # hom/prod: HttpClient puro
  │   ├── LambdaRieTransport.cs       # local: via Runtime Interface Emulator
  │   ├── TestAccountFixture.cs       # setup/cleanup da conta de teste
  │   └── IntegrationTestEnvironment.cs  # lê env vars, resolve modo/URLs/credenciais
  └── Auth/
      └── AuthFlowTests.cs           # register + confirm + login (primeiro módulo)
  ```
  Convenção pra próximos módulos (categorias, transações, etc., hoje
  no backlog): `<Modulo>/<Modulo>FlowTests.cs`, reaproveitando
  `TestAccountFixture` para a conta de teste.

### Abstração de transporte HTTP

O mesmo teste roda contra três alvos fisicamente diferentes — uma API
HTTPS normal (hom/prod) ou um container Lambda acessível só via
protocolo de invocação do Runtime Interface Emulator (local). Os
testes chamam sempre a mesma interface; o modo (`INTEGRATION_TESTS_MODE`)
decide a implementação:

```csharp
public interface IApiTransport
{
    Task<TransportResponse> SendAsync(
        HttpMethod method, string path, object? body = null,
        string? bearerToken = null, CancellationToken ct = default);
}

public sealed record TransportResponse(
    int StatusCode, string Body, IReadOnlyDictionary<string, string> Headers);
```

- **`DirectHttpTransport`** (`hom`/`prod`): `HttpClient` com
  `BaseAddress` = `https://api-hom.jrnexpenses.com` ou
  `https://api.jrnexpenses.com`. Serializa `body`, monta
  `Authorization: Bearer <token>` quando `bearerToken` é informado,
  devolve status/corpo/headers crus.
- **`LambdaRieTransport`** (`local`): monta um
  `APIGatewayHttpApiV2ProxyRequest` (payload format 2.0 — mesmo
  formato configurado em produção via `LambdaEventSource.HttpApi`,
  `Program.cs:79`) a partir de `method`/`path`/`body`/headers,
  serializa como JSON e faz
  `POST http://localhost:9000/2015-03-31/functions/function/invocations`
  (endpoint padrão do Runtime Interface Emulator — RIE), desserializa
  a `APIGatewayHttpApiV2ProxyResponse` de volta (decodifica Base64 se
  `isBase64Encoded=true`) pra `TransportResponse`. Erros de
  cold start/timeout do container aparecem aqui como falha de teste,
  não como erro genérico de rede — é exatamente esse caminho que
  expõe erro de AOT (ex.: exceção na inicialização do host, que na
  Lambda real apareceria só nos logs do CloudWatch).

### Container local (Native AOT + Runtime Interface Emulator)

- Novo Dockerfile `backend/infra/lambda/Dockerfile.local-run`,
  reaproveitando o estágio `build` já existente em
  `Dockerfile.build` (mesmo `dotnet publish -r linux-x64
  --self-contained -p:PublishAot=true`, mesma base `amazonlinux:2023`
  pra evitar o erro de GLIBC já documentado). Estágio final:
  ```dockerfile
  FROM public.ecr.aws/lambda/provided:al2023 AS local-run
  COPY --from=build /app/publish/GastosApp.Api ${LAMBDA_TASK_ROOT}/bootstrap
  COPY --from=build /app/publish/appsettings.json ${LAMBDA_TASK_ROOT}/appsettings.json
  ```
  `public.ecr.aws/lambda/provided:al2023` é a imagem oficial da AWS
  pro runtime `provided.al2023` — a mesma família de base do que roda
  a Lambda de produção/homologação hoje.
- **RIE não é embutido na imagem publicada** (essa imagem
  `local-run` nunca é usada em deploy real — só localmente): o
  binário `aws-lambda-rie` (release oficial da AWS) é baixado uma vez
  (cache em `backend/infra/lambda/.rie/`, versão pinada) e montado via
  volume no `docker run`, sobrescrevendo o entrypoint — padrão
  documentado pela própria AWS pra testar imagens de runtime
  customizado localmente sem alterar o artefato de produção:
  ```bash
  docker run --rm -p 9000:8080 \
    -v "$(pwd)/infra/lambda/.rie/aws-lambda-rie:/aws-lambda/aws-lambda-rie" \
    --entrypoint /aws-lambda/aws-lambda-rie \
    --network gastosapp-local \
    -e DynamoDb__ServiceURL=http://gastosapp-localstack:4566 \
    -e DynamoDb__AccessKey=test -e DynamoDb__SecretKey=test \
    -e Cognito__ServiceURL=http://gastosapp-cognito-local:9229 \
    ... \
    gastosapp-api-local-run /var/runtime/bootstrap
  ```
- Variáveis de ambiente do container = as mesmas hoje declaradas em
  `appsettings.Development.json` (`DynamoDb:ServiceURL`,
  `AccessKey`/`SecretKey`, `ParameterStore:*`, `Cognito:*` local) — só
  que como env vars (`Secao__Chave`), já que o binário publicado não
  lê `appsettings.Development.json` (esse arquivo só existe hoje
  porque `dotnet run` local carrega por `ASPNETCORE_ENVIRONMENT`; o
  container roda o binário publicado direto). Precisa da rede Docker
  nomeada (`gastosapp-local`, criada em `docker-compose.yml`) pra
  resolver `gastosapp-localstack`/`gastosapp-cognito-local` pelo nome
  do container.
- Script único `backend/infra/lambda/run-local.sh` (mesmo princípio
  do "um comando" já em `build.sh`/FEAT-18): builda a imagem
  `local-run`, garante `docker compose up -d` (LocalStack +
  cognito-local) se ainda não estiverem no ar, baixa o RIE se
  necessário, sobe o container, aguarda health-check (primeiro invoke
  de warm-up), roda
  `dotnet test tests/GastosApp.IntegrationTests -c Release` com
  `INTEGRATION_TESTS_MODE=local`, e desliga o container ao final
  (sempre, mesmo se os testes falharem).

### Setup e limpeza da conta de teste (Cognito + DynamoDB)

**Setup** (`TestAccountFixture`, roda no início de cada execução):
1. `POST /auth/register` (fluxo real do produto, via `IApiTransport`)
   com e-mail único por execução (ex.:
   `int-test+{Guid:N}@jrnexpenses.com`) e CPF sintético válido (dígito
   verificador correto, gerado por execução — evita colidir com o
   `CpfPointer` de execuções anteriores).
2. `AdminConfirmSignUpAsync` (SDK Cognito direto —
   `UserPoolId`+`Username=email`) pra confirmar sem depender de e-mail
   real. **A confirmar durante a implementação**: se `cognito-local`
   não suportar essa chamada, o fallback é habilitar
   `AutoConfirmUser` no `config.json` do `cognito-local` (só afeta o
   ambiente local, não hom/prod).
3. `POST /auth/login` (via `IApiTransport`) pra obter o `accessToken`
   usado pelos testes.

**Cleanup** (`DisposeAsync`, roda sempre — sucesso ou falha):
1. `Query PK=USER#<userId>` na tabela → `AccountPointer`
   (`SK=ACCOUNT#`, resolve o `AccountId`) e `UserProfile`
   (`SK=PROFILE#`, se existir — FEAT-26); apaga os dois.
2. `Query PK=ACCOUNT#<accountId>` → todos os itens da conta
   (`Account`, `Membership` Titular, as 13 categorias padrão da
   FEAT-28, e qualquer `Category`/`Transaction` que o próprio teste
   tenha criado); apaga tudo via `BatchWriteItem`.
3. `DeleteItem PK=CPF#<cpf>, SK=CPF#` (item `CpfPointer`, FEAT-26).
4. `AdminDeleteUserAsync` no Cognito (`UserPoolId`+`Username=email`) —
   mesma operação que `CognitoAuthService.DeleteAsync` já faz hoje
   como rollback de registro (`username_attributes=["email"]`), só
   que chamada direto via SDK no teste, não reaproveitando
   `IAuthService` de produção (o teste é black-box de propósito).

### Configuração do runner (env vars do job de CI)

`UserPoolId` e o nome da tabela **não são duplicados como novo
GitHub Environment variable** — são resolvidos em runtime pelo próprio
teste via `GetParametersByPath` no Parameter Store
(`/GastosApp/Cognito/UserPoolId` em prod,
`/GastosApp/Hom/Cognito/UserPoolId` em hom — mesmo prefixo já usado
pela aplicação, `AwsParameterStoreExtensions`), usando as credenciais
OIDC já configuradas no job (`aws-actions/configure-aws-credentials`).
Isso evita ter duas fontes de verdade pro mesmo valor. Env vars
passadas ao `dotnet test`:
- `INTEGRATION_TESTS_MODE` — `local` \| `hom` \| `prod`
- `INTEGRATION_TESTS_BASE_URL` — só pra `hom`/`prod`
- `INTEGRATION_TESTS_PARAMETER_STORE_PATH` — `/GastosApp/` (prod) ou
  `/GastosApp/Hom/` (hom), reaproveitando a mesma variável que a
  aplicação já usa (`ParameterStore__Path`)

## Recursos AWS usados/afetados

**Nenhum recurso novo é criado** (nenhuma tabela, User Pool, Lambda ou
API Gateway novos). A única mudança de infraestrutura é **permissão
IAM adicional** na role já existente `gastosapp-backend-cicd`
(`backend/infra/terraform/cicd/iam-policy.tf`), usada **só** pelos
jobs de teste integrado em CI (nunca pela Lambda da aplicação, cujo
conjunto de permissões não muda):

- `cognito-idp:AdminConfirmSignUp`, `cognito-idp:AdminDeleteUser` —
  escopadas aos ARNs dos User Pools de hom e prod (nunca `*`)
- `ssm:GetParametersByPath` — já deve existir pra deploy hoje; a
  confirmar se cobre os caminhos usados (`AwsParameterStoreExtensions`)
- `dynamodb:Query`, `dynamodb:DeleteItem`, `dynamodb:BatchWriteItem` —
  escopadas aos ARNs das tabelas `GastosApp-Hom`/`GastosApp` (nunca
  `*`)

⚠️ **Isso é infraestrutura AWS com implicação de segurança — nenhuma
mudança em `iam-policy.tf` é aplicada (`terraform apply`) sem
aprovação explícita sua**, tratada como etapa própria no `/tasks`
(revisão do diff do Terraform antes de aplicar, ambiente por
ambiente).

## Mudanças nos workflows (detalhamento)

### `backend-deploy-hom.yml` — novo job `integration-tests`
Entre `deploy` e `draft-release`:
```yaml
integration-tests:
  needs: deploy
  runs-on: ubuntu-latest
  environment: backend-hom
  steps:
    - uses: actions/checkout@v4
    - uses: actions/setup-dotnet@v4
      with: { dotnet-version: '10.0.x' }
    - uses: aws-actions/configure-aws-credentials@v4
      with: { role-to-assume: ${{ vars.CICD_ROLE_ARN }}, aws-region: us-east-1 }
    - run: dotnet test tests/GastosApp.IntegrationTests -c Release
      env:
        INTEGRATION_TESTS_MODE: hom
        INTEGRATION_TESTS_BASE_URL: https://api-hom.jrnexpenses.com
        INTEGRATION_TESTS_PARAMETER_STORE_PATH: /GastosApp/Hom/
```
`draft-release` passa a ter `needs: [deploy, integration-tests]` (hoje
é só `needs: deploy`).

### `backend-deploy-prod.yml` — novo job `check-hom-integration-tests`
Entre `check-changes` e `quality`:
```yaml
check-hom-integration-tests:
  needs: check-changes
  if: needs.check-changes.outputs.changed == 'true'
  runs-on: ubuntu-latest
  steps:
    - uses: actions/checkout@v4
      with: { ref: ${{ env.RELEASE_TAG }} }
    - name: Verificar teste integrado de hom bem-sucedido para este commit
      env: { GH_TOKEN: ${{ secrets.GITHUB_TOKEN }} }
      run: |
        set -euo pipefail
        commit_sha=$(git rev-parse HEAD)
        conclusion=$(gh run list --workflow backend-deploy-hom.yml \
          --json headSha,conclusion,status \
          --jq "[.[] | select(.headSha==\"$commit_sha\" and .status==\"completed\")] | sort_by(.conclusion) | last | .conclusion // \"\"")
        if [ "$conclusion" != "success" ]; then
          echo "Nenhuma execução BEM-SUCEDIDA de backend-deploy-hom.yml (job integration-tests incluso) para o commit $commit_sha."
          exit 1
        fi
        echo "OK: backend-deploy-hom.yml passou (com teste integrado) para $commit_sha."
```
Nota: `gh run list --json conclusion` reflete o resultado do workflow
inteiro — como `draft-release` já depende de `integration-tests`, um
workflow `backend-deploy-hom.yml` com `conclusion=success` já implica
que o teste integrado passou; não precisa inspecionar o job
individualmente. `quality` passa a ter
`needs: [check-changes, check-hom-integration-tests]` (hoje é só
`needs: check-changes`).

### `backend-integration-tests-prod.yml` (novo)
```yaml
name: Backend — Teste Integrado (Produção)
on:
  workflow_dispatch:
permissions:
  id-token: write
  contents: read
defaults:
  run:
    working-directory: backend
jobs:
  integration-tests:
    runs-on: ubuntu-latest
    environment: backend-prod
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with: { dotnet-version: '10.0.x' }
      - uses: aws-actions/configure-aws-credentials@v4
        with: { role-to-assume: ${{ vars.CICD_ROLE_ARN }}, aws-region: us-east-1 }
      - run: dotnet test tests/GastosApp.IntegrationTests -c Release
        env:
          INTEGRATION_TESTS_MODE: prod
          INTEGRATION_TESTS_BASE_URL: https://api.jrnexpenses.com
          INTEGRATION_TESTS_PARAMETER_STORE_PATH: /GastosApp/
```
Sem `paths:`/`push:` — só aparece na aba Actions do GitHub pra disparo
manual, exatamente o "lugar que dá pra acessar do git sem rodar a
pipeline inteira" pedido na spec.

## Mudanças em `dotnet test GastosApp.sln`

`GastosApp.IntegrationTests` continua incluído na `.sln`, mas **não**
deve rodar como parte do `dotnet test GastosApp.sln` genérico usado
nos jobs `quality` (exige Docker/rede, não é o caso de
unitário+componente). Abordagem: usar uma xUnit
[trait]/categoria (`[Trait("Category", "Integration")]`) e excluir via
`--filter "Category!=Integration"` nos jobs `quality` existentes — sem
mudar o comando hoje usado, só adicionar o filtro. Os jobs novos
(`integration-tests` em hom/prod) chamam
`dotnet test tests/GastosApp.IntegrationTests` diretamente, sem
filtro.

## Pontos que precisam de confirmação antes do `/tasks`

1. **Aprovação da mudança de IAM** (`cognito-idp:AdminConfirmSignUp`/
   `AdminDeleteUser` + `dynamodb:Query`/`DeleteItem`/`BatchWriteItem`
   na role `gastosapp-backend-cicd`) — sem custo, mas é alteração de
   segurança; preciso do seu "ok" explícito antes de qualquer
   `terraform apply`, tratado como etapa isolada no `/tasks`.
2. **`cognito-local` suportar `AdminConfirmSignUp`/`AdminDeleteUser`** —
   assumido como suportado (a lib emula boa parte da API Admin do
   Cognito); se não suportar na prática, o fallback
   (`AutoConfirmUser` no `config.json`) só afeta o ambiente local.
3. **Versão pinada do `aws-lambda-rie`** a baixar/cachear — vou fixar
   a última versão estável no momento da implementação, documentada
   no `Dockerfile.local-run`/`run-local.sh`.
4. **Nome exato dos caminhos no Parameter Store** para
   `Cognito:UserPoolId` — confirmar contra
   `AwsParameterStoreExtensions`/Terraform durante a implementação
   (assumido `/GastosApp/Cognito/UserPoolId` e
   `/GastosApp/Hom/Cognito/UserPoolId` neste plano).
