# Terraform — frontend/infra

Traz a infraestrutura de hosting do frontend para dentro do Terraform.
`environments/prod/` foi só `import` da infra que já existia, criada
manualmente via console (ver
`frontend/specs/FEAT-07-terraform-import-infra/`) — nenhum recurso foi
criado, recriado ou destruído nessa migração. `environments/hom/` foi
criado do zero via `apply` (ver
`frontend/specs/FEAT-08-ambiente-homologacao/`).

Três configurações independentes, cada uma com seu próprio state, todas
no bucket `gastosapp-terraform-state-648443184523` (reaproveitado do
backend, `key`s distintas — nenhum novo bootstrap é criado):

- **`dns/`** — camada **persistente**, nunca destruída pelo pipeline de
  CI/CD futuro (`destroy`/recreate da infra de aplicação). Gerencia a
  hosted zone `jrnexpenses.com.` (protegida com `prevent_destroy`), os 6
  records DNS de produção e os 3 de homologação (`hom.jrnexpenses.com`).
  Lê o domínio do CloudFront e os dados de validação de certificado ACM
  de cada ambiente via `terraform_remote_state` (um data source por
  ambiente, desacoplados entre si) — assim, se a infra de um ambiente for
  recriada no futuro, os records dele se atualizam sozinhos ao rodar
  `apply` aqui.
- **`environments/prod/`** — camada **efêmera**, destruível/recriável
  pelo pipeline futuro. Gerencia o bucket S3, a distribuição CloudFront,
  o certificado ACM (`jrnexpenses.com`) e o WAF WebACL de produção.
- **`environments/hom/`** — mesma estrutura de `environments/prod/`, mais
  um WAF WebACL próprio (aqui criado via `resource`, não importado).
  Distribuição assinada ao plano flat-rate **Free** do CloudFront
  (manualmente, ver seção abaixo) — custo US$0/mês.
- **`cicd/`** — OIDC Provider + IAM Role usados pelos workflows de
  deploy do GitHub Actions (`frontend/specs/FEAT-09-cicd-github-actions/`).
  **Não está no state hoje** — os recursos existem na conta, criados
  manualmente no console, mas o Terraform não consegue gerenciá-los
  (create/import) com as credenciais atuais. Código mantido como
  referência do estado desejado. Ver seção dedicada abaixo.

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

### 2. `dns/`

```bash
cd ../../dns
terraform init \
  -backend-config="bucket=gastosapp-terraform-state-648443184523" \
  -backend-config="region=us-east-1"

terraform import aws_route53_zone.main Z053098817OJTJ5LWHAZW

terraform import aws_route53_record.apex_a Z053098817OJTJ5LWHAZW_jrnexpenses.com_A
terraform import aws_route53_record.apex_aaaa Z053098817OJTJ5LWHAZW_jrnexpenses.com_AAAA
terraform import aws_route53_record.www_a Z053098817OJTJ5LWHAZW_www.jrnexpenses.com_A
terraform import aws_route53_record.www_aaaa Z053098817OJTJ5LWHAZW_www.jrnexpenses.com_AAAA

# CNAMEs de validação ACM (nomes exatos dos records a confirmar lendo o
# certificado real antes de rodar — ver acm.tf de environments/prod)
terraform import 'aws_route53_record.acm_validation["jrnexpenses.com"]' Z053098817OJTJ5LWHAZW__f91e552da643f7310e2ef48005c54b0d.jrnexpenses.com_CNAME
terraform import 'aws_route53_record.acm_validation["www.jrnexpenses.com"]' Z053098817OJTJ5LWHAZW__632a98ba4517a08bda86576acc344e22.www.jrnexpenses.com_CNAME

terraform plan   # deve bater "No changes" — ajuste o HCL até chegar lá
```

Cada `import`/`apply` é confirmado individualmente no momento da
execução — nenhum roda de forma autônoma (ver spec, US8).

### 3. `environments/hom/` (criação do zero, não import)

```bash
cd frontend/infra/terraform/environments/hom
terraform init \
  -backend-config="bucket=gastosapp-terraform-state-648443184523" \
  -backend-config="region=us-east-1"

terraform plan   # revisar antes de aplicar
terraform apply
```

