# Terraform — frontend/infra

Traz a infraestrutura de hosting do frontend para dentro do Terraform.
`environments/prod/` foi só `import` da infra que já existia, criada
manualmente via console (ver
`frontend/specs/FEAT-07-terraform-import-infra/`) — nenhum recurso foi
criado, recriado ou destruído nessa migração. `environments/hom/` foi
criado do zero via `apply` (ver
`frontend/specs/FEAT-08-ambiente-homologacao/`).

Duas configurações independentes, cada uma com seu próprio state, ambas
no bucket `gastosapp-terraform-state-648443184523` (reaproveitado do
backend, `key`s distintas — nenhum novo bootstrap é criado):

- **`dns/`** — movida para o repositório `infra-jrnexpenses`
  (`terraform/dns/`) na FEAT-34, etapa 2. Gerenciava a hosted zone
  `jrnexpenses.com.` e os records de prod/hom; hoje lê o domínio do
  CloudFront e os dados de validação ACM de cada ambiente via
  `terraform_remote_state` — a `key` de hom já aponta para
  `infra-jrnexpenses/hom/terraform.tfstate` (etapa 3); a de prod ainda
  aponta para este monorepo até a etapa 4 migrar.
- **`environments/prod/`** — camada **efêmera**, destruível/recriável
  pelo pipeline futuro. Gerencia o bucket S3, a distribuição CloudFront,
  o certificado ACM (`jrnexpenses.com`) e o WAF WebACL de produção
  (migração para `infra-jrnexpenses` é a FEAT-34, etapa 4 — ainda não
  feita).
- **`environments/hom/`** — desde a FEAT-34, etapa 3, gerencia **só o
  workload**: bucket S3 + public access block + encryption + bucket
  policy. OAC, distribuição CloudFront, certificado ACM
  (`hom.jrnexpenses.com`) e WAF WebACL (a **plataforma**) vivem em
  `infra-jrnexpenses/terraform/environments/hom/` — a bucket policy lê
  o ARN da distribuição de lá via `terraform_remote_state`
  (`remote_state.tf`).
- **`cicd/`** — OIDC Provider + IAM Role usados pelos workflows de
  deploy do GitHub Actions (`frontend/specs/FEAT-09-cicd-github-actions/`)
  — movido para o repositório `infra-jrnexpenses`
  (`terraform/cicd/frontend/`) na FEAT-34, etapa 1. Ver seção dedicada
  abaixo.

## Pré-requisitos

- Terraform >= 1.10 instalado localmente
- AWS CLI autenticado na conta do projeto (`648443184523`,
  perfil `agent-toolkit`, região `us-east-1`)
- Nenhuma permissão de criação é necessária para o `import` em si — só
  leitura dos recursos e escrita no bucket de state já existente

## Ordem de execução (primeira vez)

`terraform import` não exige que as referências entre recursos já
estejam resolvidas — só liga um ID real da AWS ao endereço do recurso no
state. Ainda assim, siga esta ordem (evita confusão ao revisar o
`plan` depois):

### 1. `environments/prod/`

```bash
cd frontend/infra/terraform/environments/prod
terraform init \
  -backend-config="bucket=gastosapp-terraform-state-648443184523" \
  -backend-config="region=us-east-1"

terraform import aws_s3_bucket.frontend gastosapp-frontend-prod
terraform import aws_s3_bucket_public_access_block.frontend gastosapp-frontend-prod
terraform import aws_s3_bucket_server_side_encryption_configuration.frontend gastosapp-frontend-prod
terraform import aws_s3_bucket_policy.frontend gastosapp-frontend-prod

terraform import aws_acm_certificate.frontend arn:aws:acm:us-east-1:648443184523:certificate/a29d5ddb-d617-400f-95d1-aca8b9d3a64a

terraform import aws_wafv2_web_acl.frontend dad6fab1-e0cb-48e6-aa48-57459260f456/CreatedByCloudFront-8ee8deea/CLOUDFRONT

terraform import aws_cloudfront_origin_access_control.frontend E1ZY2CM7WZ1H6
terraform import aws_cloudfront_distribution.main E2YCZNS0F94SCU

terraform plan   # deve bater "No changes" — ajuste o HCL até chegar lá
```

Cada `import`/`apply` é confirmado individualmente no momento da
execução — nenhum roda de forma autônoma (ver spec, US8).

`dns/` não vive mais aqui — passo de import equivalente, quando
necessário (ex.: reconstrução em conta nova), fica documentado no
`README.md` do `infra-jrnexpenses`.

### 2. `environments/hom/` (hoje: só workload — bucket + policy)

