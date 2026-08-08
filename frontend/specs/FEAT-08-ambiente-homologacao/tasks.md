# Tasks — FEAT-08: Ambiente de homologação do frontend

## `environments/hom/` — criação dos arquivos Terraform (código, sem `apply`)

- [x] 1. Criar `frontend/infra/terraform/environments/hom/versions.tf`
      (mesmo provider/backend S3 de `environments/prod/`, `key =
      "gastosapp-frontend/hom/terraform.tfstate"`)
- [x] 2. Criar `frontend/infra/terraform/environments/hom/variables.tf`
      (`aws_region`, `hom_domain_name` default
      `"hom.jrnexpenses.com"`, `frontend_bucket_name` default
      `"gastosapp-frontend-hom"`)
- [x] 3. Criar `frontend/infra/terraform/environments/hom/s3.tf`
      (`aws_s3_bucket`, `aws_s3_bucket_public_access_block`,
      `aws_s3_bucket_server_side_encryption_configuration`,
      `aws_s3_bucket_policy` — mesmo padrão de `environments/prod/s3.tf`)
- [x] 4. Criar `frontend/infra/terraform/environments/hom/acm.tf`
      (`aws_acm_certificate` para `var.hom_domain_name`, sem SAN,
      `validation_method = "DNS"`, `create_before_destroy = true`)
- [x] 5. Criar `frontend/infra/terraform/environments/hom/waf.tf`
      (`aws_wafv2_web_acl` dedicado de hom, `scope = "CLOUDFRONT"`, os
      mesmos 3 AWS Managed Rule Groups de `environments/prod/waf.tf`)
- [x] 6. Criar `frontend/infra/terraform/environments/hom/cloudfront.tf`
      (`aws_cloudfront_origin_access_control` +
      `aws_cloudfront_distribution`, `aliases = [var.hom_domain_name]`,
      `web_acl_id = aws_wafv2_web_acl.hom.arn`, `price_class =
      "PriceClass_All"`, cache policy `CachingOptimized`)
- [x] 7. Criar `frontend/infra/terraform/environments/hom/outputs.tf`
      (`cloudfront_domain_name`, `cloudfront_hosted_zone_id`,
      `acm_domain_validation_options`)
- [x] 8. Rodar `terraform init` + `terraform validate` (+ `terraform
      plan`) em `environments/hom/` — `init -backend=false` e
      `validate` passaram (config sintaticamente válida); `plan` real
      contra a conta AWS não roda neste ambiente (sem credenciais AWS
      aqui) — fica para a task 9, com suas credenciais

## `environments/hom/` — provisionamento (toca a conta AWS real)

- [x] 9. Apresentar o `terraform plan` de `environments/hom/` ao
      usuário e obter aprovação explícita antes de executar — 8 a
      criar, 0 a alterar/destruir, aprovado
- [x] 10. Executar `terraform apply` em `environments/hom/` — feito em
      duas etapas por causa da dependência circular ACM→DNS→CloudFront
      (certificado precisa validar via DNS antes do CloudFront aceitar
      associá-lo): 1) bucket S3, WAF WebACL, OAC, certificado ACM
      (`PENDING_VALIDATION`) criados; distribuição falhou
      (`InvalidViewerCertificate`, esperado); 2) `dns/` aplicado só com
      `-target` no CNAME de validação do ACM de hom (task adiantada,
      ver seção `dns/` abaixo); 3) certificado virou `ISSUED`; 4)
      `terraform apply` completado em `environments/hom/` — distribuição
      `ELE195A1APCLB` (`d15nea4q76w097.cloudfront.net`) e bucket policy
      criados. 8/8 recursos no state
- [x] 11. **Checkpoint manual**: avisado o usuário — assinatura da
      distribuição ao plano Free feita manualmente no console
- [x] 12. Confirmado pelo usuário (print do console):
      `gastosapp-cdn-hom` no **Free plan ($0/month)**, `PriceClass_All`,
      certificado `hom.jrnexpenses.com` associado

## `dns/` — novos records para hom (código, sem `apply`)

- [x] 13. Adicionar `hom_state_key` em
      `frontend/infra/terraform/dns/variables.tf`
- [x] 14. Adicionar `data "terraform_remote_state" "hom"` em
      `frontend/infra/terraform/dns/remote_state.tf`
