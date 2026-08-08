# Architecture — GastosApp

## Banco de dados

### Decisão: DynamoDB single-table
Motivo: conta AWS fora do free tier. RDS mínimo ~US$15-20/mês.
DynamoDB tem free tier permanente. Custo zero para volume pessoal.
Trade-off aceito: queries analíticas ad hoc mais trabalhosas.
Mitigado definindo todos os access patterns antes de criar a tabela.

### Tabela: GastosApp
Billing: PAY_PER_REQUEST (on-demand)

Atributos-chave:
- PK (String): partition key principal
- SK (String): sort key principal  
- GSI1PK (String): partition key do índice por categoria
- GSI1SK (String): sort key do índice por categoria

Índices:
- GSI1: permite query por userId + categoria + mês

### Access patterns

| # | Query | Mecanismo |
|---|-------|-----------|
| AP1 | Transações de um mês | PK=USER#id SK begins_with TXN#YYYY-MM |
| AP2 | Gastos por categoria no mês | GSI1PK=USER#id#cat SK begins_with YYYY-MM |
| AP3 | Evolução anual (resumos) | PK=USER#id SK between SUMMARY#YYYY-01 and SUMMARY#YYYY-12 |
| AP4 | Últimos N meses | PK=USER#id SK begins_with SUMMARY# limit N desc |

## Infraestrutura local
- Desenvolvimento conectado diretamente aos serviços da AWS.
- Credenciais configuradas localmente via AWS CLI (utilizando o profile `default` e região `us-east-1`).
- Recursos na AWS (como DynamoDB e Cognito) são consumidos diretamente durante o desenvolvimento.


## Infraestrutura de produção (implementada — FEAT-10)
- Lambda .NET Native AOT (runtime customizado `provided.al2023`,
  adapter `Amazon.Lambda.AspNetCoreServer.Hosting`), 256MB/10s,
  provisionada via `backend/infra/terraform/lambda.tf`
- API Gateway HTTP API na frente da Lambda (`api-gateway.tf`), sem
  autorizador JWT no Gateway — autenticação continua só na aplicação
  (FEAT-01). Throttling de 5 req/s (10 de rajada) no stage
- Build/empacotamento do artefato AOT rodam num container Amazon Linux
  2023 (mesma base do runtime da Lambda, necessário por compatibilidade
  de glibc — ver `backend/infra/terraform/README.md` e
  `backend/specs/FEAT-10-deploy-lambda-aot-api-gateway/`)
- DynamoDB e Cognito na mesma conta AWS (FEAT-09)
- CloudWatch Logs com retenção de 14 dias
- Custo: DynamoDB e Lambda dentro do free tier permanente; API Gateway
  HTTP API tem custo por requisição (~US$1/milhão) fora do free tier de
  12 meses desta conta — desprezível no volume de uso pessoal previsto
