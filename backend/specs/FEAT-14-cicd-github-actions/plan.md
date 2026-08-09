# Plan — FEAT-14: Esteira de CI/CD (GitHub Actions) para o backend

Referência direta de padrão técnico: workflows já validados do frontend
(`.github/workflows/frontend-deploy-hom.yml`,
`frontend-deploy-prod.yml`, `frontend-feature-pr.yml`) e
`frontend/infra/terraform/cicd/` (OIDC Provider + IAM Role). Este plano
replica a mesma estrutura, adaptada ao stack .NET/Lambda do backend.

## Camadas afetadas

- **Api** (`GastosApp.Api`): novo endpoint `GET /health`, não
  autenticado, expondo a versão publicada (rastreabilidade — US3 da
  spec). Novo arquivo `Endpoints/HealthEndpoints.cs`, registrado em
  `Program.cs` junto dos demais `Map*Endpoints()`, **antes** de
  `app.UseAuthorization()` não é necessário alterar — o endpoint só
  precisa não exigir `[Authorize]`/policy, igual aos demais endpoints
  que já usam `RequireAuthorization()` seletivamente (ver
  `AuthEndpoints.cs`).
- **Application** (`GastosApp.Application`): novo
  `GetHealthQuery`/`GetHealthQueryHandler`, seguindo o padrão Mediator
  já estabelecido (rota só faz `sender.Send`, nunca chama handler
  direto). Sem `IValidator` (query sem input, nada a validar).
- **Domain**: sem alteração — `HealthResponse` não é uma entidade de
  domínio, é um DTO de infraestrutura de deploy (versão/commit), então
  não usa o padrão `FromEntity` (esse padrão é só para records
  construídos a partir de entidades de domínio).
- **Infrastructure**: sem alteração de código. A versão/commit chegam
  via variáveis de ambiente da Lambda (`APP_VERSION`,
  `APP_COMMIT_SHA`), lidas do `IConfiguration` padrão do ASP.NET Core
  (env vars já viram configuração automaticamente, sem precisar de
  `AddAwsParameterStore` — não é segredo, não pertence ao Parameter
  Store).
- **CI/CD** (novo, fora das 4 camadas): 3 workflows GitHub Actions —
  `backend-deploy-hom.yml`, `backend-deploy-prod.yml`,
  `backend-feature-pr.yml` — espelhando 1:1 a estrutura dos 3 workflows
  do frontend.
- **Infra/Terraform** (novo): `backend/infra/terraform/cicd/` — IAM
  Role dedicada ao backend, reaproveitando o OIDC Provider já existente
  (criado para o frontend, é um recurso único por conta AWS, não por
  contexto).

## Contratos técnicos

### `GetHealthQuery` / `GetHealthQueryHandler`

```csharp
// Application/Features/Health/GetHealthQuery.cs
public sealed record GetHealthQuery : IQuery<Result<HealthResponse>>;

// Application/Features/Health/HealthResponse.cs
public sealed record HealthResponse(
    string Status,        // sempre "ok" (endpoint só responde se o processo está de pé)
    string Version,       // ex.: "v1.4.0" (prod) ou "dev-<shortSha>" (hom) — igual à
                           // convenção já usada no frontend (VITE_APP_VERSION)
    string CommitSha,     // sha completo do commit publicado
    string Environment);  // "hom" | "prod" | "local" — de onde a Lambda está rodando

// Application/Features/Health/GetHealthQueryHandler.cs
public sealed class GetHealthQueryHandler(IConfiguration configuration)
    : IQueryHandler<GetHealthQuery, Result<HealthResponse>>
{
    public ValueTask<Result<HealthResponse>> Handle(GetHealthQuery query, CancellationToken ct)
    {
        var response = new HealthResponse(
            Status: "ok",
            Version: configuration["APP_VERSION"] ?? "local",
            CommitSha: configuration["APP_COMMIT_SHA"] ?? "unknown",
            Environment: configuration["APP_ENVIRONMENT"] ?? "local");

        return ValueTask.FromResult(Result.Success(response));
    }
}
```