- [x] 15. Adicionar `aws_route53_record.hom_a` / `hom_aaaa` (alias
      para a distribuição de hom) em
      `frontend/infra/terraform/dns/route53.tf`
- [x] 16. Adicionar o(s) `aws_route53_record` de validação DNS do
      certificado ACM de hom em
      `frontend/infra/terraform/dns/route53.tf`
- [x] 17. Rodar `terraform validate` em `dns/` — passou (`.terraform/`
      já inicializado neste diretório desde a FEAT-07); `plan` real
      fica para a task 18, com credenciais AWS

## `dns/` — provisionamento (toca a conta AWS real)

- [x] 18. Apresentar o `terraform plan` de `dns/` ao usuário e obter
      aprovação explícita antes de executar — feito em duas etapas: o
      CNAME de validação do ACM foi adiantado (via `-target`) durante a
      task 10 para destravar a dependência circular ACM→CloudFront; os
      records `hom_a`/`hom_aaaa` (2 a criar) foram aprovados e
      aplicados depois, com a distribuição já existindo
- [x] 19. Executar `terraform apply` em `dns/` — `acm_validation_hom`
      criado (task 10), `hom_a`/`hom_aaaa` criados depois. `state list`
      confirma 4 records de hom + os 6 de prod (inalterados)

## Validação manual (infraestrutura)

- [x] 20. Validar que `https://hom.jrnexpenses.com` responde via HTTPS
      com certificado válido — `curl` retornou `403` (bucket vazio,
      esperado, não é regressão)
- [x] 21. Validar que `https://jrnexpenses.com`/`https://www.jrnexpenses.com`
      (produção) continuam respondendo exatamente como antes — `curl`
      retornou `200` em ambos (US4)

## `frontend/app/` — build apontando para a API de homologação

- [x] 22. Criar `frontend/app/.env.hom.example`
      (`VITE_API_BASE_URL=https://api-hom.jrnexpenses.com`) — também
      ajustado `frontend/app/.gitignore` (adicionado
      `!.env.hom.example`; o padrão `.env.*` já existente ignorava esse
      arquivo novo, só `.env.example` tinha exceção)
- [x] 23. Adicionar script `"build:hom": "tsc -b && vite build --mode
      hom"` em `frontend/app/package.json`
- [x] 24. Criar localmente `frontend/app/.env.hom` (não versionado, a
      partir do `.env.hom.example`) e rodar `npm run build:hom` — build
      passou; confirmado via grep no bundle gerado que só
      `https://api-hom.jrnexpenses.com` aparece (não a API de produção)

## Validação manual (end-to-end)

- [x] 25. Fazer upload manual, uma única vez, do `dist/` gerado
      (task 24) para o bucket `gastosapp-frontend-hom` — `aws s3 sync
      --delete`, 10 arquivos enviados, aprovado explicitamente pelo
      usuário
- [x] 26. Validar que `https://hom.jrnexpenses.com` serve o SPA
      corretamente — `curl` retornou `200`, `Content-Type: text/html`,
      HTML do `index.html` do build de hom (US2)
- [x] 27. Documentado: a validação completa de chamadas de API a
      partir de `hom.jrnexpenses.com` (US3) depende da liberação de
      CORS em `https://api-hom.jrnexpenses.com` para essa origem —
      mudança do contexto backend, fora do escopo desta feature (ver
      "Fora do escopo" da spec). Gap conhecido, não bloqueia o
      fechamento desta feature

## Documentação e fechamento

- [x] 28. Atualizar `frontend/infra/CLAUDE.md` documentando o
      ambiente de homologação, a existência do WAF dedicado no plano
      Free e o gap conhecido da assinatura manual ao plano
- [x] 29. Atualizar `frontend/infra/terraform/README.md` com o
      passo a passo de `environments/hom/` e `dns/`, incluindo a
      dependência circular ACM→DNS→CloudFront encontrada na execução e
      o checkpoint manual da assinatura ao plano Free
- [x] 30. Atualizar `frontend/specs/FEAT-08-ambiente-homologacao/spec.md`,
      critérios de aceite marcados (`- [x]`) e seção "Status" preenchida
      com o resumo do que foi provisionado/validado, incluindo os dois
      gaps conhecidos (CORS e assinatura manual ao plano Free)
