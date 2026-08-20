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
igual ao Cognito real. Com AWS CLI apontando pro endpoint local:

```bash
export AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1
export MSYS_NO_PATHCONV=1  # só no Git Bash/Windows — ver nota abaixo

USER_POOL_ID=$(grep USER_POOL_ID .local-cognito-ids | cut -d= -f2)

aws --endpoint-url http://localhost:9229 cognito-idp admin-confirm-sign-up \
  --user-pool-id "$USER_POOL_ID" --username <email-cadastrado>
```

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