- `GastosApp.Api/Endpoints/HealthEndpoints.cs`: `app.MapGet("/health", ...)`
  → `sender.Send(new GetHealthQuery(), ct)` → 200 sempre (sem
  possibilidade de falha de negócio; não usa `ResultHttpExtensions`
  além do caminho de sucesso).
- Nenhum acesso a DynamoDB/Cognito — handler só lê `IConfiguration`.

### Endpoint HTTP

| Método | Rota | Auth | Response 200 |
|---|---|---|---|
| GET | `/health` | Não | `{ "status": "ok", "version": "v1.4.0", "commitSha": "abc123...", "environment": "prod" }` |

- Adicionado a `backend/docs/openapi.json` via
  `./scripts/export-openapi.sh` (regra imutável da constitution: toda
  mudança de contrato regenera o OpenAPI).
- Teste de componente obrigatório (`GastosApp.ComponentTests`),
  cobrindo os 3 valores possíveis de `APP_ENVIRONMENT` via
  `WebApplicationFactory` + override de configuração.

## Decisões técnicas

### 1. Deploy da Lambda: `aws lambda update-function-code`, não `terraform apply`
O artefato (`function.zip`, gerado por `infra/lambda/build.sh` — build
Native AOT em container Amazon Linux 2023, Docker já vem instalado nos
runners `ubuntu-latest`) é publicado diretamente via
`aws lambda update-function-code --function-name <nome> --zip-file fileb://infra/lambda/function.zip`,
seguido de `aws lambda wait function-updated` antes de seguir. Mesmo
princípio já usado no frontend (workflow mexe direto no S3/CloudFront,
nunca roda `terraform apply` em CI) — evita dar ao pipeline permissão
ampla de Terraform (que tocaria toda a infra: DynamoDB, Cognito,
API Gateway) só para atualizar código.

**Trade-off aceito (pré-existente, não introduzido por esta feature):**
o `source_code_hash` em `lambda.tf` (calculado do `function.zip` local)
só reflete o artefato de quem rodou `terraform apply` por último. Isso
já era verdade no fluxo 100% manual de hoje; a esteira não piora nem
resolve — só automatiza a parte de publicar o zip, sem tocar no state
do Terraform.

### 2. Variáveis de versão (`APP_VERSION`, `APP_COMMIT_SHA`, `APP_ENVIRONMENT`) fora do Terraform
Definidas a cada deploy via
`aws lambda update-function-configuration --environment "Variables={...}"`
(merge com as variáveis já existentes — `ParameterStore__Path`,
`DynamoDb__TableName` continuam vindo do `environment{}` do
`lambda.tf`, não são tocadas por esse comando se usarmos
`get-function-configuration` + merge antes de aplicar, para não
sobrescrever as demais). **Essas 3 chaves não devem ser declaradas no
bloco `environment{}` de `lambda.tf`** — se fossem, um `terraform apply`
manual futuro as resetaria/apagaria a cada execução, competindo com o
que a esteira acabou de publicar.

### 3. IAM Role dedicada ao backend, reaproveitando o OIDC Provider do frontend
`backend/infra/terraform/cicd/` (nova config, mesmo padrão de
`frontend/infra/terraform/cicd/`, state próprio no bucket compartilhado
— `key = gastosapp-backend/cicd/terraform.tfstate`):
- **Reaproveita** `aws_iam_openid_connect_provider.github` já criado
  na conta (`arn:aws:iam::648443184523:oidc-provider/token.actions.githubusercontent.com`)
  via `data` source — não cria um segundo Provider (é um recurso único
  por conta/URL de emissor, não por contexto/app).
- Cria uma **Role nova**, `gastosapp-backend-cicd`, com trust policy
  restrita aos GitHub Environments do backend (ver decisão 4):
  `repo:jrneto/meus-gastos-pessoais:environment:backend-hom` e
  `:environment:backend-prod`.
