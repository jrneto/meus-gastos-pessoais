# FEAT-10: Plano Técnico — Deploy Lambda (Native AOT) + API Gateway

## Camadas afetadas

- **Api (`GastosApp.Api.csproj`, `Program.cs`)**: habilitar `PublishAot`,
  registrar o hosting da Lambda. Nenhuma mudança de rota/contrato.
- **Infrastructure (`AwsParameterStoreExtensions.cs`)**: correção
  necessária — hoje força `AWSOptions.Profile = "default"`
  incondicionalmente; dentro da Lambda não existe profile nomeado, as
  credenciais vêm da IAM Role via cadeia padrão do SDK. É preciso tornar
  esse `Profile` condicional ao ambiente (só usar em desenvolvimento
  local).
- **Infrastructure (`AddCognitoSdk.cs`, DynamoDB client)**: nenhuma
  mudança necessária — já não fixam `Profile`, funcionam com IAM Role
  automaticamente (`AmazonCognitoIdentityProviderClient(config)` /
  `AmazonDynamoDBClient(region)` sem credenciais explícitas).
- **Infrastructure (`backend/infra/terraform/`)**: novos arquivos
  `lambda.tf` e `api-gateway.tf`.
- **Novo**: script/Dockerfile de build do artefato Native AOT
  (`backend/infra/scripts/` ou raiz do repo — a definir na
  implementação), usado apenas no momento do deploy.
- **Nenhuma mudança** em `Application`/`Domain` — regra de negócio
  intocada.

## Decisões técnicas

### Empacotamento: Zip custom runtime, não container image
Duas formas de rodar Native AOT em Lambda: (a) pacote `.zip` com runtime
customizado `provided.al2023` (o binário AOT vira o executável
`bootstrap`), ou (b) imagem de container publicada no ECR. Escolhido
**(a) zip com `provided.al2023`**: mais simples, sem precisar provisionar
um repositório ECR (evita mais um recurso AWS com custo por
armazenamento, mesmo que pequeno) e é o padrão recomendado da AWS para
Lambdas .NET AOT simples como esta.

### Build via container Docker efêmero (sem publicar imagem)
Native AOT não faz cross-compilation de verdade — compilar no Windows
não produz um binário Linux. O Docker (já instalado) roda um container
efêmero (imagem base oficial da AWS para build .NET Lambda,
`public.ecr.aws/sam/build-dotnet8` ou equivalente .NET 10 quando
disponível) que executa
`dotnet publish -c Release -r linux-x64 --self-contained -p:PublishAot=true`
dentro do container, gera o executável `bootstrap`, que é copiado para
fora do container e zipado (`function.zip`). Esse `.zip` é o artefato
que o Terraform sobe para a Lambda. Nenhuma imagem é publicada em
lugar nenhum — o container só existe durante o build, é descartado
depois.

### Arquitetura: `linux-x64` (não `arm64`)
Lambda ARM64 (Graviton) é ~20% mais barato e geralmente mais rápido, mas
buildar para `arm64` a partir de um host Windows/x64 exige emulação
(QEMU via Docker Buildx), mais lento e com mais chance de fricção. Dado
o volume de uso (2 pessoas) a diferença de custo é irrelevante — prioriza-se
simplicidade do build. `linux-x64` fica como padrão; migrar para
`arm64` é uma otimização futura opcional, fora do escopo aqui.

### Sem autorizador JWT no API Gateway (autenticação continua só na aplicação)
O API Gateway poderia validar o JWT antes de invocar a Lambda
(`aws_apigatewayv2_authorizer` tipo JWT, apontando pro Cognito). Optado
por **não adicionar** essa camada: a aplicação já valida o JWT
integralmente (`AddJwtBearer` + `RequireAuthorization()`, FEAT-01), e
duplicar a validação no Gateway aumentaria a configuração sem ganho
real (mesmo emissor/audience), com risco de as duas validações
divergirem no futuro. Rota `ANY /{proxy+}` simplesmente repassa tudo
para a Lambda, que decide autenticação/autorização como já faz hoje.
**Ponto a confirmar com o usuário** — ver seção final.

