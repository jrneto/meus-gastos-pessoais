# GastosApp.IntegrationTests — guia de debug

Suíte de testes integrados (FEAT-29) — roda contra a API real (Cognito
+ DynamoDB reais em hom/prod; em local, por padrão a Api via
`dotnet run`/Kestrel, ou o binário Native AOT publicado via Lambda
Runtime Interface Emulator quando pedido explicitamente), nunca contra
dublês. Ver `backend/specs/FEAT-29-testes-integrados/spec.md` e
`plan.md` para o desenho completo. Este README é só o passo a passo
prático de debug.

## Como a suíte fala com a API (recap rápido)

- `Support/IApiTransport.cs` abstrai o transporte: `DirectHttpTransport`
  (HTTP puro — hom/prod sempre, e local por padrão, contra
  `dotnet run`/Kestrel) ou `LambdaRieTransport` (protocolo de invocação
  do Runtime Interface Emulator, contra o container Native AOT — local,
  só com `INTEGRATION_TESTS_TRANSPORT=rie` explícita).
- `Support/IntegrationTestEnvironment.cs` decide o modo a partir de
  `INTEGRATION_TESTS_MODE` (`local`\|`hom`\|`prod`, default `local`) e,
  em `local`, o transporte a partir de `INTEGRATION_TESTS_TRANSPORT`
  (`kestrel` — default, `http://localhost:5049` — \|`rie`).
- `Support/TestAccountFixture.cs` cria uma conta de teste real
  (`POST /auth/register` → `AdminConfirmSignUp` via SDK → `POST /auth/login`)
  e limpa tudo ao final (Cognito + DynamoDB), mesmo se o teste falhar.

## 1. Debugar com breakpoint no VS Code

Pré-requisito: extensão **C#** (`ms-dotnettools.csharp`) instalada — o
Test Explorer com CodeLens/ícone de debug por teste vem do **C# Dev
Kit**, mas o F5 via `launch.json` funciona só com a C# "básica".