**Dependência circular ACM → DNS → CloudFront**: como os recursos são
criados do zero (não importados), o certificado ACM nasce
`PENDING_VALIDATION` e o CloudFront recusa associá-lo enquanto não
virar `ISSUED` — mas a validação depende do CNAME em `dns/`, que por
sua vez normalmente viria depois. Ordem que funciona (usada na
FEAT-08):

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

**Checkpoint manual pós-`apply`**: a distribuição de hom nasce em
cobrança pay-as-you-go. No console AWS (CloudFront → Distributions →
`gastosapp-cdn-hom` → **Manage plan**), assinar o plano **Free**
(2º dos 3 disponíveis na conta) para zerar o custo — isso cobre a
distribuição + o WAF WebACL associado. Sem esse passo manual, a
distribuição fica com cobrança padrão. O recurso Terraform equivalente
(`aws_pricingplanmanager_subscription`) ainda não existe em nenhuma
versão publicada do provider — ver `frontend/infra/CLAUDE.md`.

## `cicd/` — OIDC Provider + IAM Role (fora do state, gerenciados manualmente)

`frontend/infra/terraform/cicd/` contém o código de referência
(`oidc.tf`, `iam-role.tf`, `iam-policy.tf`) para o OIDC Provider do
GitHub Actions e a IAM Role assumida pelos workflows de deploy
(`.github/workflows/frontend-deploy-{hom,prod}.yml`) — mas **os
recursos reais foram criados manualmente no console AWS, não pelo
Terraform**, e não estão no state desta config.

**Motivo**: tanto `terraform apply` (criação) quanto `terraform import`
(trazer o que já existe) falharam com `AccessDenied` — o perfil usado
(`agent-toolkit`, role `AWSReservedSSO_Perfil-Admin-Desenvolvedor`) não
tem permissão para nenhuma ação de leitura/escrita sobre
`aws_iam_openid_connect_provider`/Role relacionadas
(`iam:CreateOpenIDConnectProvider`, `iam:GetOpenIDConnectProvider`,
`iam:ListOpenIDConnectProviders`, `iam:GetRole`, `iam:GetRolePolicy`,
`iam:ListRolePolicies` — todas negadas), mesmo sendo um perfil
"Admin-Desenvolvedor". Aparenta ser um guardrail intencional (permission
set ou SCP da AWS Organization) contra ações de federação de
identidade/IAM, independente da permissão de admin no restante da
conta.

**Recursos existentes na conta** (criados manualmente, 2026-08-08):
- OIDC Provider: `arn:aws:iam::648443184523:oidc-provider/token.actions.githubusercontent.com`
- IAM Role: `arn:aws:iam::648443184523:role/gastosapp-frontend-cicd`
  - Trust policy e policy inline (`gastosapp-frontend-cicd-deploy`)
    criadas **byte a byte iguais** ao que `iam-role.tf`/`iam-policy.tf`
    gerariam — conferido visualmente no console (não via `terraform
    plan`, que também não funciona sem essas permissões).

**Se a permissão for liberada no futuro** (permission set/SCP ajustado
para permitir as ações acima), trazer para o state com:
```bash
cd frontend/infra/terraform/cicd
terraform import aws_iam_openid_connect_provider.github \
  arn:aws:iam::648443184523:oidc-provider/token.actions.githubusercontent.com
terraform import aws_iam_role.frontend_cicd gastosapp-frontend-cicd
terraform import aws_iam_role_policy.frontend_cicd \
  gastosapp-frontend-cicd:gastosapp-frontend-cicd-deploy
terraform plan   # deve dar "No changes" se o console bateu com o .tf
```

**Uso pelos workflows**: o ARN da Role
(`arn:aws:iam::648443184523:role/gastosapp-frontend-cicd`) é cadastrado
como variável `CICD_ROLE_ARN` nos GitHub Environments `hom`/`prod` — não
depende do state do Terraform para funcionar, só do recurso existir de
fato na conta (que existe, só não está sob Terraform).

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