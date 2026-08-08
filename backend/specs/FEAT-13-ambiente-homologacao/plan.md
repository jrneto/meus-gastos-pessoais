# Plan — FEAT-13: Ambiente de homologação do backend

## Camadas afetadas

- **Infraestrutura** (`backend/infra/terraform/`) — camada principal
  desta feature: reorganização da config de produção para
  `environments/prod/` e criação de `environments/hom/`.
- **`GastosApp.Infrastructure`** (código .NET) — pequena mudança
  pontual para tornar o caminho do Parameter Store configurável (ver
  "Achado crítico" abaixo). Nenhuma outra camada (`Api` além do ponto
  de composição em `Program.cs`, `Application`, `Domain`) é tocada.
  Nenhum novo endpoint, nenhuma mudança de contrato — `backend/docs/openapi.json`
  não precisa ser regenerado.

## Contexto herdado da spec

Toda a infra de produção já provisionada (`backend/infra/terraform/`,
flat hoje) precisa ser duplicada para um ambiente `hom`, isolado,
exposto em `https://api-hom.jrnexpenses.com`, com custo baixo — ver
`backend/specs/FEAT-13-ambiente-homologacao/spec.md`.

## Achado crítico: caminho do Parameter Store hardcoded

`backend/src/GastosApp.Infrastructure/Configuration/AwsParameterStoreExtensions.cs`
tem hoje:

```csharp
private const string ParameterPath = "/GastosApp/";
```

Esse valor é usado para uma leitura recursiva (`GetParametersByPathAsync`,
`Recursive = true`) de tudo sob `/GastosApp/` — incluindo
`/GastosApp/Cognito/*`. Diferente do nome da tabela DynamoDB
(`DynamoDbOptions.TableName`, que já vem de `IConfiguration` e pode ser
sobrescrito por uma variável de ambiente da Lambda sem tocar em
código, já que não existe hoje nenhum parâmetro `DynamoDb/TableName`
no Parameter Store), o caminho do Parameter Store **não** é
configurável — é uma constante compilada no método.

Sem mudar isso, a Lambda de homologação leria os parâmetros de Cognito
de **produção** (mesmo `/GastosApp/Cognito/UserPoolId` seria retornado
para as duas Lambdas), quebrando o isolamento de autenticação exigido
pela US2 da spec. **Decisão confirmada com o usuário**: pequena mudança
de código para tornar esse caminho configurável via variável de
ambiente, com default idêntico ao valor atual (nenhuma mudança de
comportamento em produção).

### Mudança de código

`AwsParameterStoreExtensions.cs`:

```csharp
public static IConfigurationBuilder AddAwsParameterStore(
    this IConfigurationBuilder builder,
    string path = "/GastosApp/")
{
    using var client = new AmazonSimpleSystemsManagementClient(Amazon.RegionEndpoint.USEast1);

    var values = new Dictionary<string, string?>();
    string? nextToken = null;

    do
    {
        var response = client.GetParametersByPathAsync(new GetParametersByPathRequest
        {
            Path = path,
            Recursive = true,
            WithDecryption = true,
            NextToken = nextToken
        }).GetAwaiter().GetResult();

        foreach (var parameter in response.Parameters)
        {
            var key = parameter.Name[path.Length..].Replace('/', ':');
            values[key] = parameter.Value;
        }

        nextToken = response.NextToken;
    } while (!string.IsNullOrEmpty(nextToken));

    if (values.Count == 0)
    {
        throw new InvalidOperationException(
            $"Nenhum parâmetro encontrado em '{path}' no Parameter Store.");
    }

    return builder.AddInMemoryCollection(values);
}
```

(remove o `const ParameterPath`, substitui todos os usos por `path`)

`Program.cs`:

```csharp
if (!builder.Environment.IsEnvironment("Testing"))
{
    var parameterStorePath = builder.Configuration["ParameterStore:Path"] ?? "/GastosApp/";
    builder.Configuration.AddAwsParameterStore(parameterStorePath);
}
```

