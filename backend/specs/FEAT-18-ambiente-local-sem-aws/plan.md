# Plano técnico — FEAT-18: Ambiente local sem dependência de AWS real

## Visão geral

A aplicação já é 100% configurável via `IConfiguration`
(`appsettings.json` → `appsettings.{Environment}.json` → variáveis de
ambiente → Parameter Store, nesta ordem de merge). Esta feature não
introduz nenhum mecanismo novo de configuração — só **estende os POCOs
de opções já existentes** (`CognitoOptions`, `DynamoDbOptions`, e o
parâmetro `path` de `AddAwsParameterStore`) com os campos que faltam
para apontar para endpoints locais, e adiciona os artefatos de infra
(`docker-compose.yml` + scripts de seed) que sobem e populam esses
endpoints locais com os mesmos valores que hoje vêm do Parameter Store
real.

`CognitoOptions` já tem `ServiceURL`/`AccessKey`/`SecretKey` com o
comentário `// só para dev local se precisar` (adicionados
preventivamente em feature anterior) e `AddCognitoSdk` já sabe usá-los
— **nenhuma mudança necessária no client do Cognito SDK**. O que falta:
1. `DynamoDbOptions` não tem os mesmos campos — o client do DynamoDB é
   sempre construído apontando pra AWS real.
2. `AwsParameterStoreExtensions.AddAwsParameterStore` não aceita
   endpoint/credenciais customizados — o client de SSM é sempre real.
3. `JwtAuthenticationExtensions.AddCognitoAuth` monta `options.Authority`
   sempre a partir do domínio real do Cognito
   (`cognito-idp.{region}.amazonaws.com`) e força
   `RequireHttpsMetadata = true` — quebra contra o JWKS servido em HTTP
   pelo cognito-local.

## Camadas afetadas

### `GastosApp.Infrastructure`

- **`Configuration/DynamoDbOptions.cs`**: adicionar `ServiceURL`,
  `AccessKey`, `SecretKey` (nullable, mesmo formato de
  `CognitoOptions`).
- **`DependencyInjection/InfrastructureServiceCollectionExtensions.cs`**
  (`AddAwsInfrastructure`): ler os novos campos na leitura manual já
  existente de `DynamoDbOptions`, e usá-los ao construir
  `IAmazonDynamoDB` — mesmo padrão condicional já usado em
  `AddCognitoSdk` (`ServiceURL` presente → `AmazonDynamoDBConfig` com
  `ServiceURL`/`AuthenticationRegion`; `AccessKey`/`SecretKey`
  presentes → `BasicAWSCredentials`; senão, comportamento atual
  inalterado — client real com credenciais do ambiente/IAM Role).
- **`Configuration/AwsParameterStoreExtensions.cs`**
  (`AddAwsParameterStore`): adicionar parâmetros opcionais
  `serviceURL`, `region`, `accessKey`, `secretKey` (defaults `null`/
  `"us-east-1"`, comportamento atual preservado quando omitidos).
  Quando `serviceURL` informado, construir
  `AmazonSimpleSystemsManagementConfig` com `ServiceURL`/
  `AuthenticationRegion` em vez de `RegionEndpoint.USEast1` fixo; usar
  `BasicAWSCredentials` quando `accessKey`/`secretKey` informados.

### `GastosApp.Api`

- **`Program.cs`**: antes de chamar `AddAwsParameterStore`, ler
  `ParameterStore:ServiceURL` / `ParameterStore:Region` /
  `ParameterStore:AccessKey` / `ParameterStore:SecretKey` de
  `builder.Configuration` (já populado por `appsettings.Development.json`
  neste ponto, antes do merge do Parameter Store) e repassar para a
  chamada. Nenhuma mudança de comportamento quando essas chaves não
  existem (produção/homologação continuam sem declará-las).
- **`Common/JwtAuthenticationExtensions.cs`** (`AddCognitoAuth`): ler
  também `Cognito:ServiceURL` da configuração (já populada em memória
  pelo Parameter Store — local ou real, mesma fonte de sempre). Regra:
  - `Authority` = `{ServiceURL}/{userPoolId}` quando `ServiceURL`
    presente; senão o valor atual
    (`https://cognito-idp.{region}.amazonaws.com/{userPoolId}`).
  - `RequireHttpsMetadata` = `false` quando `ServiceURL` presente
    (cognito-local roda em HTTP puro); `true` caso contrário
    (comportamento atual, inalterado em produção/homologação).
  - Resto do `TokenValidationParameters` inalterado.

### `backend/infra/` (novo, substitui o legado)

- Remover: `docker-compose.yml`, `kong.yml`, `scripts/seed-dynamo.sh`,
  `scripts/localstack-init/` (artefatos legados, já documentados como
  decisão pendente em `backend/infra/CLAUDE.md`).
- Criar `backend/infra/docker-compose.yml`:
  - **`localstack`**: imagem `localstack/localstack:3` (Community,
    gratuita), `SERVICES=dynamodb,ssm`, porta `4566:4566`, volume para
    persistência opcional entre reinícios (`./.localstack:/var/lib/localstack`).
  - **`cognito-local`**: build a partir de um `Dockerfile` próprio em
    `backend/infra/cognito-local/` (imagem base `node:20-alpine` +
    pacote npm `cognito-local`), porta `9229:9229`, volume para
    persistência (`./.cognito-local:/app/.cognito`) — build local em
    vez de depender de uma imagem de terceiros não oficial no Docker
    Hub.
