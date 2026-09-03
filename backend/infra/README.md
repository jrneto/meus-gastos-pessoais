# Ambiente local do backend GastosApp

Roda o backend inteiramente na máquina do desenvolvedor, sem depender
de credenciais AWS reais e sem afetar produção ou homologação — ver
`backend/specs/FEAT-18-ambiente-local-sem-aws/` e
[`CLAUDE.md`](CLAUDE.md).

## Pré-requisitos

- Docker (com Docker Compose)
- AWS CLI v2 (usado só pelos scripts de seed, não pela aplicação)
- .NET 10 SDK

## Passo a passo

```bash
cd backend/infra

# 1. Sobe LocalStack (DynamoDB + SSM Parameter Store) e cognito-local
docker compose up -d

# 2. Popula os dois: User Pool no cognito-local, tabela GastosApp-Local
#    no LocalStack, e os parâmetros equivalentes em /GastosApp/ no SSM
#    local. Idempotente — pode rodar de novo sem duplicar nada.
./scripts/local-init.sh

# 3. Roda a API (fora do Docker)
cd ..
dotnet run --project src/GastosApp.Api
```

A API sobe em `http://localhost:5049` (ver
`src/GastosApp.Api/Properties/launchSettings.json`), configurada por
`appsettings.Development.json` para usar os endpoints locais
(`http://localhost:4566` para DynamoDB/Parameter Store,
`http://localhost:9229` para o Cognito).

## Confirmando um usuário

O cognito-local exige confirmação depois do `POST /auth/register`,
igual ao Cognito real — sem confirmar, o `POST /auth/login` desse
usuário falha com `User is not confirmed.`. Não existe usuário nem
confirmação automática: `local-init.sh` só cria o User Pool e o App
Client (estrutura vazia), o usuário é criado por você via
`POST /auth/register`.

Para confirmar, use `admin-confirm-sign-up` do AWS CLI apontando pro
endpoint do cognito-local (`http://localhost:9229`), passando o
`UserPoolId` local (gerado por `local-init.sh` e salvo em
`.local-cognito-ids`, na raiz de `backend/infra/`) e o e-mail cadastrado
como `--username`. **Rode a partir de `backend/infra/`** — é onde
`.local-cognito-ids` fica (o `grep` abaixo falha silenciosamente com
`UserPoolId` vazio se rodado de outro diretório):

```bash
cd backend/infra  # se ainda não estiver aqui

export AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1
export MSYS_NO_PATHCONV=1  # só no Git Bash/Windows — ver nota abaixo

# lê o UserPoolId gerado por local-init.sh (ex.: local_6Enm3gxX)
USER_POOL_ID=$(grep USER_POOL_ID .local-cognito-ids | cut -d= -f2)

# exemplo: confirma o usuário teste-local@jrnexpenses.com
aws --endpoint-url http://localhost:9229 cognito-idp admin-confirm-sign-up \
  --user-pool-id "$USER_POOL_ID" --username teste-local@jrnexpenses.com
```

Depois de confirmado, `POST /auth/login` com esse e-mail/senha funciona
normalmente. Repita o comando (trocando `--username`) para cada novo
usuário de teste que precisar de login.

## Rodando as Lambdas de trigger do Cognito localmente (via Postman/curl)

O `cognito-local` não invoca nenhuma Lambda de verdade (sem
`LambdaConfig` no User Pool) — diferente do Cognito real, confirmar/
registrar um usuário localmente não aciona `account-trigger` nem
`custom-message-trigger`. Pra exercitar essas duas Lambdas manualmente
sem depender de deploy, cada uma tem um par de scripts que builda o
binário Native AOT publicado (mesma imagem base `provided.al2023` da
Lambda real) e sobe via Lambda Runtime Interface Emulator (RIE) —
mesma técnica de "Testes integrados locais" abaixo, mas pensada pra
chamar manualmente (Postman/curl), não pra rodar suíte automatizada.