### Correção de bug de credenciais no Parameter Store
`AwsParameterStoreExtensions.AddAwsParameterStore()`
(`backend/src/GastosApp.Infrastructure/Configuration/AwsParameterStoreExtensions.cs`)
hoje sempre define `AWSOptions.Profile = "default"`. Isso funciona em
desenvolvimento local (perfil AWS CLI configurado), mas dentro da
Lambda não existe esse profile — precisa ficar `null`/omitido para a
cadeia padrão de credenciais (IAM Role) assumir. Ajuste: só definir
`Profile = "default"` quando a aplicação não estiver rodando dentro de
uma Lambda (detectável pela variável de ambiente
`AWS_LAMBDA_FUNCTION_NAME`, presente automaticamente no runtime Lambda
e ausente localmente).

### Verificação de compatibilidade AOT como primeiro passo prático
Antes de escrever qualquer Terraform, o primeiro passo da implementação
é rodar o build Native AOT via Docker e testar localmente (dentro do
container ou via emulador Lambda) se a aplicação sobe sem erro. Riscos
conhecidos a observar: `System.Text.Json` sem `JsonSerializerContext`
(serialização baseada em reflection pode gerar warnings/erros de
trimming), pacotes só usados atrás de `IsDevelopment()`
(`Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`) ainda são
analisados estaticamente pelo trimmer mesmo se nunca executados em
produção. A biblioteca `Mediator` (martinothamar) já é source-generated
(compatível com AOT por design) — não é um risco. Caso apareçam erros
de trimming, resolver caso a caso (specific `JsonSerializerContext` para
os DTOs expostos, `<TrimmerRootAssembly>` pontual, ou — só como último
recurso, combinado com o usuário — cair para publish sem AOT).

## Contratos técnicos

### `GastosApp.Api.csproj`
```xml
<PublishAot>true</PublishAot>
<InvariantGlobalization>true</InvariantGlobalization>
```
(`RuntimeIdentifier` **não** fica fixo no `.csproj` — é passado via `-r
linux-x64` só no comando de publish dentro do container, para não
afetar `dotnet build`/`dotnet test` normais no Windows)

### `Program.cs`
Adicionar, após os demais `builder.Services.Add...`:
```csharp
builder.Services.AddAWSLambdaHosting(LambdaEventSource.HttpApi);
```
Esse método é um no-op fora do ambiente Lambda — `dotnet run` local
continua idêntico a hoje (Kestrel puro), sem branch condicional manual
necessário.

### `AwsParameterStoreExtensions.cs`
```csharp
var isRunningInLambda = !string.IsNullOrEmpty(
    Environment.GetEnvironmentVariable("AWS_LAMBDA_FUNCTION_NAME"));

source.AwsOptions = new AWSOptions
{
    Profile = isRunningInLambda ? null : "default",
    Region = Amazon.RegionEndpoint.USEast1
};
```

### Dockerfile de build (novo arquivo, ex. `backend/infra/lambda/Dockerfile.build`)
Multi-stage simplificado: imagem base com SDK .NET compatível com
Native AOT + toolchain de compilação nativa Linux, roda `dotnet publish`
com `-r linux-x64 --self-contained -p:PublishAot=true -o /out`, artefato
final é só o executável `bootstrap` dentro de `/out`.

### Script de empacotamento (novo, ex. `backend/infra/lambda/build.sh` ou `.ps1`)
1. `docker build` usando o Dockerfile acima
2. Extrai `/out/bootstrap` do container para o host
3. Zipa como `function.zip` (arquivo único `bootstrap` na raiz do zip —
   requisito do runtime customizado)

### `backend/infra/terraform/lambda.tf` (novo)
- `aws_iam_role "lambda_exec"`: trust policy para `lambda.amazonaws.com`
- `aws_iam_role_policy "lambda_exec"` (least privilege, scoped por ARN):
  - `dynamodb:PutItem`, `GetItem`, `Query`, `DeleteItem`,
    `TransactWriteItems` em `aws_dynamodb_table.gastos_app.arn` e
    `${arn}/index/*`
  - `ssm:GetParametersByPath` em
    `arn:aws:ssm:{region}:{account}:parameter/GastosApp/*`
  - `cognito-idp:SignUp`, `InitiateAuth`, `GetUser` em
    `aws_cognito_user_pool.main.arn`
  - `logs:CreateLogStream`, `PutLogEvents` no log group da própria
    função (política gerenciada padrão
    `AWSLambdaBasicExecutionRole` cobre isso, ou policy equivalente
    escrita à mão para manter tudo explícito)
