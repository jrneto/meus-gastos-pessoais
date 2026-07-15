# Terraform — backend/infra

Provisiona a infraestrutura AWS do backend: tabela DynamoDB (`GastosApp`
+ `GSI1` + `GSI2`, `dynamodb.tf`), Cognito User Pool + App Client
(`cognito.tf`) e os parâmetros do Cognito no Parameter Store
(`parameter-store.tf`). Toda a infraestrutura do backend está sob
Terraform (ver `backend/specs/FEAT-09-terraform-cognito-parameter-store/`).

Duas configurações independentes:

- `bootstrap/` — cria o bucket S3 que guarda o state remoto da
  configuração principal. Mantém o **próprio state local** (não tem como
  o bucket gerenciar o state que o cria). Aplicado uma única vez por
  conta AWS (ou raramente, se precisar recriar o bucket).
- raiz deste diretório (`versions.tf`, `dynamodb.tf`, ...) — a
  configuração principal, com state remoto no bucket criado pelo
  `bootstrap/`, usando o locking nativo do backend S3 (`use_lockfile`,
  sem precisar de tabela DynamoDB extra só para lock).

## Pré-requisitos

- Terraform >= 1.10 instalado localmente
- AWS CLI configurado com credenciais válidas (profile `default`,
  região `us-east-1` — mesmo padrão usado pelo backend .NET em
  desenvolvimento local, ver `backend/docs/architecture.md`)
- Permissão na conta AWS para criar bucket S3 e tabela DynamoDB

## Passo a passo (primeira vez, a partir da sua máquina local)

### 1. Criar o bucket de state (bootstrap)

```bash
cd backend/infra/terraform/bootstrap
terraform init
terraform apply
```

Confirme a criação (`yes`). Ao final, anote o valor do output
`bucket_name` (algo como `gastosapp-terraform-state-123456789012`).

### 2. Inicializar a configuração principal apontando para esse bucket

```bash
cd ../   # backend/infra/terraform
terraform init \
  -backend-config="bucket=<bucket_name do passo 1>" \
  -backend-config="region=us-east-1"
```

O Terraform vai perguntar se quer copiar o state existente para o novo
backend — como é a primeira vez, não há state anterior a migrar, apenas
confirme.

### 3. Criar a tabela DynamoDB

```bash
terraform plan
terraform apply
```

Confirme (`yes`). A partir daqui, o state fica no S3 (com locking nativo
via `use_lockfile`), então é seguro rodar `terraform plan`/`apply` de
qualquer máquina que tenha as credenciais AWS configuradas — não é mais
um artefato só local.

## Execuções seguintes

Já com o backend configurado, basta:

```bash
cd backend/infra/terraform
terraform init   # se ainda não rodou nesta máquina
terraform plan
terraform apply
```

Não é necessário repetir o `bootstrap/` — ele só roda de novo se o
bucket de state precisar ser recriado.

## Convenções

- Nenhum novo recurso Terraform deve ser criado sem pedido explícito do
  usuário (ver `backend/infra/CLAUDE.md`).
- Cognito (`cognito.tf`) e Parameter Store (`parameter-store.tf`) são
  gerenciados por Terraform desde a FEAT-09. O User Pool/App Client
  atuais foram **recriados** (não importados) — o pool anterior, criado
  manualmente, foi mantido intacto até exclusão manual pelo usuário.
  Os 3 parâmetros do Parameter Store foram trazidos via
  `terraform import` (recurso simples, sem risco de dado).
