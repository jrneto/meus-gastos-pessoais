# FEAT-07: Plano Técnico — Import da infra do frontend para Terraform

## Estratégia geral

**Import puro** para todos os recursos (nenhuma recriação, ao contrário
do precedente `FEAT-09` do backend, que recriou Cognito). Faz sentido
aqui porque:
- Não há dado de usuário em risco (diferente do User Pool do FEAT-09).
- Recriar CloudFront/ACM trocaria o domínio `*.cloudfront.net` e os
  valores de validação do certificado, invalidando DNS em produção sem
  necessidade — o objetivo desta feature é justamente parar de ter esse
  descasamento, não causar um novo.

Duas configurações Terraform independentes, cada uma com seu próprio
state (mesmo bucket, `key` distinta):

| Config | Camada | Recursos |
|---|---|---|
| `frontend/infra/terraform/environments/prod/` | efêmera (recriável por pipeline futuro) | S3, CloudFront, ACM, WAF WebACL |
| `frontend/infra/terraform/dns/` | persistente (nunca destruída) | Hosted zone + 6 records |

`dns/` lê `cloudfront_domain_name` e os dados de validação do ACM via
`terraform_remote_state` apontando para o state de `environments/prod/`
— nunca valores fixos, para que os records se autoatualizem se a infra
principal for recriada no futuro.

## Camadas afetadas

Apenas **Infrastructure (Terraform)**, dentro de `frontend/infra/terraform/`.
Nenhum código em `frontend/app/` é tocado — esta feature não altera
comportamento observável de nada, só passa a gerenciar recursos já
existentes. Também é atualizado `frontend/infra/CLAUDE.md` (hoje
desatualizado, ainda diz "frontend não iniciado / sem infra").

## Estrutura de arquivos proposta

```
frontend/infra/terraform/
├── environments/
│   └── prod/
│       ├── versions.tf        # backend s3 (key distinta), provider aws, required_version >=1.10
│       ├── variables.tf       # var.aws_region, var.domain_name, var.frontend_bucket_name
│       ├── s3.tf              # bucket + policy + public access block + encryption
│       ├── acm.tf              # aws_acm_certificate (data source, ver nota abaixo)
│       ├── cloudfront.tf       # OAC + distribution
│       ├── waf.tf              # wafv2_web_acl
│       └── outputs.tf          # cloudfront_domain_name, acm_certificate_arn, domain_validation_options
└── dns/
    ├── versions.tf             # backend s3 (key distinta), provider aws
    ├── variables.tf            # var.aws_region, var.domain_name, var.state_bucket, var.prod_state_key
    ├── remote_state.tf         # data "terraform_remote_state" "prod"
    └── route53.tf              # aws_route53_zone + 6x aws_route53_record

frontend/infra/terraform/README.md  # passo a passo de init/import, mesmo padrão de backend/infra/terraform/README.md
```

Nenhum módulo Terraform reutilizável é criado (decisão explícita da
spec, para não abstrair antes da hora).

## Contratos técnicos (blocos Terraform)

### `versions.tf` (ambas as configs, mesmo padrão do backend)

```hcl
terraform {
  required_version = ">= 1.10"
  required_providers {
    aws = { source = "hashicorp/aws", version = "~> 5.0" }
  }
  backend "s3" {
    key          = "gastosapp-frontend/<prod|dns>/terraform.tfstate"
    use_lockfile = true
  }
}
provider "aws" {
  region = var.aws_region # us-east-1 — obrigatório: CloudFront/ACM/WAF(CLOUDFRONT) só existem em us-east-1
}
```

`bucket`/`region` do backend `s3` continuam parciais (fornecidos via
`-backend-config` no `terraform init`, mesma convenção do backend) —
`bucket = "gastosapp-terraform-state-648443184523"` (reuso, sem novo
bootstrap).

### `environments/prod/variables.tf`

```hcl
variable "aws_region"           { default = "us-east-1" }
variable "domain_name"          { default = "jrnexpenses.com" }
variable "frontend_bucket_name" { default = "gastosapp-frontend-prod" }
```

### `environments/prod/s3.tf`

