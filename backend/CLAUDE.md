# Backend GastosApp — Contexto para IA

**Antes de gerar código neste diretório, leia sempre:**
`backend/docs/constitution.md` (regras imutáveis) e
`backend/docs/architecture.md` (decisões de banco/infra). Ao trabalhar
dentro de `backend/infra/`, leia também `backend/infra/CLAUDE.md`. Para o
critério de quando abrir uma spec, veja "Modo Leve vs Fluxo Completo" no
[`/CLAUDE.md`](../CLAUDE.md) raiz.

## Stack
- .NET 10, ASP.NET Core Minimal APIs (sem controllers)
- DynamoDB single-table (tabela `GastosApp`, PAY_PER_REQUEST)
- AWS Cognito (fluxo `USER_PASSWORD_AUTH`) para autenticação, JWT validado
  contra o JWKS real do Cognito (nunca simulado)
- AWS Systems Manager Parameter Store, prefixo `/GastosApp/`, para
  configuração (não há segredo em `appsettings.json`)
- Deploy alvo: AWS Lambda via `Amazon.Lambda.AspNetCoreServer.Hosting`
- Ambiente local conecta-se diretamente aos recursos AWS reais — sem
  LocalStack, sem Kong, sem simulação

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
│   └── GastosApp.IntegrationTests/# esqueleto, não usado hoje (ver FEAT-03/spec.md)
├── docs/
│   ├── constitution.md
│   ├── architecture.md
│   ├── data-model.md
│   └── README.md                  # explica o fluxo SDD do backend
├── specs/
│   └── FEAT-XX-nome-feature/{spec.md, plan.md, tasks.md}
└── infra/
    ├── CLAUDE.md                  # contexto de infra do backend
    └── docker-compose.yml, kong.yml, scripts/  # legado LocalStack/Kong, ver infra/CLAUDE.md
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

## Comandos úteis

```bash
cd backend
dotnet build GastosApp.sln
dotnet test GastosApp.sln
dotnet run --project src/GastosApp.Api
```

## Padrão de spec (Fluxo Completo)

Cada feature vive em `backend/specs/{FEAT-XX-nome-feature}/`, nunca como
arquivo solto. Use os comandos `/specify`, `/plan`, `/tasks` e `/review`
(em `.claude/commands/`) para o ciclo completo — veja
`backend/docs/README.md` para o detalhamento do processo.