**Diferença importante em relação à Api**: a Api usa
`Amazon.Lambda.AspNetCoreServer.Hosting` (protocolo API Gateway — o
body do POST pro RIE precisa simular um evento HTTP, ver
`tests/GastosApp.IntegrationTests/Support/LambdaRieTransport.cs`).
Essas duas Lambdas são handlers de evento do Cognito puros — o body do
POST pro RIE é o **próprio evento do Cognito**, sem nenhum envelope.

```bash
cd backend

# account-trigger (PostConfirmation, FEAT-19) — depende de LocalStack +
# cognito-local (sobe/garante os dois sozinho), grava em GastosApp-Local
./infra/lambda/local-env-up-account-trigger.sh
# ...
./infra/lambda/local-env-down-account-trigger.sh

# custom-message-trigger (CustomMessage, FEAT-34) — não tem dependência
# nenhuma (não toca DynamoDB/Cognito, só formata texto)
./infra/lambda/local-env-up-custom-message-trigger.sh
# ...
./infra/lambda/local-env-down-custom-message-trigger.sh
```

Cada script `local-env-up-*` imprime, ao final, a porta (`9001` pro
account-trigger, `9002` pro custom-message-trigger), o endpoint de
invocação do RIE e um exemplo completo do evento esperado — cole esse
JSON direto no body de um `POST` no Postman/curl. Os POCOs de cada
evento (`CognitoPostConfirmationEvent.cs`/`CognitoCustomMessageEvent.cs`)
são a fonte de verdade do formato exato.

## Testes integrados locais (FEAT-29)

Sobe o binário Native AOT publicado num container da mesma família de
imagem base da Lambda real (`provided.al2023`), acessível via Lambda
Runtime Interface Emulator, e roda `GastosApp.IntegrationTests` contra
ele — pega erro específico de AOT antes de qualquer deploy real. Requer
`docker compose up -d` (passo 1 acima), mas **não** requer
`./scripts/local-init.sh` nem `dotnet run` manuais — o próprio script
cuida disso:

```bash
cd backend
./infra/lambda/run-local.sh
```

Ver `backend/specs/FEAT-29-testes-integrados/plan.md` para detalhes.

## Zerando os dados (sem derrubar o ambiente)

Remove todos os itens da tabela `GastosApp-Local` (LocalStack) e todos
os usuários do User Pool local (cognito-local), mantendo containers,
tabela, User Pool/App Client e parâmetros intactos — útil pra repetir
um teste manual do zero sem reprovisionar o ambiente inteiro. Diferente
de "Parando/limpando" abaixo, que derruba os containers e apaga todo o
estado.

```bash
cd backend/infra
./scripts/local-reset.sh
```

Também dá pra rodar cada parte isoladamente: `./scripts/reset-dynamodb.sh`
(só a tabela) ou `./scripts/reset-cognito.sh` (só os usuários).

## Parando/limpando

```bash
docker compose down          # para os containers
rm -rf .localstack .cognito-local .local-cognito-ids   # apaga o estado local
```

## Notas

- **Windows/Git Bash**: argumentos começando com `/` (ex.:
  `/GastosApp/...`) são reescritos como caminho de arquivo Windows
  pelo MSYS antes de chegar no AWS CLI, corrompendo nomes de parâmetro
  silenciosamente. Os scripts em `scripts/` já exportam
  `MSYS_NO_PATHCONV=1`; se você rodar comandos `aws` manualmente no
  Git Bash, exporte essa variável também.
- **Credenciais dummy (`test`/`test`)**: exigidas pelo AWS CLI/SDK para
  falar com LocalStack e cognito-local, sem relação com credenciais AWS
  reais — não são segredo.
- **Produção e homologação não são afetadas**: os endpoints locais só
  existem em `appsettings.Development.json` e nos parâmetros seedados
  aqui — nenhuma variável equivalente existe em produção/homologação.