`builder.Configuration["ParameterStore:Path"]` lê de fontes já
carregadas por `CreateBuilder` (appsettings, variáveis de ambiente)
antes dessa linha — em produção, sem a variável setada, cai no default
`"/GastosApp/"` (comportamento idêntico ao atual). Em homologação, a
Lambda seta `ParameterStore__Path=/GastosApp/Hom/` (variável de
ambiente, `__` vira `:` na convenção do .NET), isolando completamente
a leitura.

Testes: se existir teste cobrindo `AddAwsParameterStore`, ajustar para
o novo parâmetro opcional — comportamento default não muda.

## Estrutura Terraform: migrar para `environments/{prod,hom}/`

**Decisão confirmada com o usuário.** O frontend já adota
`environments/prod/` + `dns/` (state separado, mesmo bucket) e já
documentou a intenção de que o backend siga o mesmo padrão quando hom
for criado (`frontend/infra/terraform/README.md`: "a config já vive em
`environments/prod/` ... para que uma futura `environments/hom/` não
exija mover state de uma estrutura plana").

A migração da config de produção (hoje flat em
`backend/infra/terraform/`) não recria nem altera nenhum recurso real:
os *endereços* dos resources no state (`aws_dynamodb_table.gastos_app`,
etc.) não mudam — só o diretório do `.tf` e a `key` do backend S3
mudam. Não é necessário `terraform state mv`.

### Arquivos movidos (git mv, sem alterar conteúdo além do listado)

`backend/infra/terraform/{acm,api-gateway-domain,api-gateway,cognito,dns,dynamodb,lambda,outputs,parameter-store,variables,versions}.tf`
→ `backend/infra/terraform/environments/prod/{mesmo nome}.tf`

`bootstrap/` e `README.md` continuam em `backend/infra/terraform/`
(não são por ambiente).

### Ajustes nos arquivos movidos

`environments/prod/versions.tf` — `key` do backend S3 muda de
`gastosapp/terraform.tfstate` para `gastosapp/prod/terraform.tfstate`.

`environments/prod/lambda.tf` — o path do artefato sobe 2 níveis a
mais de diretório:

```hcl
filename         = "${path.module}/../../../lambda/function.zip"
source_code_hash = filebase64sha256("${path.module}/../../../lambda/function.zip")
```

### Passo a passo da migração (cada comando com aprovação explícita)

1. A partir de `backend/infra/terraform/` (ainda flat): `terraform plan`
   — baseline, precisa dar **"No changes."** antes de qualquer
   reorganização. Se não der, parar e resolver primeiro.
2. `git mv` dos 11 arquivos para `environments/prod/` (não é comando
   Terraform, mas ainda uma mudança de repo — aprovação antes).
3. Editar `environments/prod/versions.tf` (nova `key`) e
   `environments/prod/lambda.tf` (novo path do zip).
4. A partir de `environments/prod/`:
   ```bash
   terraform init \
     -backend-config="bucket=gastosapp-terraform-state-648443184523" \
     -backend-config="region=us-east-1" \
     -backend-config="key=gastosapp/prod/terraform.tfstate" \
     -migrate-state
   ```
   Terraform pergunta se quer copiar o state existente para a nova
   `key` — responder `yes`. Não toca em nenhum recurso real, só move
   onde o state é guardado no S3.
5. `terraform plan` de validação — precisa voltar a dar "No changes.".
   Qualquer diff aqui precisa ser investigado antes de prosseguir (não
   aplicar por cima de uma diferença inesperada).
6. Só depois do "No changes" confirmado: remover a `key` antiga
   (`gastosapp/terraform.tfstate`) do bucket S3, via console ou
   `aws s3 rm` — limpeza, evita cópia órfã. Aprovação explícita antes.

## `environments/hom/`: novos arquivos, sem módulo

Só 2 ambientes hoje — copiar os `.tf` de `environments/prod/` com
valores trocados é mais simples e consistente com o que o frontend já
fez (sem extrair módulo). Se um 3º ambiente aparecer no futuro, é o
gatilho natural para reconsiderar.

### `environments/hom/versions.tf`
Mesmo backend S3, `key = "gastosapp/hom/terraform.tfstate"`.

### `environments/hom/variables.tf`
Mesmas variáveis de prod (`aws_region`, `table_name`, `frontend_origins`),
com defaults: `table_name = "GastosApp-Hom"`, `frontend_origins = []`.

### `environments/hom/dynamodb.tf`
Idêntico a prod (PK/SK, GSI1, GSI2, `PAY_PER_REQUEST`), só o
`var.table_name` muda (via default acima).

### `environments/hom/cognito.tf`
```hcl
resource "aws_cognito_user_pool" "main" {
  name = "user-pool-gastos-app-hom"
  # ... mesma password_policy, mfa_configuration, deletion_protection,
  # account_recovery_setting, schema de prod
}

resource "aws_cognito_user_pool_client" "spa" {
  name         = "controle-gastos-spa-hom"
  user_pool_id = aws_cognito_user_pool.main.id
  # ... mesmos explicit_auth_flows, supported_identity_providers,
  # allowed_oauth_flows/scopes de prod

  # Placeholder — não há frontend de homologação ainda (mesma decisão
  # de produção antes de ter frontend integrado). Trocar quando existir
  # um frontend de hom real.
  callback_urls = ["http://localhost:5173"]

  # ... resto idêntico a prod (prevent_user_existence_errors,
  # enable_token_revocation, validity settings)
}
```

### `environments/hom/parameter-store.tf`
```hcl
resource "aws_ssm_parameter" "cognito_user_pool_id" {
  name  = "/GastosApp/Hom/Cognito/UserPoolId"
  type  = "String"
  value = aws_cognito_user_pool.main.id
}

resource "aws_ssm_parameter" "cognito_client_id" {
  name  = "/GastosApp/Hom/Cognito/ClientId"
  type  = "String"
  value = aws_cognito_user_pool_client.spa.id
}

resource "aws_ssm_parameter" "cognito_region" {
  name  = "/GastosApp/Hom/Cognito/Region"
  type  = "String"
  value = var.aws_region
}
```
Sem parâmetros de CORS de produção (não aplicável a hom).

### `environments/hom/lambda.tf`
Mesma estrutura de prod (IAM role, log group, policy, function), com:
- `aws_iam_role.lambda_exec` → `name = "gastos-app-api-lambda-exec-hom"`
- `aws_cloudwatch_log_group.lambda` → `name = "/aws/lambda/gastos-app-api-hom"`, `retention_in_days = 14`
- IAM policy: mesmas actions de prod, `Resource` apontando para os
  recursos de hom (`aws_dynamodb_table.gastos_app.arn` de hom,
  `arn:aws:ssm:...:parameter/GastosApp/Hom/*`,
  `aws_cognito_user_pool.main.arn` de hom)
- `aws_lambda_function.api`:
  ```hcl
  resource "aws_lambda_function" "api" {
    function_name = "gastos-app-api-hom"

    filename         = "${path.module}/../../../lambda/function.zip"
    source_code_hash = filebase64sha256("${path.module}/../../../lambda/function.zip")

    role    = aws_iam_role.lambda_exec.arn
    handler = "bootstrap"
    runtime = "provided.al2023"

    architectures = ["x86_64"]
    memory_size   = 256
    timeout       = 10

    environment {
      variables = {
        ParameterStore__Path = "/GastosApp/Hom/"
      }
    }

    depends_on = [aws_cloudwatch_log_group.lambda]
  }
  ```
  Mesmo artefato físico (`function.zip`) que produção — sem processo de
  build separado, já que é o mesmo código/contrato (a spec exige
  isso). Documentar no README que aplicar em hom e prod usa o mesmo zip
  em disco no momento do `apply` de cada um (sem garantia automática de
  paridade de versão entre os dois sem CI/CD — aceitável para esta
  feature, deploy manual).

### `environments/hom/api-gateway.tf`
Idêntico a prod, exceto:
- `aws_apigatewayv2_api.main` → `name = "gastos-app-api-hom"`
- `cors_configuration.allow_origins = var.frontend_origins` (default
  `[]` — sem frontend de hom, nenhuma origem de browser liberada;
  chamadas via curl/Postman/testes não são afetadas por CORS)

### `environments/hom/acm.tf`
Certificado **novo** (diferente de prod, que foi importado já
`ISSUED` na FEAT-12):
```hcl
resource "aws_acm_certificate" "api_hom" {
  domain_name       = "api-hom.jrnexpenses.com"
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }
}
```

### `environments/hom/dns.tf`
Mesmo padrão de leitura da zona já usado em prod (FEAT-12) — sem
duplicar ou gerenciar a zona do frontend:
```hcl
data "aws_route53_zone" "jrnexpenses" {
  name         = "jrnexpenses.com."
  private_zone = false
}

resource "aws_route53_record" "api_hom_acm_validation" {
  for_each = {
    for dvo in aws_acm_certificate.api_hom.domain_validation_options :
    dvo.domain_name => dvo
  }

  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = each.value.resource_record_name
  type    = each.value.resource_record_type
  ttl     = 300
  records = [each.value.resource_record_value]
}

resource "aws_acm_certificate_validation" "api_hom" {
  certificate_arn         = aws_acm_certificate.api_hom.arn
  validation_record_fqdns = [for r in aws_route53_record.api_hom_acm_validation : r.fqdn]
}

resource "aws_route53_record" "api_hom_a" {
  zone_id = data.aws_route53_zone.jrnexpenses.zone_id
  name    = "api-hom.jrnexpenses.com"
  type    = "A"

  alias {
    name                   = aws_apigatewayv2_domain_name.api_hom.domain_name_configuration[0].target_domain_name
    zone_id                = aws_apigatewayv2_domain_name.api_hom.domain_name_configuration[0].hosted_zone_id
    evaluate_target_health = false
  }
}
```

Diferente de prod: aqui o certificado é novo (não importado), então
precisa do fluxo completo de emissão — `aws_acm_certificate_validation`
faz o Terraform esperar a validação DNS completar (`ISSUED`) antes do
domínio customizado usar o certificado.

### `environments/hom/api-gateway-domain.tf`
```hcl
resource "aws_apigatewayv2_domain_name" "api_hom" {
  domain_name = "api-hom.jrnexpenses.com"

  domain_name_configuration {
    certificate_arn = aws_acm_certificate_validation.api_hom.certificate_arn
    endpoint_type   = "REGIONAL"
    security_policy = "TLS_1_2"
  }
}

resource "aws_apigatewayv2_api_mapping" "api_hom" {
  api_id      = aws_apigatewayv2_api.main.id
  domain_name = aws_apigatewayv2_domain_name.api_hom.id
  stage       = aws_apigatewayv2_stage.default.id
}
```
`certificate_arn` referencia `aws_acm_certificate_validation.api_hom`
(não o certificado direto) — força o Terraform a esperar a validação
DNS terminar antes de associar o domínio.

### `environments/hom/outputs.tf`
Mesmos 4 outputs de prod, com `api_custom_domain_url` apontando para
`https://api-hom.jrnexpenses.com`.

## Ordem de execução — `environments/hom/` (cada comando com aprovação explícita)

1. Escrever todos os `.tf` acima (sem rodar nada ainda).
2. ```bash
   cd environments/hom
   terraform init \
     -backend-config="bucket=gastosapp-terraform-state-648443184523" \
     -backend-config="region=us-east-1" \
     -backend-config="key=gastosapp/hom/terraform.tfstate"
   ```
3. `terraform plan` — revisar a lista completa de recursos **novos**
   (tabela, user pool + client, 3 parâmetros SSM, IAM role + policy,
   log group, Lambda, HTTP API + integração + rota + stage +
   permission, certificado ACM + validação, 2 records DNS, domínio
   customizado + mapping) — aprovação explícita antes do `apply`.
4. `terraform apply` — só após aprovação.
5. Validação manual:
   - `curl -i https://api-hom.jrnexpenses.com/expenses` sem token →
     espera `401`
   - Fluxo completo com usuário de teste temporário: `POST
     /auth/register` → `admin-confirm-sign-up` no user pool de hom →
     `POST /auth/login` → `GET /auth/me` → `GET /expenses` (mesmo
     roteiro da FEAT-12); confirmar que o dado fica na tabela
     `GastosApp-Hom`, não `GastosApp`
   - `admin-delete-user` ao final, sem deixar dado de teste
   - `curl -i https://api.jrnexpenses.com/expenses` sem token → `401`,
     confirmando zero regressão em produção

## Recursos AWS novos (custo)

| Recurso | Custo esperado |
|---|---|
| Tabela DynamoDB `GastosApp-Hom` | Free tier permanente (PAY_PER_REQUEST) |
| Cognito User Pool + Client (hom) | Free tier (até 50 MAU) |
| 3 parâmetros SSM `/GastosApp/Hom/*` | Grátis (Standard tier) |
| Lambda `gastos-app-api-hom` | Free tier permanente (256MB/10s) |
| CloudWatch Log Group (hom, 14 dias) | Marginal, mesmo perfil de prod |
| API Gateway HTTP API (hom) | ~US$1/milhão de requisições, desprezível no volume de teste |
| Certificado ACM `api-hom.jrnexpenses.com` | Grátis |
| Record DNS (A + CNAME validação) | Sem custo incremental (zona já existe) |

Nenhum recurso cobrado por hora ligada.

## Mapeamento de erros de negócio

Não aplicável — nenhum `Command`/`Query`/`Handler`, endpoint ou regra
de negócio é criado ou alterado. A mudança em
`AwsParameterStoreExtensions`/`Program.cs` é configuração interna, sem
`Error`/`ErrorType`/status HTTP novo.

## Documentação a atualizar ao final

- `backend/infra/CLAUDE.md` — nova seção descrevendo
  `environments/prod/` e `environments/hom/`, isolamento total (tabela,
  pool, prefixo SSM, Lambda próprios de cada ambiente), link para esta
  spec.
- `backend/infra/terraform/README.md` — passo a passo reescrito
  apontando para `environments/prod/` e nova seção para
  `environments/hom/`, incluindo a nota do artefato de Lambda
  compartilhado e do `ParameterStore__Path` por ambiente.

## Addendum pós-execução: segundo bug de binding sob Native AOT

Durante a validação end-to-end (após o `apply` de `environments/hom/`),
`/expenses` retornava `500`: a Lambda de hom tentava acessar a tabela
`GastosApp` (produção) em vez de `GastosApp-Hom`, mesmo com a variável
de ambiente `DynamoDb__TableName=GastosApp-Hom` corretamente configurada
na Lambda (confirmado via `aws lambda get-function-configuration`).

Causa raiz: diferente do que este plano assumiu ("nome da tabela já é
isolável sem mudança de código"), `InfrastructureServiceCollectionExtensions.cs`
lia `DynamoDbOptions` via `services.Configure<DynamoDbOptions>(configuration.GetSection(...))`
— o mesmo mecanismo de binding por reflection que já tinha sido
identificado como quebrado sob Native AOT para `CognitoOptions` na
FEAT-10 (comentário em `AddCognitoSdk.cs`: "Configure<T>(IConfiguration)
usa reflection ... e falha silenciosamente sob Native AOT"). Esse
comentário/correção nunca foi replicado para `DynamoDbOptions` — o bug
sempre existiu, só nunca se manifestou porque o default hardcoded
(`"GastosApp"`) coincidia com o nome real da tabela de produção.

Correção aplicada (mesmo padrão já usado para Cognito): trocar
`services.Configure<DynamoDbOptions>(...)` por leitura manual via
`services.AddSingleton(_ => Options.Create(new DynamoDbOptions { TableName = section["TableName"] ?? "GastosApp", ... }))`
em `InfrastructureServiceCollectionExtensions.cs`. Rebuild completo
(`dotnet build`/`test`, artefato Lambda via `infra/lambda/build.sh`) e
reaplicado em `environments/hom/` e `environments/prod/` (mesmo
artefato compartilhado) — validado com `terraform plan` "No changes."
em ambos e teste end-to-end completo (registro, login, criação e
exclusão de despesa em hom, confirmando isolamento real de dados
contra a tabela de produção).

## Pontos que precisam de confirmação antes do `/tasks`

1. Confirmar se existe teste cobrindo `AddAwsParameterStore` hoje — se
   sim, ajustar para a nova assinatura antes de considerar o Passo 1
   concluído.
2. No momento da execução, confirmar credenciais AWS ativas e o nome
   exato do bucket de state (`gastosapp-terraform-state-648443184523`,
   assumido a partir do README existente) antes de qualquer `terraform
   init`/`apply` real.
