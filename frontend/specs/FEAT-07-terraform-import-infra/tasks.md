# FEAT-07: Tasks — Import da infra do frontend para Terraform

Cada task de `terraform import`/`apply` exige aprovação explícita do
usuário no momento da execução (US8) — nenhuma rodou de forma autônoma;
os imports abaixo foram executados após aprovação explícita do usuário
em duas rodadas (uma por config).

## Config `environments/prod/` — scaffolding

- [x] 1. Criar `frontend/infra/terraform/environments/prod/versions.tf`
      (backend S3 com `key = "gastosapp-frontend/prod/terraform.tfstate"`,
      `use_lockfile = true`, provider `aws` `~> 5.0`, `region = var.aws_region`)
- [x] 2. Criar `frontend/infra/terraform/environments/prod/variables.tf`
      (`aws_region`, `domain_name`, `frontend_bucket_name`)
- [x] 3. Rodar `terraform init` com `-backend-config` para bucket/region
      (state remoto no bucket `gastosapp-terraform-state-648443184523`)

## Config `environments/prod/` — S3

- [x] 4. Escrever `s3.tf`: `aws_s3_bucket.frontend`,
      `aws_s3_bucket_public_access_block.frontend`,
      `aws_s3_bucket_server_side_encryption_configuration.frontend`,
      `aws_s3_bucket_policy.frontend` (valores replicando exatamente o
      observado na conta: bucket `gastosapp-frontend-prod`, SSE-S3,
      public access block todo `true`, policy restrita ao principal
      `cloudfront.amazonaws.com`, `Version = "2008-10-17"` literal para
      bater byte a byte com a policy gerada pelo console)
- [x] 5. `terraform import aws_s3_bucket.frontend gastosapp-frontend-prod`
- [x] 6. `terraform import aws_s3_bucket_public_access_block.frontend gastosapp-frontend-prod`
- [x] 7. `terraform import aws_s3_bucket_server_side_encryption_configuration.frontend gastosapp-frontend-prod`
- [x] 8. `terraform import aws_s3_bucket_policy.frontend gastosapp-frontend-prod`

## Config `environments/prod/` — ACM

- [x] 9. Ler o certificado real (`aws acm describe-certificate`) —
      confirmado `KeyAlgorithm: RSA-2048` (`RSA_2048` no HCL)
- [x] 10. Escrever `acm.tf`: `aws_acm_certificate.frontend`
      (`domain_name`, `subject_alternative_names`, `validation_method = "DNS"`,
      `key_algorithm = "RSA_2048"`)
- [x] 11. `terraform import aws_acm_certificate.frontend arn:aws:acm:us-east-1:648443184523:certificate/a29d5ddb-d617-400f-95d1-aca8b9d3a64a`

## Config `environments/prod/` — WAF

- [x] 12. Ler o WebACL real (`aws wafv2 get-web-acl`) — confirmado
      nomes/prioridades das 3 regras (`AWS-AWSManagedRulesAmazonIpReputationList`
      prioridade 0, `AWS-AWSManagedRulesCommonRuleSet` prioridade 1,
      `AWS-AWSManagedRulesKnownBadInputsRuleSet` prioridade 2) e que o
      WebACL não tem `description` (campo omitido no HCL)
- [x] 13. Escrever `waf.tf`: `aws_wafv2_web_acl.frontend` (scope
      `CLOUDFRONT`, `default_action allow`, as 3 `rule` com
      `override_action none` replicando exatamente o WebACL atual)
- [x] 14. `terraform import aws_wafv2_web_acl.frontend dad6fab1-e0cb-48e6-aa48-57459260f456/CreatedByCloudFront-8ee8deea/CLOUDFRONT`
      (nota: formato do ID é `ID/NAME/SCOPE`, com `/`, não `,`)

## Config `environments/prod/` — CloudFront

- [x] 15. Ler a distribuição real (`aws cloudfront get-distribution-config`)
      — confirmado `minimum_protocol_version = "TLSv1.2_2021"`,
      `compress = true`, `origin_id` auto-gerado pelo console, sem
      `s3_origin_config` no state (origin usa só OAC)