- Policy inline mínima: `lambda:UpdateFunctionCode`,
  `lambda:UpdateFunctionConfiguration`, `lambda:GetFunction`,
  `lambda:GetFunctionConfiguration` — escopadas só às duas ARNs de
  função (`gastos-app-api`, `gastos-app-api-hom`), nenhum outro recurso
  da conta.
- Mesmo gap conhecido esperado do frontend (perfil `agent-toolkit` sem
  permissão de `iam:CreateRole`/`PutRolePolicy` — a confirmar): se
  `apply` falhar por `AccessDenied`, o código fica como referência e a
  Role é criada manualmente no console pelo usuário, mesmo processo
  documentado em `frontend/infra/terraform/README.md`, seção "cicd/".

### 4. Dois novos GitHub Environments: `backend-hom` e `backend-prod`
Não reaproveita os Environments `hom`/`prod` já usados pelo frontend —
cada um deles hoje guarda variáveis (`BUCKET_NAME`, `DISTRIBUTION_ID`,
`CICD_ROLE_ARN`) que são por-contexto; reusar o mesmo nome exigiria que
as duas roles diferentes (frontend e backend) competissem pela mesma
chave `CICD_ROLE_ARN`. `backend-hom`/`backend-prod` guardam:
`CICD_ROLE_ARN` (Role da decisão 3) e `FUNCTION_NAME`
(`gastos-app-api-hom` / `gastos-app-api`).
**Requer aprovação explícita do usuário** para criar os 2 Environments
e cadastrar as variáveis (mesma regra de custo/segurança já vigente).

### 5. Convenção de tag para não colidir com o versionamento do frontend
O frontend já usa `vX.Y.Z` (`v0.1.0`, `v0.1.1`, `v0.1.2` publicadas).
Como é o mesmo repositório, o backend usa um **prefixo distinto**:
`backend-vX.Y.Z`. Consequência em cada workflow:
- `backend-deploy-prod.yml` dispara em `release: published`, mas com
  `if: startsWith(github.event.release.tag_name, 'backend-v')` no job
  `deploy` — sem esse guard, uma release do frontend (`vX.Y.Z`)
  dispararia (e falharia, ou pior, publicaria código errado) o pipeline
  de prod do backend, e vice-versa.
- `backend-deploy-hom.yml` (job `draft-release`) filtra
  `gh release list` por `tagName` iniciando com `backend-v` — tanto
  para achar a última release publicada (patch bump) quanto para achar
  um rascunho pendente a substituir. **Sem esse filtro, o job herdado
  1:1 do frontend apagaria o rascunho pendente do frontend** (o script
  atual do frontend pega "o primeiro draft" sem filtro).
- ⚠️ **Mudança necessária no workflow já existente do frontend**
  (`frontend-deploy-hom.yml`, job `draft-release`): hoje ele também não
  filtra por prefixo — assim que a primeira release `backend-v*` for
  criada, o job do frontend passaria a arriscar apagá-la ou usá-la como
  referência de "última publicada" para o patch bump. Precisa passar a
  filtrar `tagName` que **não** comece com `backend-v` (ex.: regex
  `^v[0-9]`). Fora do escopo original da spec (que é sobre o backend),
  mas é uma correção obrigatória para as duas esteiras conviverem sem
  se atropelar — **sinalizado para confirmação do usuário antes de
  implementar** (ver seção final).

### 6. Gate de qualidade
`dotnet build GastosApp.sln -c Release` + `dotnet test GastosApp.sln`
(inclui `UnitTests` e `ComponentTests`; `IntegrationTests` continua
esqueleto não usado, ver FEAT-03). Runner `ubuntu-latest`, setup via
`actions/setup-dotnet@v4` (`dotnet-version: '10.0.x'`), cache do NuGet
via `cache: true` do próprio setup-dotnet.