- `aws_cloudwatch_log_group "lambda"`: nome
  `/aws/lambda/{function_name}`, `retention_in_days = 14` (15 não é
  aceito pelo CloudWatch Logs; 14 é o valor válido mais próximo) — criado
  explicitamente para a Lambda usar em vez de deixá-la criar sozinha
  sem retenção
- `aws_lambda_function "api"`:
  - `function_name = "gastos-app-api"`
  - `filename`/`source_code_hash` apontando para o `function.zip`
    gerado pelo script de build
  - `runtime = "provided.al2023"`, `handler = "bootstrap"`,
    `architectures = ["x86_64"]`
  - `memory_size = 256`, `timeout = 10`
  - `environment.variables`: nenhuma variável nova necessária — a
    aplicação já lê tudo do Parameter Store em runtime

### `backend/infra/terraform/api-gateway.tf` (novo)
- `aws_apigatewayv2_api "main"`: `protocol_type = "HTTP"`,
  `cors_configuration` com `allow_origins = [var.frontend_origin]`
  (nova variável, placeholder até o domínio do Angular existir, mesma
  lógica do `callback_urls` do Cognito na FEAT-09),
  `allow_methods = ["GET","POST","PUT","DELETE","OPTIONS"]`,
  `allow_headers = ["Authorization","Content-Type"]`
- `aws_apigatewayv2_integration "lambda"`: `integration_type = "AWS_PROXY"`,
  `integration_uri = aws_lambda_function.api.invoke_arn`,
  `payload_format_version = "2.0"`
- `aws_apigatewayv2_route "default"`: `route_key = "ANY /{proxy+}"` →
  integração acima (repassa tudo para a Lambda; a aplicação decide
  roteamento/auth internamente, como já faz)
- `aws_apigatewayv2_stage "default"`: `name = "$default"`,
  `auto_deploy = true`, `default_route_settings`:
  `throttling_rate_limit = 5`, `throttling_burst_limit = 10`
- `aws_lambda_permission "apigateway"`: permite
  `apigateway.amazonaws.com` invocar `aws_lambda_function.api`, `source_arn`
  restrito ao ARN de execução do API Gateway (não `*`)

### Nova variável (`variables.tf`)
```hcl
variable "frontend_origin" {
  description = "Origem (URL) do frontend Angular permitida no CORS. Placeholder até o domínio existir."
  type        = string
  default     = "http://localhost:4200"
}
```

### Novo output (`outputs.tf`)
```hcl
output "api_gateway_url" {
  description = "URL pública base do HTTP API."
  value       = aws_apigatewayv2_stage.default.invoke_url
}
```

## Recursos AWS afetados

Novos: `aws_lambda_function`, `aws_iam_role` + `aws_iam_role_policy`
(execução da Lambda), `aws_cloudwatch_log_group` (retenção 14 dias),
`aws_apigatewayv2_api`, `aws_apigatewayv2_integration`,
`aws_apigatewayv2_route`, `aws_apigatewayv2_stage`,
`aws_lambda_permission`. Nenhuma mudança nos recursos já existentes
(DynamoDB, Cognito, Parameter Store — apenas referenciados por ARN nas
novas policies).

## Mapeamento de erros

Não há novo erro de negócio — mesmos `Error`/`ErrorType`/status HTTP já
mapeados hoje (`ResultHttpExtensions`). Comportamentos operacionais
novos, não relacionados a regra de negócio:
- Requisição além do `throttling` do stage → `429` (gerado pelo próprio
  API Gateway, antes de chegar à Lambda)
- Falha de inicialização da Lambda (ex.: erro ao ler Parameter Store)
  → `500` genérico do API Gateway, visível nos logs do CloudWatch

## Decisões confirmadas pelo usuário

1. **Sem autorizador JWT no API Gateway** — autenticação só na
   aplicação, como hoje
2. **Arquitetura `linux-x64`** (não `arm64`) — evita emulação no build
   via Docker
3. **Memória: 256 MB**, timeout 10s — meio-termo mais seguro que 128MB
   (risco de OOM/CPU fraca) e mais barato que 512MB, ajustável depois
   com base em métricas reais do CloudWatch
4. Nenhum `terraform apply`/build/deploy roda sem aprovação explícita,
   comando a comando — mesma regra já usada na FEAT-09.