- [x] 16. Escrever `cloudfront.tf`:
      `aws_cloudfront_origin_access_control.frontend` +
      `aws_cloudfront_distribution.main` (origem via OAC, aliases,
      cache policy gerenciada `658327ea-f89d-4fab-a63d-7e88639e58f6`,
      viewer certificate referenciando `aws_acm_certificate.frontend`,
      `web_acl_id` referenciando `aws_wafv2_web_acl.frontend`, tags
      `Name = "gastosapp-cdn"`)
- [x] 17. `terraform import aws_cloudfront_origin_access_control.frontend E1ZY2CM7WZ1H6`
- [x] 18. `terraform import aws_cloudfront_distribution.main E2YCZNS0F94SCU`

## Config `environments/prod/` — outputs e verificação

- [x] 19. Escrever `outputs.tf` (`cloudfront_domain_name`,
      `cloudfront_hosted_zone_id`, `acm_domain_validation_options`)
- [x] 20. Rodar `terraform plan` na config `prod` — resultado final:
      **"No changes. Your infrastructure matches the configuration."**

## Config `dns/` — scaffolding

- [x] 21. Criar `frontend/infra/terraform/dns/versions.tf` (backend S3
      com `key = "gastosapp-frontend/dns/terraform.tfstate"`, mesmo
      bucket de state, provider `aws`)
- [x] 22. Criar `frontend/infra/terraform/dns/variables.tf`
      (`aws_region`, `domain_name`)
- [x] 23. Rodar `terraform init` com `-backend-config` para bucket/region

## Config `dns/` — recursos

- [x] 24. Escrever `remote_state.tf` (`data "terraform_remote_state" "prod"`
      apontando para o state da config `environments/prod/`)
- [x] 25. Escrever `route53.tf`: `aws_route53_zone.main` com
      `lifecycle { prevent_destroy = true }` (`comment` mantido igual ao
      original, `"HostedZone created by Route53 Registrar"`)
- [x] 26. `terraform import aws_route53_zone.main Z053098817OJTJ5LWHAZW`
- [x] 27. Adicionar a `route53.tf` os 4 `aws_route53_record` alias
      (apex A/AAAA, `www` A/AAAA) referenciando
      `data.terraform_remote_state.prod.outputs.cloudfront_domain_name`/`cloudfront_hosted_zone_id`
- [x] 28. `terraform import` dos 4 records alias (`ZONEID_nome_TIPO`,
      um comando por record)
- [x] 29. Adicionar a `route53.tf` os 2 `aws_route53_record` CNAME de
      validação ACM via `for_each` sobre
      `data.terraform_remote_state.prod.outputs.acm_domain_validation_options`
      (já escopado ao certificado do frontend, sem colisão com o de
      `api.jrnexpenses.com`)
- [x] 30. `terraform import` dos 2 records CNAME de validação (um
      comando por record)

## Config `dns/` — verificação

- [x] 31. Rodar `terraform plan` na config `dns` — resultado final:
      **"No changes. Your infrastructure matches the configuration."**

## Documentação

- [x] 32. Criar `frontend/infra/terraform/README.md` (passo a passo de
      `init`/`import` de ambas as configs, mesmo padrão de
      `backend/infra/terraform/README.md`)
- [x] 33. Atualizar `frontend/infra/CLAUDE.md` para descrever a
      arquitetura de duas camadas e que a infra de hosting + DNS do
      frontend passam a ser geridas por Terraform

## Fechamento

- [x] 34. Confirmar que nenhum record `NS`/`SOA` ou de
      `api.jrnexpenses.com` foi importado/referenciado em nenhuma das
      duas configs — confirmado via `aws route53 list-resource-record-sets`:
      apenas os 6 records do frontend + a zona foram trazidos para o
      Terraform; `NS`/`SOA` e os 2 records de `api.jrnexpenses.com`
      permanecem fora, intocados
- [x] 35. Atualizar `spec.md` marcando todos os critérios de aceite
      concluídos