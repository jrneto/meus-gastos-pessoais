# Infra do Backend GastosApp — Contexto para IA

Consulte também [`/CLAUDE.md`](../../CLAUDE.md) raiz para o critério de Modo
Leve vs Fluxo Completo e a regra de organização de specs, e
[`/backend/CLAUDE.md`](../CLAUDE.md) para o contexto geral do backend.

## Princípios

- Toda infraestrutura é **100% AWS**. Não há ambiente simulado: mesmo em
  desenvolvimento local, o backend se conecta diretamente aos recursos
  AWS reais (Cognito, DynamoDB, Parameter Store) — ver
  `backend/docs/constitution.md` e `backend/docs/architecture.md`.
- **Sem LocalStack, sem Kong.** Nenhuma simulação local de infraestrutura
  AWS deve ser (re)introduzida.
- IaC feito **exclusivamente em Terraform** — não CloudFormation, não
  CDK. **Só gerar/alterar código Terraform para um recurso quando
  solicitado explicitamente pelo usuário.**
- Provisionamento via Terraform vive em `backend/infra/terraform/`,
  organizado por ambiente desde a FEAT-13
  (`environments/prod/`, `environments/hom/`): tabela DynamoDB
  (`GastosApp` + `GSI1` + `GSI2`), Cognito User Pool + App Client e
  parâmetros do Parameter Store — ver `backend/docs/architecture.md`,
  `backend/docs/data-model.md` e
  `backend/specs/FEAT-09-terraform-cognito-parameter-store/`. State
  remoto em bucket S3 (locking nativo do backend S3, `use_lockfile` —
  sem tabela DynamoDB extra só para lock), com `key` distinta por
  ambiente, criado por um módulo `bootstrap/` separado (fora da divisão
  por ambiente) que mantém o próprio state local (chicken-and-egg do
  bucket que guarda seu próprio state). Passo a passo completo em
  `backend/infra/terraform/README.md`.
- Cognito e Parameter Store estão sob Terraform desde a FEAT-09 (antes
  eram provisionados manualmente). Qualquer novo recurso ou mudança
  ainda exige pedido explícito do usuário.
- O domínio customizado da API (`api.jrnexpenses.com`) está sob
  Terraform desde a FEAT-12: certificado ACM (`acm.tf`), domínio
  customizado + mapeamento do API Gateway (`api-gateway-domain.tf`) e
  os records DNS correspondentes (`dns.tf`). A hosted zone
  `jrnexpenses.com.` continua gerenciada pelo Terraform do **frontend**
  (`frontend/infra/terraform/dns/`, FEAT-07) — o backend só a lê via
  `data "aws_route53_zone"` (por nome), sem duplicá-la ou geri-la — ver
  `backend/specs/FEAT-12-terraform-dominio-customizado-api/`.
- Desde a FEAT-13, `backend/infra/terraform/` está organizado em
  **duas configurações por ambiente**, cada uma com state próprio no
  mesmo bucket (`environments/prod/` e `environments/hom/`), replicando
  o padrão já adotado pelo Terraform do frontend
  (`frontend/infra/terraform/environments/prod/`). `bootstrap/`
  continua fora dessa divisão (não é por ambiente).

## Ambiente de homologação (FEAT-13)

Além de produção (`environments/prod/`, `api.jrnexpenses.com`), existe
um ambiente de homologação completo e isolado em `environments/hom/`,
exposto em `https://api-hom.jrnexpenses.com` — ver
`backend/specs/FEAT-13-ambiente-homologacao/`.

Isolamento total entre os dois ambientes:
- Tabela DynamoDB própria (`GastosApp-Hom`, mesmo modelo de dados de
  produção)
- Cognito User Pool + App Client próprios (`user-pool-gastos-app-hom`,
  `controle-gastos-spa-hom`)
- Parameter Store em prefixo distinto (`/GastosApp/Hom/...` vs.
  `/GastosApp/...`) — a Lambda de hom lê esse prefixo via a variável de
  ambiente `ParameterStore__Path`, que sobrepõe o default
  `/GastosApp/` usado em produção (mudança em
  `AwsParameterStoreExtensions.cs`/`Program.cs`, sem alterar contrato
  de API)
- Tabela DynamoDB isolada via a variável de ambiente
  `DynamoDb__TableName` na Lambda de hom. Achado durante a validação da
  FEAT-13: o binding de `DynamoDbOptions` via `services.Configure<T>()`
  falha silenciosamente sob Native AOT (mesmo problema já corrigido
  para `CognitoOptions` na FEAT-10) — corrigido em
  `InfrastructureServiceCollectionExtensions.cs` para leitura manual,
  mesmo padrão do Cognito. Qualquer novo `Options` lido de
  `IConfiguration` neste projeto deve seguir esse padrão manual, nunca
  `services.Configure<T>()`
- Lambda + API Gateway HTTP API próprios (`gastos-app-api-hom`),
  publicando o **mesmo artefato** (`infra/lambda/function.zip`) e o
  mesmo código/contrato de produção — não há build separado para hom
- Certificado ACM próprio (`api-hom.jrnexpenses.com`, emitido do zero,
  diferente de produção que foi importado já `ISSUED`)

Sem frontend de homologação ainda: CORS (`frontend_origins`) e
`callback_urls` do Cognito de hom usam valores de baixo risco
(`[]` e `http://localhost:5173`, respectivamente) — trocar quando
existir um frontend de homologação real.

## Estado legado (pendente de decisão)

`docker-compose.yml`, `kong.yml` e `scripts/` (incluindo
`scripts/localstack-init/`) são artefatos de uma abordagem anterior
baseada em LocalStack/Kong, que contradiz o princípio acima (infra 100%
AWS real, sem simulação). Não os use como referência para novo trabalho,
e não os modifique/estenda sem antes confirmar com o usuário — a remoção
ou substituição por Terraform é uma decisão pendente do usuário, ainda
não tomada.

## Specs

Quando este contexto começar a ter specs próprias de infraestrutura,
seguir o mesmo padrão do restante do backend:
`backend/specs/{FEAT-XX-nome}/{spec.md, plan.md, tasks.md}`, nunca
arquivo solto — não crie uma árvore de specs separada só para infra.