- Criar `backend/infra/scripts/local-init.sh` (orquestrador,
  idempotente — checa antes de criar), que chama em ordem:
  1. `init-cognito.sh` — via AWS CLI apontando
     `--endpoint-url http://localhost:9229`: cria o User Pool + App
     Client (mesmo padrão de política de senha de
     `backend/infra/terraform/environments/hom/cognito.tf`), captura
     `UserPoolId`/`ClientId`.
  2. `init-dynamodb.sh` — via AWS CLI apontando
     `--endpoint-url http://localhost:4566`: cria a tabela
     (`GastosApp-Local`) com o mesmo modelo de dados de
     `backend/docs/architecture.md` (PK/SK, GSI1PK/GSI1SK, billing
     `PAY_PER_REQUEST`).
  3. `init-parameter-store.sh` — via AWS CLI apontando
     `--endpoint-url http://localhost:4566`: `put-parameter` para cada
     chave hoje existente em `/GastosApp/` (produção) — `Cognito/Region`,
     `Cognito/UserPoolId`, `Cognito/ClientId` (dos passos 1),
     `Cognito/ServiceURL=http://localhost:9229`,
     `Cognito/AccessKey=test`, `Cognito/SecretKey=test`,
     `Cors/AllowedOrigins`. Credenciais AWS CLI/SDK usadas pelos
     scripts e pela app: dummy fixas (`test`/`test`), padrão aceito por
     LocalStack e cognito-local — sem relação com credenciais reais.
- Atualizar `backend/src/GastosApp.Api/appsettings.Development.json`:
  adicionar seção `DynamoDb` com `ServiceURL: http://localhost:4566`,
  `AccessKey`/`SecretKey: test`, `TableName: GastosApp-Local` (troca o
  valor atual `GastosApp-Hom`, que hoje aponta para a tabela real de
  homologação) e seção `ParameterStore` com
  `ServiceURL: http://localhost:4566`, `AccessKey`/`SecretKey: test`.
  Valores fixos e sem segredo real — seguro versionar.
- Criar `backend/infra/README.md`: `docker compose up -d` +
  `./scripts/local-init.sh` + `dotnet run --project src/GastosApp.Api`,
  passo a passo completo, incluindo pré-requisito (AWS CLI instalado
  para os scripts de seed).

## Recursos AWS usados/afetados

**Nenhum recurso AWS real é criado, alterado ou afetado por esta
feature.** Todos os recursos "AWS" desta feature são emulados
localmente (LocalStack Community + cognito-local, ambos gratuitos,
rodando em containers Docker na máquina do desenvolvedor).
`environments/prod/` e `environments/hom/` em
`backend/infra/terraform/` não são tocados.

## Decisões técnicas / trade-offs

- **Sem lib de terceiros para Parameter Store nem para Cognito
  local**: mantém o padrão já estabelecido (leitura manual via AWS SDK
  direto, sem `Amazon.Extensions.Configuration.SystemsManager`) — ver
  comentário em `AwsParameterStoreExtensions.cs`.
- **`ServiceURL` como sinalizador de "modo local"**: em vez de uma
  flag booleana separada (`UseLocalStack`), a própria presença de
  `Cognito:ServiceURL`/`DynamoDb:ServiceURL`/`ParameterStore:ServiceURL`
  na configuração já determina o comportamento (Authority alternativo,
  `RequireHttpsMetadata=false`, credenciais dummy) — menos uma
  variável para manter sincronizada, e produção/homologação nunca
  declaram essas chaves, então o comportamento real é sempre o
  default atual.
- **cognito-local via build próprio**, não uma imagem pronta de
  terceiros: reduz risco de imagem desatualizada/não mantida no Docker
  Hub; o `Dockerfile` fica versionado no repositório.
- **`GastosApp-Local` como nome de tabela local**, distinto de
  `GastosApp`/`GastosApp-Hom`, só por clareza operacional — não há
  nenhuma tabela real com esse nome, é local ao LocalStack.
- **Credenciais dummy (`test`/`test`) fixas e versionadas**: padrão
  documentado do próprio LocalStack/cognito-local para desenvolvimento
  local, sem relação com credenciais reais — não é segredo, pode ficar
  em `appsettings.Development.json` versionado.
- **Sem mudança nos testes automatizados**: `ComponentTests` já usa
  `WebApplicationFactory` com `Environment = "Testing"`, que hoje pula
  `AddAwsParameterStore` inteiramente (`Program.cs:14`) — nenhum
  contato com LocalStack/cognito-local nem com AWS real. Continua
  assim.

## Mapeamento de erros de negócio

Não aplicável — esta feature não introduz nem altera nenhum
comportamento de negócio, endpoint ou `Result`/`Error`. É puramente
infraestrutura de desenvolvimento local.

## Pontos que precisam de confirmação antes do `/tasks`

1. **Nome/organização exata dos scripts de seed** (`local-init.sh` +
   3 scripts separados vs. um único script) — proposta acima é a
   linha-base, mas pode ser ajustada no `/tasks` sem impacto na spec.
2. **Persistência entre reinícios dos containers** (`./.localstack`,
   `./.cognito-local`): incluídos aqui como volumes montados — confirmar
   se devem entrar no `.gitignore` (dados/estado local, não código).
3. **Versão fixa do pacote npm `cognito-local`** a pinar no
   `Dockerfile` — a definir durante a implementação (última versão
   estável no momento).