- `aws_s3_bucket "frontend"` — `bucket = var.frontend_bucket_name`
- `aws_s3_bucket_public_access_block "frontend"` — todos os 4 campos `true`
- `aws_s3_bucket_server_side_encryption_configuration "frontend"` — `sse_algorithm = "AES256"`
- `aws_s3_bucket_policy "frontend"` — `data "aws_iam_policy_document"` com
  `Allow s3:GetObject` para principal `cloudfront.amazonaws.com`,
  `condition { StringEquals AWS:SourceArn = aws_cloudfront_distribution.main.arn }`
- Sem `aws_s3_bucket_versioning` (spec confirma: sem versionamento hoje)

### `environments/prod/acm.tf`

`aws_acm_certificate "frontend"` como **recurso gerenciado** (não
`data source` — precisa ser importável e referenciado por
`aws_cloudfront_distribution.viewer_certificate.acm_certificate_arn`).
Atributos a confirmar durante a implementação lendo o certificado real
(`domain_name`, `subject_alternative_names = ["www.jrnexpenses.com"]`,
`validation_method = "DNS"`, `key_algorithm`).

### `environments/prod/cloudfront.tf`

- `aws_cloudfront_origin_access_control "frontend"` — id atual `E1ZY2CM7WZ1H6`, `signing_behavior = "always"`, `signing_protocol = "sigv4"`, `origin_access_control_origin_type = "s3"`
- `aws_cloudfront_distribution "main"`:
  - `aliases = [var.domain_name, "www.${var.domain_name}"]`
  - `origin { domain_name = aws_s3_bucket.frontend.bucket_regional_domain_name, origin_access_control_id = aws_cloudfront_origin_access_control.frontend.id }`
  - `default_root_object = "index.html"`
  - `default_cache_behavior { viewer_protocol_policy = "redirect-to-https", cache_policy_id = "658327ea-f89d-4fab-a63d-7e88639e58f6" }` (cache policy gerenciada pela AWS, referenciada por ID fixo — não é um recurso deste projeto)
  - `viewer_certificate { acm_certificate_arn = aws_acm_certificate.frontend.arn, ssl_support_method = "sni-only", minimum_protocol_version = <confirmar valor atual> }`
  - `web_acl_id = aws_wafv2_web_acl.frontend.arn`
  - `price_class = "PriceClass_All"`, `http_version = "http2"`, `is_ipv6_enabled = true`
  - `restrictions { geo_restriction { restriction_type = "none" } }`

### `environments/prod/waf.tf`

`aws_wafv2_web_acl "frontend"` — `scope = "CLOUDFRONT"` (exige provider
em `us-east-1`), `default_action { allow {} }`, 3 blocos `rule` (um por
managed rule group: `AWSManagedRulesAmazonIpReputationList`,
`AWSManagedRulesCommonRuleSet`, `AWSManagedRulesKnownBadInputsRuleSet`),
cada um com `override_action { none {} }` e `visibility_config` — nomes,
prioridades e `sampled_requests_enabled`/`cloudwatch_metrics_enabled`
exatos a confirmar lendo a config real durante a implementação (import
primeiro, depois ajustar HCL até `plan` bater "No changes").

### `environments/prod/outputs.tf`

```hcl
output "cloudfront_domain_name"      { value = aws_cloudfront_distribution.main.domain_name }
output "cloudfront_hosted_zone_id"   { value = aws_cloudfront_distribution.main.hosted_zone_id }
output "acm_domain_validation_options" { value = aws_acm_certificate.frontend.domain_validation_options }
```

Esses outputs são o único ponto de acoplamento com a config `dns/` (via
`terraform_remote_state`), conforme US6.

### `dns/remote_state.tf`

```hcl
data "terraform_remote_state" "prod" {
  backend = "s3"
  config = {
    bucket = "gastosapp-terraform-state-648443184523"
    key    = "gastosapp-frontend/prod/terraform.tfstate"
    region = var.aws_region
  }
}
```

### `dns/route53.tf`

- `aws_route53_zone "main"` — `name = var.domain_name`, com
  `lifecycle { prevent_destroy = true }` (mesmo padrão do bucket de
  state em `backend/infra/terraform/bootstrap/main.tf`)
- 4x `aws_route53_record` alias (apex A/AAAA, `www` A/AAAA) —
  `zone_id = aws_route53_zone.main.zone_id`, `alias { name =
  data.terraform_remote_state.prod.outputs.cloudfront_domain_name,
  zone_id = data.terraform_remote_state.prod.outputs.cloudfront_hosted_zone_id,
  evaluate_target_health = false }`
