# Backend GastosApp — Contexto para IA

**Antes de gerar código neste diretório, leia sempre:**
`backend/docs/constitution.md` (regras imutáveis) e
[`/docs/architecture.md`](../docs/architecture.md) (arquitetura C4 do
monorepo completo — `backend/docs/architecture.md` é hoje só um
ponteiro pra lá). Ao trabalhar dentro de `backend/infra/`, leia também
`backend/infra/CLAUDE.md`. Para o critério de quando abrir uma spec,
veja "Modo Leve vs Fluxo Completo" no [`/CLAUDE.md`](../CLAUDE.md) raiz.

## Stack
- .NET 10, ASP.NET Core Minimal APIs (sem controllers)
- DynamoDB single-table (tabela `GastosApp`, PAY_PER_REQUEST)
- AWS Cognito (fluxo `USER_PASSWORD_AUTH`) para autenticação, JWT validado
  contra o JWKS real do Cognito (nunca simulado)
- AWS Systems Manager Parameter Store, prefixo `/GastosApp/`, para
  configuração (não há segredo em `appsettings.json`)
- Deploy alvo: AWS Lambda via `Amazon.Lambda.AspNetCoreServer.Hosting` —
  automatizado via GitHub Actions desde a FEAT-14 (deploy em hom a cada
  push em `develop`, deploy em prod a partir de uma GitHub Release
  `backend-vX.Y.Z`), ver `backend/infra/CLAUDE.md`
- Ambiente local roda contra serviços emulados em Docker desde a
  FEAT-18 — LocalStack (DynamoDB + SSM Parameter Store) e cognito-local
  (Cognito) — sem depender de credenciais AWS reais; produção e
  homologação continuam 100% AWS real. Ver `backend/infra/CLAUDE.md` e
  `backend/infra/README.md`

## Estrutura de projetos (Clean Architecture)

```
backend/
├── GastosApp.sln
├── src/
│   ├── GastosApp.Api/             # Minimal API endpoints, middlewares, Program.cs
│   ├── GastosApp.Application/     # Handlers (Mediator), Result Pattern, interfaces
│   ├── GastosApp.Domain/          # Entidades, value objects, regras de negócio puras
│   └── GastosApp.Infrastructure/  # DynamoDB, Cognito, config (AWS SDK)
├── tests/
│   ├── GastosApp.UnitTests/       # xUnit + NSubstitute + FluentAssertions
│   ├── GastosApp.ComponentTests/  # WebApplicationFactory + mocks (ver FEAT-03)
│   └── GastosApp.IntegrationTests/# suíte black-box multiambiente (local/hom/prod, ver FEAT-29)
├── docs/
│   ├── constitution.md
│   ├── architecture.md            # ponteiro pra /docs/architecture.md (raiz)
│   ├── data-model.md
│   ├── openapi.json                # contrato OpenAPI exportado (ver scripts/export-openapi.sh)
│   ├── backlog.md                 # sequência de FEATs + débitos técnicos/melhorias futuras
│   └── README.md                  # explica o fluxo SDD do backend
├── specs/
│   └── FEAT-XX-nome-feature/{spec.md, plan.md, tasks.md}
└── infra/
    ├── CLAUDE.md                  # contexto de infra do backend
    ├── README.md                  # como subir o ambiente local (FEAT-18)
    ├── docker-compose.yml         # LocalStack + cognito-local (dev local)
    ├── cognito-local/             # Dockerfile do emulador de Cognito
    ├── scripts/                   # seed idempotente (Cognito, DynamoDB, Parameter Store)
    └── terraform/                 # infra real (produção/homologação)
```

Fluxo de dependência: `Api → Application → Domain` e
`Infrastructure → Application/Domain` (Infrastructure implementa
interfaces definidas em Application). `Domain` não depende de nada.

## Convenções que valem só para o backend

- Valor monetário sempre em centavos (`long`)
- `userId` sempre extraído do JWT (claim `sub`), nunca do corpo do request
- Mediator (`Mediator` da lib martinothamar) — rotas só fazem
  `sender.Send(command, ct)`, nunca chamam handlers diretamente
- Result Pattern customizado (`GastosApp.Application.Common.Results`) —
  proibido lançar exceção para fluxo de negócio, proibido lib externa de
  Result (ex.: FluentResults)
- Erros HTTP seguem RFC 9457 (`ProblemDetails`), mapeados a partir do
  `Result` em `GastosApp.Api/Common/ResultHttpExtensions.cs`
- Sem `Scan` no DynamoDB — apenas `Query` com PK ou GSI definidos
- Todo novo endpoint precisa de teste de componente (mock de
  repositórios/dependências externas), ver
  `backend/specs/FEAT-03-testes-componentes/spec.md`
- Todo novo endpoint **também** precisa de teste integrado (contra a
  API real, sem dublês — `GastosApp.IntegrationTests`), cobrindo pelo
  menos o fluxo de sucesso, ver
  `backend/specs/FEAT-29-testes-integrados/spec.md`. Roda localmente
  via `backend/infra/lambda/run-local.sh` (binário Native AOT publicado,
  via Runtime Interface Emulator — pega erro de AOT antes do deploy);
  em CI, roda contra hom/prod (`--filter Category=Integration`), fora
  do `dotnet test GastosApp.sln` padrão

## Comandos úteis

```bash
cd backend
dotnet build GastosApp.sln
dotnet test GastosApp.sln
dotnet run --project src/GastosApp.Api
./scripts/export-openapi.sh   # regenera docs/openapi.json a partir do contrato real
```

## Contrato de API

`backend/docs/openapi.json` é a fonte de verdade do contrato de wire
(endpoints, request/response, status codes), consumida pelo frontend.
**Toda mudança de contrato exige regenerar esse arquivo antes de a
feature ser considerada concluída** (ver `backend/docs/constitution.md`).
Regras de negócio/validação que o schema não expressa continuam em
`backend/specs/`.

## Padrão de spec (Fluxo Completo)

Cada feature vive em `backend/specs/{FEAT-XX-nome-feature}/`, nunca como
arquivo solto. Use os comandos `/specify`, `/plan`, `/tasks` e `/review`
(em `.claude/commands/`) para o ciclo completo — veja
`backend/docs/README.md` para o detalhamento do processo.