O ponto chave: o processo que roda o **código da suíte**
(`AuthFlowTests.cs`, `TestAccountFixture.cs`, `LambdaRieTransport.cs`
etc.) é sempre um `dotnet test` normal rodando **na sua máquina** —
nunca dentro de um container, em nenhum dos 3 modos. Breakpoint nesses
arquivos funciona exatamente como em qualquer outro projeto de teste
.NET (`UnitTests`/`ComponentTests`). O que muda por modo é só **pra
onde** a suíte manda a requisição HTTP (ver "Como a suíte fala com a
API" acima) — não muda nada em como você debuga o lado do teste.

> Se o objetivo é debugar o **código da Api** (não só o do teste), é
> exatamente pra isso que serve o modo padrão descrito abaixo — a Api
> roda via `dotnet run`/Kestrel (JIT), então breakpoint funciona nela
> também. Só o modo explícito "via container/RIE" (Native AOT) não tem
> esse suporte — ver "Debugar a própria Api (não só o teste)".

### Modo local

**Por padrão, sem nenhuma configuração especial, `GastosApp.IntegrationTests`
aponta pra Api rodando via `dotnet run`/Kestrel (JIT normal) em
`http://localhost:5049`** — não pro container Native AOT. Isso vale
tanto pra CLI quanto pro Test Explorer do VS Code (ícone de frasco na
barra lateral, ou o ícone de debug/run via CodeLens acima do método,
C# Dev Kit): **precisa da Api rodando à parte antes**:
```bash
cd backend
dotnet run --project src/GastosApp.Api   # ou F5 na config "GastosApp.Api"
```
Sem isso, dá `HttpIOException: The response ended prematurely` (achado
real — não é um "conexão recusada" comum porque o `cognito-local`
publica a porta 9000, reservada pro container FEAT-29, então o Docker
aceita a conexão e derruba na hora se não tiver nada ouvindo do outro
lado).

> **Por que o padrão é Kestrel, não o container?** Tentamos fazer o
> Test Explorer injetar `INTEGRATION_TESTS_BASE_URL` via
> `.vscode/settings.json` (`dotnet.unitTestDebuggingOptions`) — não se
> mostrou confiável (o teste continuava caindo no RIE mesmo com a
> chave setada). Em vez de depender disso, o próprio código passou a
> ter esse default embutido — funciona igual não importa como o teste
> é disparado.

Pra validar contra o **binário Native AOT publicado de verdade** (não
Kestrel — mais fiel ao que vai pra produção, mas sem breakpoint dentro
do código da Api, só do teste, ver "Debugar a própria Api" abaixo), use
a config **"Debug Integration Tests (local via container/RIE, escolher
filtro)"** no Run and Debug (`Ctrl+Shift+D`) — ela seta
`INTEGRATION_TESTS_TRANSPORT=rie` explicitamente. Precisa do container
no ar:
```bash
cd backend
./infra/lambda/local-env-up.sh   # deixa rodando — não use run-local.sh aqui, ele desliga tudo ao final
```
Quando terminar: `./infra/lambda/local-env-down.sh`.

Em qualquer um dos dois: coloque o breakpoint no arquivo do teste antes
de disparar (ex.: `tests/GastosApp.IntegrationTests/Auth/AuthFlowTests.cs`)
— a execução para ali, inspeciona variável/watch/call stack
normalmente.

### Debugar a própria Api (não só o teste)

O fluxo "via container/RIE" acima debuga só o **código do teste** — o
container roda o binário Native AOT sem nenhum debugger anexado nele.
Native AOT não tem o mesmo suporte de debug interativo que código JIT,
então **não dá pra colocar breakpoint dentro do código da Api enquanto
ela roda no container**.

Pra colocar breakpoint dentro da própria Api (um `Handler`, um
`Endpoint`, etc.), use o modo **padrão** descrito acima (Kestrel/JIT) —
já é exatamente isso: a suíte fala HTTP direto com a Api rodando via
`dotnet run`, então breakpoint funciona nos dois lados.

1. Garanta LocalStack + cognito-local no ar:
   ```bash
   cd backend/infra && docker compose up -d && cd ..
   ```
2. Coloque o breakpoint dentro do código da Api (ex.:
   `src/GastosApp.Application/Auth/Commands/Register/RegisterUserCommand.cs`).
3. Duas formas de disparar (Run and Debug, `Ctrl+Shift+D`):
   - **Um clique**: escolha o compound **"Debug Api + Integration Test
     (local via Kestrel)"** e aperte `F5` — sobe a Api e já dispara o
     teste (pede o filtro). Tem uma corrida em aberto: se o teste
     disparar antes da Api terminar de subir, a primeira tentativa
     falha — só rodar de novo a config do teste sozinha (a Api já vai
     estar de pé). Se isso incomodar, use o fluxo em 2 passos abaixo.
   - **2 passos (mais previsível)**: rode **"GastosApp.Api"** primeiro,
     espere aparecer `Now listening on: http://localhost:5049` no
     Debug Console; só então, numa segunda sessão de debug, rode
     **"Debug Integration Tests (local via Kestrel, escolher
     filtro)"** (ou o Test Explorer, ou um `dotnet test` direto no
     terminal — todos batem na mesma Api).
4. Os dois breakpoints (Api e teste) funcionam ao mesmo tempo — são
   duas sessões de debug simultâneas no VS Code.

### Modo hom (contra a API real de homologação)

1. Autentique na AWS no **mesmo** terminal/perfil que o VS Code herda
   (ex.: `aws sso login --profile <seu-profile>` e
   `export AWS_PROFILE=<seu-profile>` antes de abrir o VS Code a partir
   desse terminal) — a suíte usa a cadeia padrão de credenciais do SDK,
   nada é declarado no `launch.json`.
2. Use a config **"Debug Integration Tests (hom, escolher filtro)"** em
   Run and Debug (`F5`) — pede um filtro de teste.
3. Isso cria e limpa uma conta de teste **real** em homologação (ver
   `TestAccountFixture`). Evite deixar pausado num breakpoint por muito
   tempo no meio de um teste — a limpeza só roda quando o teste
   termina (`DisposeAsync`).

### Sem VS Code — anexar um debugger via linha de comando

```bash
INTEGRATION_TESTS_MODE=local VSTEST_HOST_DEBUG=1 \
  dotnet test tests/GastosApp.IntegrationTests --filter "FullyQualifiedName~NomeDoTeste"
```

O `testhost` imprime o próprio PID e fica esperando um debugger anexar
antes de continuar. No VS Code: **Run → Attach to Process** (ou
`Ctrl+Shift+P` → "Debug: Attach to a .NET 5+ or .NET Core Process") e
escolha esse PID.

## 2. Debugar contra o ambiente local (mais comum)

### Rodar do zero

```bash
cd backend
./infra/lambda/run-local.sh
```

