# Tasks — FEAT-12: Domínio customizado da API sob Terraform

- [x] 1. Confirmar o ARN completo do certificado ACM `api.jrnexpenses.com`
      via `aws acm list-certificates` (ou console) — registrar o valor
      usado no `plan.md`/commit
      (`arn:aws:acm:us-east-1:648443184523:certificate/1b64dbcd-776f-4008-8a3a-2683ceb34fab`)
- [x] 2. Confirmar o `api_mapping_id` do mapeamento existente entre
      `api.jrnexpenses.com` e o stage `$default` via
      `aws apigatewayv2 get-api-mappings --domain-name api.jrnexpenses.com`
      (`oqn3qo`, api `dhb1xc3bsi`)
- [x] 3. Confirmar `endpoint_type` e `security_policy` reais do domínio
      customizado via
      `aws apigatewayv2 get-domain-name --domain-name api.jrnexpenses.com`
      (`REGIONAL`/`TLS_1_2`, confirmando os valores assumidos no `plan.md`)
- [x] 4. Criar `backend/infra/terraform/acm.tf` com o recurso
      `aws_acm_certificate.api` (`api.jrnexpenses.com`, `validation_method = "DNS"`)
- [x] 5. Criar `backend/infra/terraform/dns.tf` com o
      `data "aws_route53_zone" "jrnexpenses"` e os recursos
      `aws_route53_record.api_acm_validation` e `aws_route53_record.api_a`
- [x] 6. Criar `backend/infra/terraform/api-gateway-domain.tf` com
      `aws_apigatewayv2_domain_name.api` e `aws_apigatewayv2_api_mapping.api`,
      usando os valores confirmados nas tasks 2 e 3
- [x] 7. Adicionar o output `api_custom_domain_url` em
      `backend/infra/terraform/outputs.tf`
- [x] 8. Rodar `terraform validate`/`terraform plan` (sem aplicar) para
      confirmar que a sintaxe e as referências entre arquivos estão
      corretas antes de qualquer `import`
- [x] 9. Pedir aprovação explícita e executar
      `terraform import aws_acm_certificate.api <arn-completo>`
- [x] 10. Pedir aprovação explícita e executar
      `terraform import 'aws_route53_record.api_acm_validation["api.jrnexpenses.com"]' <zone_id>_<record_name>_<record_type>`
- [x] 11. Pedir aprovação explícita e executar
      `terraform import aws_apigatewayv2_domain_name.api api.jrnexpenses.com`
- [x] 12. Pedir aprovação explícita e executar
      `terraform import aws_apigatewayv2_api_mapping.api <api-mapping-id>/<domain_name>`
      (formato real do provider: `api-mapping-id/domain-name`, sem
      `api-id` — ajustado durante a execução após erro de formato)
- [x] 13. Pedir aprovação explícita e executar
      `terraform import aws_route53_record.api_a <zone_id>_api.jrnexpenses.com_A`
- [x] 14. Rodar `terraform plan` final e ajustar os `.tf` até o
      resultado ser "No changes" contra a conta AWS real — confirmado
      "No changes" já na primeira tentativa pós-import
- [x] 15. Validar manualmente: `GET https://api.jrnexpenses.com/expenses`
      sem token retorna 401 (confirmado via `curl`). Fluxo completo
      autenticado revalidado no `/review`, com usuário de teste
      temporário (`e2e-feat12-review@jrnexpenses.com`, confirmado via
      `admin-confirm-sign-up`, excluído via `admin-delete-user` ao
      final, sem dados de despesa criados): `POST /auth/register` (201)
      → `POST /auth/login` (200) → `GET /auth/me` (200) →
      `GET /expenses` (200) — tudo via `https://api.jrnexpenses.com`
- [x] 16. Validar que a URL padrão do API Gateway
      (`*.execute-api.us-east-1.amazonaws.com`) continua respondendo
      normalmente (401 sem token, igual ao domínio customizado), sem
      regressão
- [x] 17. Atualizar `backend/infra/CLAUDE.md` refletindo que o domínio
      customizado da API (certificado, mapeamento e records) agora é
      gerido por Terraform
- [x] 18. Atualizar `backend/infra/terraform/README.md` com a seção do
      domínio customizado (arquivos novos, ordem de import, recursos
      cobertos)
- [x] 19. Atualizar `backend/specs/FEAT-12-terraform-dominio-customizado-api/spec.md`
      marcando todos os critérios de aceite (`- [ ]` → `- [x]`) e
      preenchendo a seção "Status" com o resultado final (ARNs/IDs
      reais confirmados, resultado do `terraform plan` final, validação
      manual realizada)
