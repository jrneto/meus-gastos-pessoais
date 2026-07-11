# Constitution — GastosApp

## O que é este sistema
API de controle de gastos pessoal. Backend-first, frontend React depois.

## Regras imutáveis
- Valor monetário SEMPRE em centavos (long). Ex: R$ 49,90 = 4990
- userId SEMPRE extraído do JWT (Cognito sub), NUNCA do body do request
- Toda feature começa com uma spec em /docs/specs antes de qualquer código
- Todo novo endpoint deve ter testes de componente (mock de repositórios
  e dependências externas, ver `docs/specs/FEAT-03-testes-componentes.md`)
  antes de ser considerado concluído
- Sem Scan no DynamoDB — apenas Query com PK ou GSI definidos

## Stack
- Runtime: .NET 10, ASP.NET Core Minimal APIs
- Banco: DynamoDB single-table (tabela: GastosApp)
- Auth: AWS Cognito com JWT
- Infra local: Conexão direta aos serviços da AWS (DynamoDB, Cognito)

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
- Manter o Program.cs limpo, utilizando injeção de dependência nativa do .NET.
- Buscar sempre o uso de Native AOT no .NET 10 para otimizar os cold starts do AWS Lambda.

## Restrições de Arquitetura e Custo (Foco em Free Tier)
- Toda a infraestrutura deve ser estritamente Serverless.
- Banco de dados: Usar APENAS Amazon DynamoDB (Modo On-Demand). É expressamente proibido o uso de RDS, instâncias EC2 ou qualquer recurso com cobrança por hora ligada.
- Autenticação: Usar exclusivamente o Amazon Cognito User Pools.

## Padrões de Código e Arquitetura
- **Mediator Pattern:** Usar a biblioteca de Mediator/MediatR para desacoplar os Handlers de comandos/queries das Minimal APIs. As rotas devem apenas enviar (`Send`) o request para o mediator.
- **Result Pattern:** Nenhum Handler ou serviço deve lançar exceções para fluxo de negócio ou retornar null. Todos devem retornar um objeto `Result` ou `Result<T>` unificado (indicando Success ou Failure com mensagens de erro/status), e a controller/Minimal API mapeia esse Result para o respectivo HTTP Status Code (200, 400, 404, etc).
- **Result Pattern Customizado:** Não utilizar bibliotecas externas de Result (como FluentResults). Criar uma implementação própria e simples de `Result` e `Result<T>` dentro do projeto.