Isso builda a imagem Native AOT, garante LocalStack + cognito-local no
ar, roda `local-init.sh` (idempotente), sobe o container via Runtime
Interface Emulator e roda a suíte. Se passar, pronto. Se falhar, siga
abaixo.

### O container não sobe / RIE não responde ("Container não respondeu depois de 30 tentativas")

```bash
# o script já imprime os logs no final, mas pra investigar mais:
docker logs gastosapp-api-local-run-container
```

Causas mais comuns:
- **Erro de inicialização do host (AOT)**: alguma dependência usa
  reflection não suportada por Native AOT, ou `services.Configure<T>()`
  em vez de leitura manual (ver `backend/infra/CLAUDE.md`, "Gotchas
  conhecidos"). O log do container mostra a exceção de startup.
- **`docker build` falhou silenciosamente por um erro transitório do
  BuildKit** (`NotFound: forwarding Ping: no such job ...`, achado
  real durante a implementação) — rode o build isolado pra confirmar:
  ```bash
  docker build -f infra/lambda/Dockerfile.local-run --target local-run -t gastosapp-api-local-run .
  ```
  Se for isso, é transitório do Docker Desktop — só rodar de novo.

### `Parâmetro 'Cognito/UserPoolId' não encontrado sob '/GastosApp/'`

O SSM local (LocalStack) está sem os parâmetros seedados — geralmente
porque o `docker compose up -d` precisou **recriar** os containers
(ex.: mudança de config) e o estado do LocalStack não sobreviveu, mesmo
com o volume montado. `run-local.sh` já roda `local-init.sh`
incondicionalmente por causa disso, mas se você estiver rodando os
testes manualmente (sem passar por `run-local.sh`), rode primeiro:

```bash
cd backend/infra
docker compose up -d
./scripts/local-init.sh   # idempotente, sempre seguro rodar de novo
```

Pra confirmar o que tem (ou não) no SSM local:

```bash
export AWS_ACCESS_KEY_ID=test AWS_SECRET_ACCESS_KEY=test AWS_DEFAULT_REGION=us-east-1 MSYS_NO_PATHCONV=1
aws --endpoint-url http://localhost:4566 ssm get-parameters-by-path --path /GastosApp/ --recursive
```

### `GET /auth/me` retorna 401 mesmo com token válido (achado real, já corrigido)

Se você alterar `run-local.sh` e isso voltar a acontecer: o
`cognito-local` fixa `issuer`/`jwks_uri` do seu discovery document
(`/.well-known/openid-configuration`) em `http://localhost:9229`
(`backend/infra/cognito-local/config.json`, `IssuerDomain`) — o
middleware de JWT da Api só consegue buscar o JWKS se `localhost:9229`
resolver pro próprio `cognito-local`. Isso só acontece rodando no
**mesmo namespace de rede** dele (`docker run --network container:gastosapp-cognito-local`,
não `--network gastosapp-local`) — Docker recusa sobrescrever
`localhost` via `--add-host`. Ver `plan.md`, seção "Container local",
pra reproduzir o diagnóstico:

```bash
# de dentro de um container na MESMA rede (não no mesmo namespace) —
# reproduz o bug: retorna jwks_uri="http://localhost:9229/...", que
# esse container NÃO alcança
docker run --rm --network gastosapp-local --entrypoint sh \
  public.ecr.aws/lambda/provided:al2023 \
  -c "curl -s http://gastosapp-cognito-local:9229/<USER_POOL_ID>/.well-known/openid-configuration"

# com --network container:gastosapp-cognito-local (namespace
# compartilhado) o mesmo curl para "localhost:9229" funciona
docker run --rm --network container:gastosapp-cognito-local --entrypoint sh \
  public.ecr.aws/lambda/provided:al2023 \
  -c "curl -s http://localhost:9229/<USER_POOL_ID>/.well-known/openid-configuration"
```

### Rodar só a suíte, sem rebuildar a imagem (iteração rápida)

Se a imagem já existe e você só quer reiniciar o container (ex.: depois
de mudar só uma variável de ambiente), comente o passo de
`docker build` em `run-local.sh` temporariamente, ou rode o `docker run`
manualmente — os parâmetros exatos (env vars, `--network`) estão
documentados nos comentários de `infra/lambda/run-local.sh`.

### Container/estado ficaram "sujos" depois de uma falha

```bash
cd backend/infra
docker rm -f gastosapp-api-local-run-container 2>/dev/null  # se sobrou (normalmente o trap já limpa)
docker compose down
rm -rf .localstack .cognito-local .local-cognito-ids  # reseta o ambiente local do zero
docker compose up -d
./scripts/local-init.sh
```

### Windows/Git Bash: argumento corrompido silenciosamente

Se você rodar comandos `docker`/`aws` manualmente (fora dos scripts) e
algo com `/` no início (ex.: `/aws-lambda/aws-lambda-rie`,
`/GastosApp/...`) se comportar estranho, exporte
`MSYS_NO_PATHCONV=1` antes — MSYS/Git Bash reescreve esses argumentos
como caminho de arquivo Windows antes de chegar no programa. Os
scripts (`run-local.sh`, `local-init.sh` etc.) já fazem isso
internamente.

## 3. Debugar contra homologação/produção

Precisa de credenciais AWS reais com permissão na role
`gastosapp-backend-cicd` (ou equivalente) — localmente, geralmente via
`aws sso login` num profile com esse acesso. Rodar manualmente (fora do
CI):

```bash
cd backend
export AWS_PROFILE=<seu-profile-com-acesso>

# contra homologação
INTEGRATION_TESTS_MODE=hom \
INTEGRATION_TESTS_BASE_URL=https://api-hom.jrnexpenses.com \
INTEGRATION_TESTS_PARAMETER_STORE_PATH=/GastosApp/Hom/ \
dotnet test tests/GastosApp.IntegrationTests -c Release --filter "Category=Integration"

# contra produção — use com cautela, cria/remove uma conta real
INTEGRATION_TESTS_MODE=prod \
INTEGRATION_TESTS_BASE_URL=https://api.jrnexpenses.com \
INTEGRATION_TESTS_PARAMETER_STORE_PATH=/GastosApp/ \
dotnet test tests/GastosApp.IntegrationTests -c Release --filter "Category=Integration"
```

### Falha em `ResolveUserPoolIdAsync` (`ssm:GetParametersByPath` negado)

A role/usuário usado precisa da permissão `ssm:GetParametersByPath`
sob `/GastosApp` e `/GastosApp/Hom` — ver
`backend/infra/terraform/cicd/iam-policy.tf`, statement
`ReadIntegrationTestParameterStore`. Rodando localmente com um profile
sem essa permissão (ex.: `agent-toolkit`, que tem IAM restrito), troque
pro profile certo.

### Falha em `AdminConfirmSignUpAsync`/`AdminDeleteUserAsync` (`AccessDenied`)

Mesma ideia: permissão `cognito-idp:AdminConfirmSignUp`/
`AdminDeleteUser` nos User Pools de hom/prod, statement
`ManageIntegrationTestCognitoUser` no mesmo `iam-policy.tf`.

### Uma execução falhou no meio e deixou lixo em hom/prod

`TestAccountFixture.DisposeAsync` é best-effort — cada etapa de
limpeza roda mesmo se outra falhar, e loga no stderr o que não
conseguiu limpar (`[TestAccountFixture] Falha na ...: ...`). Se algo
ficou pra trás (usuário Cognito órfão, itens no DynamoDB), o e-mail
segue o padrão `int-test+<guid>@jrnexpenses.com` — dá pra localizar e
limpar manualmente:

```bash
# localizar/remover o usuário no Cognito (hom)
aws cognito-idp admin-get-user --user-pool-id <HOM_USER_POOL_ID> --username <email>
aws cognito-idp admin-delete-user --user-pool-id <HOM_USER_POOL_ID> --username <email>

# localizar/remover os itens no DynamoDB — via userId (sub do Cognito),
# não pelo e-mail
aws dynamodb query --table-name GastosApp-Hom \
  --key-condition-expression "PK = :pk" \
  --expression-attribute-values '{":pk":{"S":"USER#<userId>"}}'
```

## 4. Rodando um teste específico (qualquer modo)

```bash
dotnet test tests/GastosApp.IntegrationTests -c Release \
  --filter "FullyQualifiedName~AuthFlowTests.Login_CredenciaisInvalidas_Retorna401"
```

## 5. Por que a suíte não roda no `dotnet test GastosApp.sln` normal

Todo teste aqui tem `[Trait("Category", "Integration")]`. O `dotnet
test` usado no gate de qualidade (local e CI) filtra
`--filter "Category!=Integration"` de propósito — exige Docker/rede
real, não cabe no build rápido. Rodar esta suíte é sempre explícito
(`--filter "Category=Integration"` ou o `run-local.sh`).