### 7. Gatilhos e paths (mesma lógica do frontend, adaptada)

| Workflow | Gatilho | Paths | Job final |
|---|---|---|---|
| `backend-feature-pr.yml` | `push` em `FEAT-*` | `backend/**` | abre PR → `develop` |
| `backend-deploy-hom.yml` | `push` em `develop` | `backend/**` | deploy hom + draft-release |
| `backend-deploy-prod.yml` | `release: published` | (sem filtro de path — mesma razão do frontend: a release já é o gate) + `if: startsWith(tag_name, 'backend-v')` | deploy prod + PR → `main` |

`backend-feature-pr.yml` roda em qualquer branch `FEAT-*` (mesmo padrão
`branches: ['FEAT-*']` do frontend) — não colide com o workflow do
frontend porque o filtro de `paths` (`backend/**` vs `frontend/app/**`)
é mutuamente exclusivo por PR/push real.

### 8. PR automático `develop → main`: convivência com o job do frontend
`open-pr-main` do backend usa a mesma lógica idempotente (`gh pr list
--head develop --base main --state open`) — como o par de branches é
literalmente o mesmo (`develop`/`main`) usado pelo frontend, o
`if [ "$existing" -eq 0 ]` já garante que, se o PR já foi aberto pelo
lado do frontend, o backend não duplica (e vice-versa). Nenhuma mudança
necessária no job do frontend para este ponto (diferente do item 5,
que é específico de releases/tags).

## Recursos AWS usados ou afetados

- **Nenhum recurso novo de dados/aplicação** (sem tabela, índice,
  Cognito App Client ou parâmetro de Parameter Store novos).
- **Novo**: IAM Role `gastosapp-backend-cicd` (decisão 3) — exige
  aprovação explícita antes de criar/aplicar.
- **Reaproveitado, não criado**: OIDC Provider do GitHub Actions já
  existente na conta.
- **Alterado (fora do Terraform)**: variáveis de ambiente da Lambda
  (`APP_VERSION`, `APP_COMMIT_SHA`, `APP_ENVIRONMENT`) passam a ser
  geridas pelo pipeline via `update-function-configuration`, não mais
  ausentes/manuais.
- **Configuração de GitHub** (não é recurso AWS, mas também exige
  aprovação): 2 novos Environments (`backend-hom`, `backend-prod`) e
  suas variáveis; confirmar se "Allow GitHub Actions to create and
  approve pull requests" já está habilitada (deveria estar, desde a
  FEAT-10 do frontend) — se sim, nada a fazer aqui.

## Mapeamento de erros de negócio

Não aplicável — `/health` não tem caminho de falha de negócio (sempre
200 enquanto o processo Lambda estiver de pé; se a Lambda não
responder, o erro é infraestrutural — timeout/5xx do API Gateway — não
um `Result` de falha do domínio).

## Pontos que precisavam confirmação do usuário — todos confirmados

1. **Prefixo de tag `backend-v*`** (decisão 5) — **confirmado**.
2. **Alterar `frontend-deploy-hom.yml` (job `draft-release`)** para
   filtrar por prefixo (decisão 5) — **confirmado**: o `tasks.md` deve
   incluir essa alteração no workflow já em produção do frontend,
   filtrando `tagName` que não comece com `backend-v` (ex.: regex
   `^v[0-9]`) tanto na busca da última release publicada quanto na
   busca de rascunho pendente a substituir.
3. **2 novos GitHub Environments** (`backend-hom`, `backend-prod`) e a
   IAM Role `gastosapp-backend-cicd` (decisões 3 e 4) — **aprovação
   confirmada**; se o `apply` do Terraform falhar por `AccessDenied`
   (mesmo padrão do gap já documentado no frontend), a criação manual
   no console segue o mesmo processo de
   `frontend/infra/terraform/README.md`, seção "cicd/".
4. **Nome/rota do endpoint de versão**: **`GET /health`, confirmado**.