```bash
cd frontend/infra/terraform/environments/hom
terraform init \
  -backend-config="bucket=gastosapp-terraform-state-648443184523" \
  -backend-config="region=us-east-1"

terraform plan   # deve bater "No changes" — a plataforma (CloudFront/
                  # ACM/WAF) é lida via terraform_remote_state de
                  # infra-jrnexpenses/terraform/environments/hom/,
                  # não gerenciada aqui
```

**Histórico (FEAT-08, antes da FEAT-34 etapa 3)**: quando esta config
ainda era dona da plataforma inteira (bucket + OAC + distribuição
CloudFront + ACM + WAF, criados do zero via `apply`, não `import`),
existia uma dependência circular ACM → DNS → CloudFront — o certificado
ACM nasce `PENDING_VALIDATION` e o CloudFront recusa associá-lo
enquanto não virar `ISSUED`, mas a validação depende do CNAME em
`dns/`, que por sua vez normalmente viria depois. Ordem usada na época
(`dns/` também vivia neste monorepo):

```bash
# 1) primeiro apply em hom/ — cria bucket, WAF, OAC e o certificado
#    ACM (PENDING_VALIDATION); a distribuição falha com
#    "InvalidViewerCertificate" (esperado, não é erro real)
terraform apply

# 2) aplicar só o CNAME de validação do ACM em dns/ (a distribuição
#    ainda não existe, então -target evita erro nos records hom_a/hom_aaaa)
cd ../../dns
terraform apply -target='aws_route53_record.acm_validation_hom["hom.jrnexpenses.com"]'

# 3) aguardar o certificado virar ISSUED (alguns minutos)
aws acm describe-certificate --region us-east-1 \
  --certificate-arn <arn do certificado> \
  --query 'Certificate.Status' --output text

# 4) completar o apply em hom/ — cria a distribuição + bucket policy
cd ../environments/hom
terraform apply

# 5) apply completo em dns/ — cria hom_a/hom_aaaa (distribuição já existe)
cd ../../dns
terraform apply
```

Se a plataforma de hom precisar ser recriada do zero no futuro (ex.:
conta AWS nova), esse mesmo procedimento se aplica a
`infra-jrnexpenses/terraform/environments/hom/` (que hoje concentra
OAC/distribuição/ACM/WAF) em conjunto com `infra-jrnexpenses/terraform/dns/`
— **checkpoint manual pós-`apply`** nesse caso: a distribuição nasce em
cobrança pay-as-you-go; no console AWS (CloudFront → Distributions →
`gastosapp-cdn-hom` → **Manage plan**), assinar o plano **Free** (2º
dos 3 disponíveis na conta) para zerar o custo — cobre distribuição +
WAF associado. O recurso Terraform equivalente
(`aws_pricingplanmanager_subscription`) ainda não existe em nenhuma
versão publicada do provider — ver `CLAUDE.md` do `infra-jrnexpenses`.

## `cicd/` — movido para `infra-jrnexpenses`

O código de referência do OIDC Provider + IAM Role do CI/CD do frontend
(`oidc.tf`, `iam-role.tf`, `iam-policy.tf` — recursos criados
manualmente no console, fora do state por causa do guardrail de IAM do
perfil `agent-toolkit`) vive agora em
`infra-jrnexpenses/terraform/cicd/frontend/` (FEAT-34, etapa 1). ARNs,
motivo do guardrail e comando de `import` futuro: `README.md` daquele
repositório.

O ARN da Role (`arn:aws:iam::648443184523:role/gastosapp-frontend-cicd`)
continua cadastrado como variável `CICD_ROLE_ARN` nos GitHub
Environments `hom`/`prod` deste monorepo — não depende de onde o
Terraform de referência vive, só do recurso existir de fato na conta.

## Explicitamente fora desta config

- Records `NS`/`SOA` da zona (default, criados junto com a hosted zone)
- Record de `api.jrnexpenses.com` e seu CNAME de validação ACM
  (pertencem ao contexto backend)
- O registro do domínio em si (`jrnexpenses.com`, comprado via Amazon
  Registrar) — migrá-lo é um processo de transferência de registrador,
  fora do alcance do Terraform

## Convenções

- Nenhum novo recurso Terraform deve ser criado sem pedido explícito do
  usuário (ver `frontend/infra/CLAUDE.md`).
- Deploy do build para o bucket de hom continua manual: `cd
  frontend/app && npm run build:hom && aws s3 sync dist/
  s3://gastosapp-frontend-hom/ --delete`. Requer `frontend/app/.env.hom`
  local (não versionado, a partir de `.env.hom.example`).