# Tasks — FEAT-18: Ambiente local sem dependência de AWS real

## Infrastructure (código .NET)

- [x] 1. Adicionar `ServiceURL`, `AccessKey`, `SecretKey` (nullable) em
      `GastosApp.Infrastructure/Configuration/DynamoDbOptions.cs`, mesmo
      padrão de `CognitoOptions`
- [x] 2. Atualizar `AddAwsInfrastructure` em
      `InfrastructureServiceCollectionExtensions.cs` para ler os novos
      campos de `DynamoDbOptions` na leitura manual já existente
- [x] 3. Atualizar a construção de `IAmazonDynamoDB` em
      `AddAwsInfrastructure` para usar `AmazonDynamoDBConfig` com
      `ServiceURL`/`AuthenticationRegion` quando `ServiceURL` estiver
      presente, e `BasicAWSCredentials` quando `AccessKey`/`SecretKey`
      estiverem presentes — comportamento atual preservado quando
      nenhum dos dois é informado
- [x] 4. Adicionar parâmetros opcionais `serviceURL`, `region`,
      `accessKey`, `secretKey` a `AddAwsParameterStore` em
      `AwsParameterStoreExtensions.cs`
- [x] 5. Atualizar `AddAwsParameterStore` para construir
      `AmazonSimpleSystemsManagementConfig` com `ServiceURL`/
      `AuthenticationRegion` quando `serviceURL` informado, e
      `BasicAWSCredentials` quando `accessKey`/`secretKey` informados —
      comportamento atual preservado quando omitidos
- [x] 6. Atualizar `Program.cs` (`GastosApp.Api`) para ler
      `ParameterStore:ServiceURL`/`Region`/`AccessKey`/`SecretKey` de
      `builder.Configuration` antes de chamar `AddAwsParameterStore` e
      repassar os valores
- [x] 7. Atualizar `AddCognitoAuth` em
      `Common/JwtAuthenticationExtensions.cs` para ler
      `Cognito:ServiceURL` e montar `Authority` como
      `{ServiceURL}/{userPoolId}` quando presente (senão o valor atual)
- [x] 8. Atualizar `AddCognitoAuth` para definir
      `RequireHttpsMetadata = false` quando `Cognito:ServiceURL`
      estiver presente, `true` caso contrário (comportamento atual)
- [x] 9. `dotnet build GastosApp.sln` sem erros/warnings novos

## Infra local (Docker/scripts)

- [x] 10. Remover artefatos legados: `backend/infra/docker-compose.yml`,
       `backend/infra/kong.yml`, `backend/infra/scripts/seed-dynamo.sh`,
       `backend/infra/scripts/localstack-init/`
- [x] 11. Criar `backend/infra/cognito-local/Dockerfile` (build a partir
       de `node:20-alpine` + pacote npm `cognito-local`, versão fixa)
- [x] 12. Criar `backend/infra/docker-compose.yml` com os serviços
       `localstack` (`SERVICES=dynamodb,ssm`, porta `4566`, volume
       `./.localstack`) e `cognito-local` (build do Dockerfile da task
       11, porta `9229`, volume `./.cognito-local`)
- [x] 13. Adicionar `.localstack/` e `.cognito-local/` ao `.gitignore`
- [x] 14. Criar `backend/infra/scripts/init-cognito.sh` — cria User
       Pool + App Client via AWS CLI contra
       `http://localhost:9229`, idempotente, imprime
       `UserPoolId`/`ClientId`
- [x] 15. Criar `backend/infra/scripts/init-dynamodb.sh` — cria a tabela
       `GastosApp-Local` (PK/SK, GSI1PK/GSI1SK, `PAY_PER_REQUEST`) via
       AWS CLI contra `http://localhost:4566`, idempotente
- [x] 16. Criar `backend/infra/scripts/init-parameter-store.sh` — popula
       `/GastosApp/` no SSM local (`http://localhost:4566`) com
       `Cognito/Region`, `Cognito/UserPoolId`, `Cognito/ClientId`
       (saída da task 14), `Cognito/ServiceURL`, `Cognito/AccessKey`,
       `Cognito/SecretKey`, `Cors/AllowedOrigins`, idempotente
- [x] 17. Criar `backend/infra/scripts/local-init.sh` orquestrando as
       tasks 14→16 em ordem, com credenciais dummy (`test`/`test`)
       exportadas para o AWS CLI

## Configuração da aplicação

- [x] 18. Atualizar
       `backend/src/GastosApp.Api/appsettings.Development.json`: seção
       `DynamoDb` com `ServiceURL=http://localhost:4566`,
       `AccessKey`/`SecretKey=test`, `TableName=GastosApp-Local`
       (substitui `GastosApp-Hom`); seção `ParameterStore` com
       `ServiceURL=http://localhost:4566`, `AccessKey`/`SecretKey=test`

## Validação manual (fluxo completo local)

- [x] 19. Subir `docker compose up -d` + `./scripts/local-init.sh` em
       `backend/infra/` numa máquina sem credenciais AWS configuradas
- [x] 20. Rodar `dotnet run --project src/GastosApp.Api` e validar
       `POST /auth/register` → `POST /auth/login` → `GET /auth/me`
       contra o cognito-local
- [x] 21. Validar `POST /categories` → `GET /categories` →
       `POST /expenses` → `GET /expenses` → `PUT /expenses/{id}` →
       `DELETE /expenses/{id}` contra o LocalStack local, confirmando
       persistência na tabela `GastosApp-Local`
- [x] 22. Confirmar que nenhuma chamada tocou AWS real durante a
       validação (sem credenciais AWS na máquina de teste, sem erro de
       autenticação/rede para fora do localhost)
- [x] 23. Validar produção e homologação sem regressão (smoke test
       contra `api.jrnexpenses.com`/`api-hom.jrnexpenses.com`: `401`
       sem token, comportamento idêntico ao anterior à feature)

## Testes automatizados

- [x] 24. `dotnet test GastosApp.sln` completo (unitários + componente)
       passando sem alteração — confirmar que `ComponentTests`
       continuam usando `Environment=Testing` e não tocam
       LocalStack/cognito-local/AWS real

## Documentação

- [x] 25. Atualizar `backend/docs/constitution.md` (regra imutável de
       infraestrutura) refletindo o novo princípio: ambiente local
       emulado (LocalStack + cognito-local), produção/homologação
       continuam 100% AWS real
- [x] 26. Atualizar `backend/CLAUDE.md` (seção "Stack" / estrutura de
       `infra/`) refletindo o mesmo princípio e a nova estrutura de
       `backend/infra/`
- [x] 27. Atualizar `backend/infra/CLAUDE.md` (seção "Princípios" e
       "Estado legado (pendente de decisão)") removendo a proibição de
       simulação local e documentando a solução adotada
- [x] 28. Criar `backend/infra/README.md` com o passo a passo completo
       para subir e usar o ambiente local do zero

## Fechamento da spec

- [x] 29. Marcar em `spec.md` os critérios de aceite concluídos e
       preencher a seção "Status" com o resumo do que foi feito
       (mesmo padrão de `backend/specs/FEAT-13-ambiente-homologacao/spec.md`)