- 2x `aws_route53_record` CNAME de validação ACM — `for_each` sobre
  `data.terraform_remote_state.prod.outputs.acm_domain_validation_options`,
  filtrando pelos 2 domínios do frontend (`jrnexpenses.com`,
  `www.jrnexpenses.com`) para não colidir com o CNAME de
  `api.jrnexpenses.com` (fora de escopo, gerido pelo backend)

## Ordem de execução (import)

`terraform import` não exige que as referências entre recursos já
estejam resolvidas (ele só liga um ID real da AWS ao endereço do
recurso no state) — então a ordem entre S3/CloudFront/ACM/WAF não é
crítica. Ordem sugerida, por menor risco de erro de digitação do ID:

1. `environments/prod`: S3 bucket → public access block → encryption →
   bucket policy → ACM certificate → OAC → WAF WebACL → CloudFront
   distribution (por último, pois referencia todos os outros)
2. `terraform plan` na config `prod` → **usuário aprova antes de
   qualquer ajuste ser considerado final**; iterar HCL até "No changes"
3. `dns`: hosted zone → 6 records
4. `terraform plan` na config `dns` → mesmo processo até "No changes"

Cada comando `terraform import`/`plan`/`apply` é confirmado
individualmente no momento da execução (US8) — nenhum roda de forma
autônoma.

## Recursos AWS afetados

**Nenhum recurso novo é criado.** Todos os recursos abaixo já existem
na conta e só passam a ser referenciados/gerenciados pelo Terraform via
`import`:
- S3: bucket `gastosapp-frontend-prod` (+ policy, public access block, encryption)
- CloudFront: distribuição `E2YCZNS0F94SCU` + OAC `E1ZY2CM7WZ1H6`
- ACM: certificado `jrnexpenses.com` (`arn:...certificate/a29d5ddb-...`)
- WAF: WebACL `CreatedByCloudFront-8ee8deea`
- Route 53: hosted zone `Z053098817OJTJ5LWHAZW` + 6 records do frontend
- State: reaproveita o bucket `gastosapp-terraform-state-648443184523`
  (já existe, criado pelo `backend/infra/terraform/bootstrap/`), com 2
  novas `key`s (`gastosapp-frontend/prod/terraform.tfstate` e
  `gastosapp-frontend/dns/terraform.tfstate`)

## Mapeamento de erros

Não aplicável — não há mudança de contrato de API, endpoint ou fluxo de
erro de aplicação. Efeito observável esperado: nenhum (é o próprio
critério de aceite via `terraform plan` → "No changes").

## Documentação a atualizar

- `frontend/infra/CLAUDE.md`: hoje diz "frontend ainda não foi iniciado
  / pasta sem conteúdo" — atualizar para descrever a arquitetura de duas
  camadas (`dns/` persistente + `environments/prod/` efêmera), que a
  infra de hosting (S3/CloudFront/ACM/WAF) e o DNS passam a ser
  gerenciados por Terraform, e o reuso do bucket de state do backend.
- `frontend/infra/terraform/README.md` (novo): passo a passo de
  `terraform init` (com `-backend-config` para bucket/region, mesma
  convenção do backend) e da sequência de `import` de cada config,
  mesmo padrão de `backend/infra/terraform/README.md`.

## Decisões confirmadas pelo usuário

1. **Valores exatos de atributos "de detalhe"** (prioridades e nomes das
   3 regras do WAF, `minimum_protocol_version` do certificado no
   viewer_certificate, `key_algorithm` do ACM) não são fixados neste
   plano — serão lidos da conta real (`aws cloudfront
   get-distribution-config`, `aws wafv2 get-web-acl`, `aws acm
   describe-certificate`) durante a implementação, iterando o HCL até
   `terraform plan` bater "No changes".
2. **`README.md`** da nova pasta Terraform faz parte do escopo desta
   feature (adicionado à estrutura de arquivos acima).
3. **Nomes dos `resource` blocks** definidos neste plano
   (`aws_s3_bucket.frontend`, `aws_cloudfront_distribution.main`,
   `aws_acm_certificate.frontend`, `aws_wafv2_web_acl.frontend`,
   `aws_cloudfront_origin_access_control.frontend`,
   `aws_route53_zone.main`) são finais — usar exatamente esses no
   `tasks.md` e na implementação, sem renomear depois do `import` (evita
   `terraform state mv`).
