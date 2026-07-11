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


## Infraestrutura de produção (fase futura)
- Lambda + API Gateway (adapter: Amazon.Lambda.AspNetCoreServer)
- DynamoDB na mesma conta AWS
- Cognito User Pool dedicado
- Custo estimado: ~US$0 no free tier permanente do DynamoDB
