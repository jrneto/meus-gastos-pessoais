# Constitution — GastosApp

## O que é este sistema
API de controle de gastos pessoal. Backend-first, frontend React depois.

## Regras imutáveis
- Valor monetário SEMPRE em centavos (long). Ex: R$ 49,90 = 4990
- userId SEMPRE extraído do JWT (Cognito sub), NUNCA do body do request
- Toda feature começa com uma spec em /docs/specs antes de qualquer código
- Sem Scan no DynamoDB — apenas Query com PK ou GSI definidos

## Stack
- Runtime: .NET 10, ASP.NET Core Minimal APIs
- Banco: DynamoDB single-table (tabela: GastosApp)
- Auth: AWS Cognito com JWT
- Infra local: LocalStack na porta 4566
- Gateway local: Kong na porta 8000

## Padrão de camadas
- Api → recebe request, valida JWT, chama Application
- Application → use cases, orquestra Domain e Infrastructure  
- Domain → entidades, value objects, regras de negócio puras
- Infrastructure → DynamoDB, Cognito, integrações externas

## Convenções de código
- Endpoints no plural: /transactions, /categories
- Datas em ISO 8601: "2025-06-15"
- Respostas de erro seguem RFC 9457 (ProblemDetails)
- Logs estruturados com Serilog
